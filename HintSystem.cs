using Reloaded.Memory;
using System.Collections.Concurrent;

namespace LHP2_Archi_Mod;

public struct HintData
{
    public const int MaxLength = 255;
}

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

    private static unsafe bool IsScreenEmpty()
    {
        byte* screenEmptyBaseAddress = (byte*)(Mod.BaseAddress + 0xAD98D9);
        return *screenEmptyBaseAddress == 255; // 255 is the value when the screen is empty, 0 means something is on screen
    }

    private static unsafe bool IsPlayerNotInHubCutscene()
    {
        byte* hubCutSceneAddress = (byte*)(Mod.BaseAddress + 0xC5B224);
        return *hubCutSceneAddress == 0; // 48 means that the player is in a hub cutscene, 0 means they are not
    }

    private static readonly ConcurrentQueue<HintMessage> MessageQueue = new();
    private static readonly LinkedList<HintMessage> InterruptedMessageQueue = new();
    private static readonly object queueLock = new();

    public static void EnqueueMessage(string message, byte messageType = 0)
    {
        // Only want to show messages for our player
        if (!string.IsNullOrEmpty(message))
        {
            MessageQueue.Enqueue(new HintMessage(message, messageType));
        }
    }

    public static unsafe void HandleMessages()
    {
        while (true)
        {
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
                if (*hintPTRBaseAddress == 0 || *hintTimerBaseAddress >= 5.0f)
                {
                    HintMessage? message = null;
                    lock (queueLock)
                    {
                        if (InterruptedMessageQueue.Count > 0)
                        {
                            // Game.PrintToLog($"There are {InterruptedMessageQueue.Count} messages in the interrupted queue.");
                            message = InterruptedMessageQueue.First!.Value;
                            InterruptedMessageQueue.RemoveFirst();
                        }
                    }
                    if (message == null)
                    {
                        MessageQueue.TryDequeue(out var dequeuedMessage);
                        message = dequeuedMessage;
                    }

                    if (message != null)
                    {
                        uint messagePTRValue = MessagePTRValue;
                        uint hintTextPTRAddress = HintTextAddress;

                        // Game.PrintToLog($"Message PTR Value: 0x{messagePTRValue:X}");
                        // Game.PrintToLog($"Hint Text PTR Address: 0x{hintTextPTRAddress:X}");
                        SetMessageText(message.Text, hintTextPTRAddress);
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

    public static unsafe void HandleInterruptedMessage()
    {
        if (*hintTimerBaseAddress > 4f || *hintPTRBaseAddress == 0) // If timer is greater than 4 seconds or if there is nothing on screen, we can return
        {
            *hintPTRBaseAddress = 0;
            return;
        }

        uint hintTextPTRAddress = HintTextAddress;
        string currentMessage = new((sbyte*)hintTextPTRAddress);
        byte currentMessageType = *hintColor;

        if (!string.IsNullOrEmpty(currentMessage))
        {
            lock (queueLock)
            {
                if (!InterruptedMessageQueue.Any(m => m.Text == currentMessage))
                {
                    // Game.PrintToLog("Adding Message to Interrupted Queue");
                    InterruptedMessageQueue.AddFirst(new HintMessage(currentMessage, currentMessageType));
                }
            }
        }
    }

    public static void SetMessageText(string newText, uint hintTextPTRAddress)
    {

        // ASCII encode and null-terminate
        var normalized = newText;
        var bytes = System.Text.Encoding.ASCII.GetBytes(normalized + '\0');

        // Ensure buffer is fixed size to avoid wide leftover values.
        if (bytes.Length > HintData.MaxLength)
        {
            var full = new byte[HintData.MaxLength];
            Array.Copy(bytes, full, HintData.MaxLength);
            bytes = full;
        }

        Memory.Instance.WriteRaw(hintTextPTRAddress, bytes);
    }

    public static void DisplayHorcruxCount(byte count)
    {
        string message = $"Horcruxes Collected: {count}";
        SetMessageText(message, PressButtonToStartTextAddress);
    }

}