using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using System.Collections.Concurrent;
using LHP2_Archi_Mod.Addresses;

namespace LHP2_Archi_Mod;

// We use a record container to hold the hint messages and their associated type (i.e. progression, filler, trap, useful).
public record HintMessage(string Text, byte MessageType);

public class HintSystem
{
    // PlayerHandler also uses these 2 addresses to check if the player can receive a negative effect (runs on a different thread). Set up a lock for thread safety
    private static readonly object ScreenStateLock = new();

    // This is a helper function to verify if there is anything else on screen before printing a hint message.
    public static unsafe bool IsScreenEmpty()
    {
        byte* screenEmptyBaseAddress = (byte*)Addr.ScreenEmptyByte;
        // 255 is the value when the screen is empty, 0 means something is on screen
        return *screenEmptyBaseAddress == 255;
    }

    // This is a helper function to verify if the player is Not in a Hub cutscene (i.e. umbridge breaking up the students kissing)
    public static unsafe bool IsPlayerNotInHubCutscene()
    {
        if (Addr.HubCutsceneByte == 0)
            return true; // address not resolved: don't block the display
        if (GameVersion.Is64)
            return *(nuint*)Addr.HubCutsceneByte == 0; // cutscene object, null outside cutscenes
        byte* hubCutSceneAddress = (byte*)Addr.HubCutsceneByte;
        // 48 means that the player is in a hub cutscene, 0 means they are not
        return *hubCutSceneAddress == 0;
    }

    public static (bool nothingOnScreen, bool hubCutscene) GetScreenAndCutsceneState()
    {
        lock (ScreenStateLock)
        {
            bool nothing = IsScreenEmpty();
            bool hub = IsPlayerNotInHubCutscene();
            return (nothing, hub);
        }
    }

    private static readonly ConcurrentQueue<HintMessage> MessageQueue = new();
    // Used a link list for interupted messages so you can add to front of list if it can't be displayed
    private static readonly LinkedList<HintMessage> InterruptedMessageList = new();
    private static readonly object interruptedMessageLock = new();

    // Helper function to add a message to the queue 
    public static void EnqueueMessage(string message, byte messageType = 5)
    {
        if (!string.IsNullOrEmpty(message))
        {
            MessageQueue.Enqueue(new HintMessage(message, messageType));
        }
    }

