using Reloaded.Memory;
using System.Collections.Concurrent;

namespace LHP2_Archi_Mod;

// Struct that contains the max text length that the game can handle
public struct HintData
{
    public const int MaxLength = 255;
}

// We use a record container to hold the hint messages and their associated type (i.e. progression, filler, trap, useful).
public record HintMessage(string Text, byte MessageType);

public class HintSystem
{
    private static unsafe float* hintTimerBaseAddress => (float*)(Mod.BaseAddress + 0xC5839C);
    private static unsafe uint* hintPTRBaseAddress => (uint*)(Mod.BaseAddress + 0xC5838C);
    private static unsafe byte* hintColor => (byte*)(Mod.BaseAddress + 0xC58391);
    private static unsafe byte* HintTextBaseAddress => *(byte**)(Mod.BaseAddress + 0xB16324);
    private static unsafe uint HintTextAddress => (uint)(HintTextBaseAddress + 0xBA);
    private static unsafe uint MessagePTRValue => (uint)(((byte*)*(uint**)(Mod.BaseAddress + 0xC58388)) + 0xFFC);
    private static unsafe byte* PressButtonToStartTextBaseAddress => *(byte**)(Mod.BaseAddress + 0xC4EBFC);
    private static unsafe uint PressButtonToStartTextAddress => (uint)PressButtonToStartTextBaseAddress;

    // This is a helper function to verify if there is anything else on screen before printing a hint message.
    private static unsafe bool IsScreenEmpty()
    {
        byte* screenEmptyBaseAddress = (byte*)(Mod.BaseAddress + 0xAD98D9);
        return *screenEmptyBaseAddress == 255; // 255 is the value when the screen is empty, 0 means something is on screen
    }

    // This is a helper function to verify if the player is Not in a Hub cutscene (i.e. umbridge breaking up the students kissing)
    private static unsafe bool IsPlayerNotInHubCutscene()
    {
        byte* hubCutSceneAddress = (byte*)(Mod.BaseAddress + 0xC5B224);
        return *hubCutSceneAddress == 0; // 48 means that the player is in a hub cutscene, 0 means they are not
    }

    // We set up our thread safe containers and lock for them
    private static readonly ConcurrentQueue<HintMessage> MessageQueue = new();
    private static readonly LinkedList<HintMessage> InterruptedMessageQueue = new();
    private static readonly object queueLock = new();

    // Helper function to add a message to the queue 
    public static void EnqueueMessage(string message, byte messageType = 5)
    {
        if (!string.IsNullOrEmpty(message))
        {
            MessageQueue.Enqueue(new HintMessage(message, messageType));
        }
    }

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
            bool nothingOnScreen = IsScreenEmpty();
            bool hubCutscene = IsPlayerNotInHubCutscene();
            lock (Mod.GameInstance!.StateLock)
            {
                notInShop = Mod.GameInstance!.PrevInShop == false;
                notInLevelSelect = Mod.GameInstance!.PrevInLevelSelect == false;
                notInMenu = Mod.GameInstance!.PrevInMenu == false;
            }

            if (playerControllable && notInShop && notInLevelSelect && notInMenu && nothingOnScreen && hubCutscene)
            {
                // verify if there is no message currently being printed or if the timer is maxed (at 5 seconds)
                if (*hintPTRBaseAddress == 0 || *hintTimerBaseAddress >= 5.0f)
                {
                    HintMessage? message = null;
                    lock (queueLock)
                    {
                        // If there is a message that was interrupted, we want that to print first
                        if (InterruptedMessageQueue.Count > 0)
                        {
                            message = InterruptedMessageQueue.First!.Value;
                            InterruptedMessageQueue.RemoveFirst();
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
                        uint messagePTRValue = MessagePTRValue;
                        uint hintTextPTRAddress = HintTextAddress;

                        SetMessageText(message.Text, hintTextPTRAddress); // Set the designated messaged
                        *hintPTRBaseAddress = messagePTRValue; // Set hint system pointer to our message
                        *hintColor = message.MessageType; // Set Color based on item progression
                        *hintTimerBaseAddress = 0f; // Restart Hint timer, shows for 5 seconds
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
        if (*hintTimerBaseAddress > 4f || *hintPTRBaseAddress == 0) // If timer is greater than 4 seconds or if there is nothing on screen, we can return
        {
            *hintPTRBaseAddress = 0;
            return;
        }

        uint hintTextPTRAddress = HintTextAddress;
        string currentMessage = new((sbyte*)hintTextPTRAddress); // Read the current message from memory
        byte currentMessageType = *hintColor; // Read the message type from memory

        if (string.IsNullOrEmpty(currentMessage))
        {
            Game.PrintToLog("Hint Message has a null value");
            return;
        }

        if (currentMessage.Length > HintData.MaxLength)
        {
            Game.PrintToLog("Unexpected Behavior, hint message exceeded max length");
            return;
        }

        if (!string.IsNullOrEmpty(currentMessage))
        {
            lock (queueLock)
            {
                // Verifies that the message isn't already in the queue
                if (!InterruptedMessageQueue.Any(m => m.Text == currentMessage))
                {
                    // Adds the message to the front of the queue
                    InterruptedMessageQueue.AddFirst(new HintMessage(currentMessage, currentMessageType));
                }
            }
        }
    }

    /*
    Helper function to convert a string to ASCII encoded bytes
    Used primarily for hint system, but also used to restore Return to Leaky Cauldron in The Seven Harrys since the Delum and Bag lesson messes with it to ensure you learn apparition
    */
    public static void SetMessageText(string newText, uint hintTextPTRAddress)
    {

        // ASCII encode and null-terminate
        var normalized = newText;
        var bytes = System.Text.Encoding.ASCII.GetBytes(normalized + '\0');

        // Ensure that our message isn't too large to print.
        if (bytes.Length > HintData.MaxLength)
        {
            var full = new byte[HintData.MaxLength];
            Array.Copy(bytes, full, HintData.MaxLength);
            bytes = full;
        }

        // Write the message directly to memory
        Memory.Instance.WriteRaw(hintTextPTRAddress, bytes);
    }

    // Helper function to write the received Horcrux count to the Player 2 slot name
    public static void DisplayHorcruxCount(byte count)
    {
        string message = $"Horcruxes Collected: {count}";
        SetMessageText(message, PressButtonToStartTextAddress);
    }

}