using Reloaded.Memory;
using System.Collections.Concurrent;

namespace LHP2_Archi_Mod;

public unsafe struct HintData
{
    public const int MaxLength = 101;
    public fixed char Text[MaxLength];
}

public class HintSystem
{
    private static unsafe readonly float* hintTimerBaseAddress = (float*)(Mod.BaseAddress + 0xC5839C);
    private static unsafe readonly uint* hintPTRBaseAddress = (uint*)(Mod.BaseAddress + 0xC5838C);
    
    private static unsafe uint GetHintTextAddress()
    {
        byte* hintTextBaseAddress = *(byte**)(Mod.BaseAddress + 0xB16324);
        return (uint)(hintTextBaseAddress + 0xBA);
    }
    
    private static unsafe uint GetMessagePTRValue()
    {
        uint* messageBaseAddress = *(uint**)(Mod.BaseAddress + 0xc58388);
        return (uint)((byte*)messageBaseAddress + 0xFFC);
    }

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
    
    private static readonly ConcurrentQueue<string> MessageQueue = new();
    
    public static void EnqueueMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            MessageQueue.Enqueue(message);
        }
    }
    

    public static unsafe void HandleMessages()
    {
        while (true)
        {
            bool playerControllable = Game.PlayerControllable();
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
                if (MessageQueue.TryDequeue(out string? message))
                {
                    uint messagePTRValue = GetMessagePTRValue();
                    uint hintTextPTRAddress = GetHintTextAddress();
                    
                    Mod.Logger!.WriteLineAsync($"Message PTR Value: 0x{messagePTRValue:X}");
                    Mod.Logger!.WriteLineAsync($"Hint Text PTR Address: 0x{hintTextPTRAddress:X}");
                    SetMessageText(message, hintTextPTRAddress);
                    *hintPTRBaseAddress = messagePTRValue; // Set hint system pointer to our message
                    *hintTimerBaseAddress = 0f; // Restart Hint timer, shows for 5 seconds
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }
    
    private static void SetMessageText(string newText, uint hintTextPTRAddress)
    {

        // ASCII encode and null-terminate
        var normalized = newText.Length >= HintData.MaxLength ? newText[..(HintData.MaxLength - 1)] : newText;
        var bytes = System.Text.Encoding.ASCII.GetBytes(normalized + '\0');

        // Ensure buffer is fixed size to avoid wide leftover values.
        if (bytes.Length < HintData.MaxLength)
        {
            var full = new byte[HintData.MaxLength];
            Array.Copy(bytes, full, bytes.Length);
            bytes = full;
        }

        Memory.Instance.WriteRaw(hintTextPTRAddress, bytes);
    }
}