    // Helper function to add a message to the front of the interrupted message queue
    public static void AddInterruptedMessageToFront(string message, byte messageType)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        lock (interruptedMessageLock)
        {
            if (!InterruptedMessageList.Any(m => m.Text == message))
            {
                InterruptedMessageList.AddFirst(new HintMessage(message, messageType));
            }
        }
    }

    // Timer for how long hints stay on screen (5 seconds)
    private static unsafe float* HintTimerAddress => (float*)Addr.HintTimer;
    // Place the hint code in this address to make the respective text appear on screen
    private static unsafe nuint* HintCodeInterpreterAddress => (nuint*)Addr.HintPtr;
    // The byte here determines what color the hint text prints as
    private static unsafe byte* HintColorAdress => (byte*)Addr.HintColor;

    private static unsafe byte* HintMessageBaseAddress => *(byte**)Addr.MessageBlockPtr;
    // Hint code of the "Some LEGO blocks" text (message #93), to place into the interpreter
    private static unsafe nuint SomeLegoHintCode => (nuint)(HintMessageBaseAddress + Off.MessagePtrValue);
    // Text of message #93; 64-bit: a mod buffer, since the game's string is too short
    private static unsafe nuint SomeLegoTextAddress => GameVersion.Is64 ? OwnHintText : *(nuint*)(SomeLegoHintCode + (nuint)Off.MessageRecordText);
    // Text of the "Use Magic As Harry" hint (message #81); 64-bit: a mod buffer
    private static unsafe nuint UseMagicAsHarryTextAddress => GameVersion.Is64 ? OwnWinConText : *(nuint*)(HintMessageBaseAddress + Off.WinConMessageText);

    // 64-bit: if message #93 isn't the expected record, messages go to the console
    public static bool HintTextAvailable => Addr.MessageBlockPtr != 0 && (!GameVersion.Is64 || _messageRecordUsable);
    private static bool _messageRecordUsable = true;
    private const int MessageRecordIndex64 = 0x18;
    private const int HijackedMessageIndex = 93;
    private static nuint _ownHintText;
    private static nuint _ownWinConText;

    private static nuint OwnHintText => _ownHintText != 0 ? _ownHintText : (_ownHintText = AllocText());
    private static nuint OwnWinConText => _ownWinConText != 0 ? _ownWinConText : (_ownWinConText = AllocText());

    private static unsafe nuint AllocText()
    {
        nuint buffer = (nuint)System.Runtime.InteropServices.Marshal.AllocHGlobal(MaxMessageLength + 1);
        *(byte*)buffer = 0;
        return buffer;
    }

    // 64-bit: points message #93 (0x40-byte record, number at +0x18) at our buffer
    private static unsafe bool RedirectMessageRecord64(nuint record)
    {
        int index = *(int*)(record + MessageRecordIndex64) & 0xFFFF;
        if (index != HijackedMessageIndex)
        {
            Game.PrintToLog($"[Hint] Unexpected message record (#{index} instead of #{HijackedMessageIndex}): messages go to the console.");
            _messageRecordUsable = false;
            return false;
        }
        *(nuint*)(record + (nuint)Off.MessageRecordText) = OwnHintText;
        return true;
    }
    // Limiting the text to 255 characters. Can potentially handle more, but this should be sufficient and prevent unexpected crashes.
    private const int MaxMessageLength = 255;

    // Main function we use (in a separate thread) to print a message on screen
    public static unsafe void HandleMessages()
    {
        if (!HintTextAvailable)
            Game.PrintToLog("[Hint] On-screen messages unavailable (64-bit message text not resolved): messages go to the console.");

        while (true)
        {
            if (GameVersion.Is64)
                RefreshNewGameText64();

            if (!HintTextAvailable)
            {
                DiscardPendingMessages();
                Thread.Sleep(250);
                continue;
            }

            // Several checks we run to determine if something can be printed on screen
            bool playerControllable = Game.IsPlayerControllable();
            bool notInShop;
            bool notInLevelSelect;
            bool notInMenu;
            (bool nothingOnScreen, bool hubCutscene) = GetScreenAndCutsceneState();
            lock (Mod.GameInstance!.StateLock)
            {
                notInShop = Mod.GameInstance!.PrevInShop == false;
                notInLevelSelect = Mod.GameInstance!.PrevInLevelSelect == false;
                notInMenu = Mod.GameInstance!.PrevInMenu == false;
            }

            if (playerControllable && notInShop && notInLevelSelect && notInMenu && nothingOnScreen && hubCutscene)
            {
                // verify if there is no message currently being printed or if the timer is maxed (at 5 seconds)
                if (*HintCodeInterpreterAddress == 0 || *HintTimerAddress >= 5.0f)
                {
                    HintMessage? message = null;
                    lock (interruptedMessageLock)
                    {
                        // If there is a message that was interrupted, we want that to print first
                        if (InterruptedMessageList.Count > 0)
                        {
                            message = InterruptedMessageList.First!.Value;
                            InterruptedMessageList.RemoveFirst();
                        }
                    }
                    // If there wasn't anything in the interrupted queue, try to get something from the message queue
                    if (message == null)
                    {
                        MessageQueue.TryDequeue(out var dequeuedMessage);
                        message = dequeuedMessage;
                    }

                    // If there was something in the message queue, print it out
                    if (message != null)
                    {
                        nuint hintCode = SomeLegoHintCode;
                        if (GameVersion.Is64 && !RedirectMessageRecord64(hintCode))
                            continue;

                        WriteTextToMemory(message.Text, SomeLegoTextAddress); // Set the designated messaged
                        *HintCodeInterpreterAddress = hintCode; // Set hint system pointer to our message
                        *HintColorAdress = message.MessageType; // Set Color based on item progression
                        *HintTimerAddress = 0f; // Restart Hint timer, shows for 5 seconds
                    }
                }
                Thread.Sleep(100);
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }

    // 64-bit fallback: drop the queues, APHandler already logs item and hint messages
    private static void DiscardPendingMessages()
    {
        lock (interruptedMessageLock)
            InterruptedMessageList.Clear();
        MessageQueue.Clear();
    }

    /* 
    This function is called when the game removes the hint message from the screen and adds it to the interrupted queue.
    This can happen due to opening a shop, walking through a loading zone, pausing, etc.
    timerAlreadyReset: the second 64-bit hook runs before the game resets the timer.
    */
    public static unsafe void HandleInterruptedMessage(bool timerAlreadyReset = false)
    {
        bool timerExpired = !timerAlreadyReset && *HintTimerAddress > 4f;
        if (timerExpired || *HintCodeInterpreterAddress == 0) // If timer is greater than 4 seconds or if there is nothing on screen, we can return
        {
            *HintCodeInterpreterAddress = 0;
            return;
        }

        if (!HintTextAvailable)
            return; // 64-bit: message text unavailable, nothing to queue again

        string currentMessage = ReadTextFromMemory(SomeLegoTextAddress); // Read the current message from memory
        byte currentMessageType = *HintColorAdress; // Read the message type from memory

        if (string.IsNullOrEmpty(currentMessage))
        {
            Game.PrintToLog("Message was interrupted, but had a null value");
            return;
        }

        if (currentMessage.Length > MaxMessageLength)
        {
            Game.PrintToLog("Unexpected Behavior, hint message exceeded max length");
            return;
        }

        if (!string.IsNullOrEmpty(currentMessage))
        {
            AddInterruptedMessageToFront(currentMessage, currentMessageType);
        }
    }

    /*
    Helper function to convert a string to ASCII encoded bytes
    Used primarily for hint system, but also used to restore Return to Leaky Cauldron in The Seven Harrys since the Delum and Bag lesson messes with it to ensure you learn apparition
    */
    // Reads a game string back with the same encoding as WriteTextToMemory
    private static unsafe string ReadTextFromMemory(nuint address)
    {
        byte* text = (byte*)address;
        int length = 0;
        while (length < MaxMessageLength && text[length] != 0)
            length++;
        return TextEncoding.GetString(text, length);
    }

    // Both versions draw UTF-8: a single-byte "é" eats the next letters
    private static System.Text.Encoding TextEncoding => System.Text.Encoding.UTF8;

    public static void WriteTextToMemory(string newText, nuint memoryAddress)
    {
        if (memoryAddress == 0)
            return;


        // Encode and null-terminate
        var bytes = TextEncoding.GetBytes(newText + '\0');

        // Ensure that our message isn't too large to print.
        if (bytes.Length > MaxMessageLength)
        {
            // keep the terminator, and don't cut a UTF-8 character in half
            int cut = MaxMessageLength - 1;
            while (cut > 0 && (bytes[cut] & 0xC0) == 0x80)
                cut--;
            var full = new byte[cut + 1];
            Array.Copy(bytes, full, cut);
            bytes = full;
        }

        // Write the message directly to memory
        Memory.Instance.SafeWrite(memoryAddress, bytes);
    }

    private static nuint OriginalPausedAddress = 0;

    // "Paused" text pointer in the text table (entry 0xE5, 0xF1 on 64-bit)
    private static unsafe nuint* PausedTextSlot => (nuint*)(*(byte**)Addr.TextTablePtr + Off.PauseTextSlot);
    private static bool WinConTextAvailable => HintTextAvailable && Addr.TextTablePtr != 0;

    // This is a helper function that verifies the count of horcruxes received and updates the on screen text
    public static unsafe void UpdateWinConText()
    {
        if (!WinConTextAvailable)
            return; // text table not resolved

        nuint* pausedTextBaseAddress = PausedTextSlot;
        if (OriginalPausedAddress == 0)
        {
            OriginalPausedAddress = *pausedTextBaseAddress;
            Game.PrintToLog($"Set OriginalPausedAddress to: 0x{OriginalPausedAddress:X}");
        }

        *pausedTextBaseAddress = UseMagicAsHarryTextAddress;

        if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 0)
        {
            byte HorcruxCount = (byte)Mod.LHP2_Archipelago!.CountItemsCheckedInRange(440, 446);
            DisplayHorcruxCount(HorcruxCount);
        }
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 2)
        {
            byte levelsBeaten = (byte)Mod.LHP2_Archipelago!.CountLocationsCheckedInRange(450, 473);
            DisplayLevelsBeaten(levelsBeaten);
        }
    }

    public static unsafe void RestoreWinConText()
    {
        if (!WinConTextAvailable || OriginalPausedAddress == 0)
            return;
        nuint* pausedTextBaseAddress = PausedTextSlot;
        *pausedTextBaseAddress = OriginalPausedAddress;
    }

    // Helper function to write the received Horcrux count to the Player 2 slot name
    public static unsafe void DisplayHorcruxCount(byte count)
    {
        string message = $"Horcruxes Collected: {count}/{Mod.LHP2_Archipelago!.SlotDataInstance!.NumberOfRequiredHorcruxes}";
        WriteTextToMemory(message, UseMagicAsHarryTextAddress);
    }

    // Helper function to write the Levels Beaten to the Player 2 slot name
    public static unsafe void DisplayLevelsBeaten(byte count)
    {
        string message = $"Levels Beaten: {count}/{Mod.LHP2_Archipelago!.SlotDataInstance!.NumberOfRequiredLevels}";
        WriteTextToMemory(message, UseMagicAsHarryTextAddress);
    }

    // New Game text on the main menu. We show connection status here.
    public static unsafe byte* NewGameTextAddress => *(byte**)(*(byte**)Addr.NewGameTextPtr + Off.NewGameText);
    private static byte[] OriginalNewGameText = new byte[30];

    public static unsafe void UpdateConnectionStatusText(string status)
    {
        if (GameVersion.Is64)
        {
            UpdateConnectionStatusText64(status);
            return;
        }

        byte* newGameText = NewGameTextAddress;
        if (newGameText == null)
            return;

        if (OriginalNewGameText[0] == 0)
        {
            for (int i = 0; i < OriginalNewGameText.Length; i++)
            {
                OriginalNewGameText[i] = *(newGameText + i);
            }
        }
        WriteTextToMemory(status, (nuint)newGameText);
    }

    public static unsafe void RestoreNewGamePtr()
    {
        if (GameVersion.Is64)
        {
            RestoreNewGamePtr64();
            return;
        }

        byte* newGameText = NewGameTextAddress;
        if (newGameText == null || OriginalNewGameText[0] == 0)
            return;

        for (int i = 0; i < OriginalNewGameText.Length; i++)
        {
            *(newGameText + i) = OriginalNewGameText[i];
        }
    }

    // 64-bit: the menu entry has its own text pointer, pointed at a mod buffer
    private const int MenuItemListMax64 = 4096; // about 100 items on the main menu
    private const int MenuEntryText64 = 0x28;
    private const int NewGameTextSlot64 = 0xF6; // "New Game" entry of the text table
    private static nuint _ownNewGameText;
    private static volatile string? _newGameStatus64;
    private static nuint OwnNewGameText => _ownNewGameText != 0 ? _ownNewGameText : (_ownNewGameText = AllocText());

    // Points every New Game item of the menu at our text; false if the menu has none
    private static unsafe bool RedirectNewGameItems64()
    {
        // Fault-tolerant reads: the menu may already be freed when a new game starts
        // MenuNewGameOwner is the tail of the menu item list, the head is just before it
        if (Addr.MenuNewGameOwner == 0 || !TryReadPointer(Addr.MenuNewGameOwner - 8, out nuint node))
            return false;
        nuint gameText = GameNewGameString64();
        if (gameText == 0)
            return false;
        bool found = false;
        for (int i = 0; i < MenuItemListMax64 && node != 0; i++)
        {
            nuint current = node;
            if (!TryReadPointer(current, out node))
                break;
            if (!TryReadPointer(current + 8, out nuint entry) || entry < 0x10000 || !TryReadPointer(entry, out nuint vtable))
                continue;
            // only write into a game object
            if (vtable <= Mod.BaseAddress || vtable >= Mod.BaseAddress + GameVersion.SizeOfImage)
                continue;
            if (!TryReadPointer(entry + MenuEntryText64, out nuint text))
                continue;
            if (text == gameText)
                *(nuint*)(entry + MenuEntryText64) = OwnNewGameText;
            found |= text == gameText || text == OwnNewGameText;
        }
        return found;
    }

    private static unsafe void UpdateConnectionStatusText64(string status)
    {
        WriteTextToMemory(status, OwnNewGameText);
        _newGameStatus64 = status;
        if (!RedirectNewGameItems64())
            Game.PrintToLog($"[Menu] New Game entry not found: {status}");
    }

    // Entry 0xF6 of the text table, the string the menu entry points to
    private static unsafe nuint GameNewGameString64()
    {
        if (Addr.TextTablePtr == 0 || !TryReadPointer(Addr.TextTablePtr, out nuint table) || table == 0)
            return 0;
        return TryReadPointer(table + NewGameTextSlot64 * 8, out nuint text) ? text : 0;
    }

    // Submenus and the title screen bring back the game's text: point it again, on the menu only
    private static unsafe void RefreshNewGameText64()
    {
        if (_newGameStatus64 == null)
            return;
        if (Addr.MapID == 0 || *(int*)Addr.MapID != 402 || Addr.MenuNewGameOwner == 0)
            return;
        RedirectNewGameItems64();
    }

    // ReadProcessMemory under the hood: a freed address throws instead of crashing like Memory.Read
    private static readonly ExternalMemory SelfMemory = new(System.Diagnostics.Process.GetCurrentProcess());

    private static bool TryReadPointer(nuint address, out nuint value)
    {
        try
        {
            value = SelfMemory.Read<nuint>(address);
            return true;
        }
        catch (Reloaded.Memory.Exceptions.MemoryException)
        {
            value = 0;
            return false;
        }
    }

    // Nothing to write back: the menu entry is freed when a game starts, and our buffer stays valid
    private static void RestoreNewGamePtr64()
    {
        _newGameStatus64 = null;
    }
}