using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using System.Collections.Concurrent;

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
        byte* screenEmptyBaseAddress = (byte*)(Mod.BaseAddress + 0xAD98D9);
        // 255 is the value when the screen is empty, 0 means something is on screen
        return *screenEmptyBaseAddress == 255;
    }

    // This is a helper function to verify if the player is Not in a Hub cutscene (i.e. umbridge breaking up the students kissing)
    public static unsafe bool IsPlayerNotInHubCutscene()
    {
        byte* hubCutSceneAddress = (byte*)(Mod.BaseAddress + 0xC5B224);
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
    private static unsafe float* HintTimerAddress => (float*)(Mod.BaseAddress + 0xC5839C);
    // Place the hint code in this address to make the respective text appear on screen
    private static unsafe uint* HintCodeInterpreterAddress => (uint*)(Mod.BaseAddress + 0xC5838C);
    // The byte here determines what color the hint text prints as
    private static unsafe byte* HintColorAdress => (byte*)(Mod.BaseAddress + 0xC58391);

    private static unsafe uint* HintMessageBaseAddress => *(uint**)(Mod.BaseAddress + 0xC58388);
    // Pointer to the Hint Code for "Some LEGO blocks" hint text. Place this address into the interpreter to get it to print on screen
    private static unsafe uint* SomeLegoHintCode => HintMessageBaseAddress + (0xFFC / 4);
    // The address that contains the "Some LEGO blocks" hint is 4 bytes after the hint code
    private static unsafe uint* SomeLegoTextAddress => HintMessageBaseAddress + (0x1000 / 4);
    // The address that contains the "Use Magic As Harry" hint
    private static unsafe uint* UseMagicAsHarryTextAddress => HintMessageBaseAddress + (0xDF0 / 4);
    // Limiting the text to 255 characters. Can potentially handle more, but this should be sufficient and prevent unexpected crashes.
    private const int MaxMessageLength = 255;

    // Main function we use (in a separate thread) to print a message on screen
    public static unsafe void HandleMessages()
    {
        while (true)
        {
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
                        WriteTextToMemory(message.Text, *SomeLegoTextAddress); // Set the designated messaged
                        *HintCodeInterpreterAddress = (uint)SomeLegoHintCode; // Set hint system pointer to our message
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

    /* 
    This function is called when the game removes the hint message from the screen and adds it to the interrupted queue.
    This can happen due to opening a shop, walking through a loading zone, pausing, etc.
    */
    public static unsafe void HandleInterruptedMessage()
    {
        if (*HintTimerAddress > 4f || *HintCodeInterpreterAddress == 0) // If timer is greater than 4 seconds or if there is nothing on screen, we can return
        {
            *HintCodeInterpreterAddress = 0;
            return;
        }

        string currentMessage = new((sbyte*)*SomeLegoTextAddress); // Read the current message from memory
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
    public static void WriteTextToMemory(string newText, uint memoryAddress)
    {

        // ASCII encode and null-terminate
        var bytes = System.Text.Encoding.ASCII.GetBytes(newText + '\0');

        // Ensure that our message isn't too large to print.
        if (bytes.Length > MaxMessageLength)
        {
            var full = new byte[MaxMessageLength];
            Array.Copy(bytes, full, MaxMessageLength);
            bytes = full;
        }

        // Write the message directly to memory
        Memory.Instance.SafeWrite(memoryAddress, bytes);
    }

    private static uint OriginalPausedAddress = 0;

    // This is a helper function that verifies the count of horcruxes received and updates the on screen text
    public static unsafe void UpdateWinConText()
    {
        uint* pausedTextBaseAddress = *(uint**)(Mod.BaseAddress + 0xAE6E58) + 0xE5;
        if (OriginalPausedAddress == 0)
        {
            OriginalPausedAddress = (uint)*(byte**)pausedTextBaseAddress;
            Game.PrintToLog($"Set OriginalPausedAddress to: 0x{OriginalPausedAddress:X}");
        }

        *pausedTextBaseAddress = *UseMagicAsHarryTextAddress;

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
        uint* pausedTextBaseAddress = *(uint**)(Mod.BaseAddress + 0xAE6E58) + 0xE5;
        *pausedTextBaseAddress = OriginalPausedAddress;
    }

    // Helper function to write the received Horcrux count to the Player 2 slot name
    public static unsafe void DisplayHorcruxCount(byte count)
    {
        string message = $"Horcruxes Collected: {count}/{Mod.LHP2_Archipelago!.SlotDataInstance!.NumberOfRequiredHorcruxes}";
        WriteTextToMemory(message, *UseMagicAsHarryTextAddress);
    }

    // Helper function to write the Levels Beaten to the Player 2 slot name
    public static unsafe void DisplayLevelsBeaten(byte count)
    {
        string message = $"Levels Beaten: {count}/{Mod.LHP2_Archipelago!.SlotDataInstance!.NumberOfRequiredLevels}";
        WriteTextToMemory(message, *UseMagicAsHarryTextAddress);
    }

}