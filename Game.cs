using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Microsoft.VisualBasic;
using Archipelago.MultiClient.Net.Models;
using LHP_Archi_Mod.Template;
using LHP_Archi_Mod.Configuration;

namespace LHP_Archi_Mod;

public class Game
{
    public int PrevLevelID { get; private set; } = -1;
    public int LevelID { get; private set; } = -1;
    public int PrevMapID { get; private set; } = -1;
    public int MapID { get; private set; } = -1;

    public static void GameLoaded()
    {
        Console.WriteLine("Checking to see if save file is loaded");
        while (!PlayerControllable())
        {
            System.Threading.Thread.Sleep(2000);
            Console.WriteLine("Waiting for save file to load");
        }
        Console.WriteLine("Save File loaded!");
    }

    public static unsafe bool PlayerControllable()
    {
        try
        {
            byte** basePtr = (byte**)(Mod.BaseAddress + 0xC5763C);
            if (basePtr == null || *basePtr == null)
                return false;

            byte* finalPtr = (byte*)(*basePtr + 0x119C);
            if (finalPtr == null)
                return false;

            return *finalPtr == 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in InGame check: {ex.Message}");
            return false;
        }
    }

    public void ModifyInstructions()
    {
        unsafe
        {
            int* cutsceneBaseAddress = (int*)(Mod.BaseAddress + 0xC5D5F4);
            nuint ptr = (nuint)(*cutsceneBaseAddress + 0x12);

            // Make all cutscenes skippable (except Lesson Outros) TODO: look into NOCLIP to make it better
            Memory.Instance.SafeWrite(ptr, new byte[]
            { 0x81, 0xFF, 0xFF, 0x76, 0xBF, 0xB7, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0x77, 0xFF, 0xFF, 0xDF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x1F});

            // NOP GB Corrector #1
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x332694, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            // NOP GB Corrector #2
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB8B, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            //// NOP GB Corrector #3 (crashes game?)
            //Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB90, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            // Unlock Current Level Story
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B817E, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            // Unlock Current Level Freeplay
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B8165, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            //NOP Unlock Next Story Level
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B809C, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
        }
    }

    public void ManageItem(int index, ItemInfo item)
    {
        var itemName = item.ItemName;
        var newItemID = (int)(item.ItemId - 400000);
        
        //// implement logic for in shop or not controllable
        //if (!PlayerControllable())
        //    return;

        Level.LevelData level;

        switch(newItemID)
        {
                case < 450:
                    Console.WriteLine($"Unknown item received: {itemName}, {newItemID}");
                    break;
                case < 475:
                    // Todo: Update so that we don't have to subtract 450 every time
                    Console.WriteLine($"Received Level Unlock: {itemName}, {newItemID}");
                    level = Level.ConvertIDToLeveData(newItemID - 450);
                    Level.UnlockLevel(level);
                    break;
                case < 550:
                    Console.WriteLine("Student in Peril Received");
                    level = Level.ConvertIDToLeveData(newItemID - 475);
                    Level.UnlockStudentInPeril(level);
                    break;
                case < 574:
                    Console.WriteLine($"Gryffindor Crest Received");
                    level = Level.ConvertIDToLeveData(newItemID - 550);
                    Level.UnlockGryffindorCrest(level);
                    break;
                case < 598:
                    Console.WriteLine($"Slytherin Crest Received");
                    level = Level.ConvertIDToLeveData(newItemID - 574);
                    Level.UnlockSlytherinCrest(level);
                    break;
                case < 622:
                    Console.WriteLine($"Ravenclaw Crest Received");
                    level = Level.ConvertIDToLeveData(newItemID - 598);
                    Level.UnlockRavenclawCrest(level);
                    break;
                case < 646:
                    Console.WriteLine($"Hufflepuff Crest Received");
                    level = Level.ConvertIDToLeveData(newItemID - 622);
                    Level.UnlockHufflepuffCrest(level);
                    break;
            default:
                    Console.WriteLine($"Unknown item received: {itemName}, {newItemID}");
                    break;
        }
    }

    public void SetCurrentLevelID()
    {
        unsafe
        {
            int* levelIDPtr = (int*)(Mod.BaseAddress + 0xADDB7C);
            if (levelIDPtr == null) return;
            if (*levelIDPtr != LevelID)
            {
                PrevLevelID = LevelID;
                LevelID = *levelIDPtr;
                Console.WriteLine($"Level ID changed to: {LevelID}");
            }
        }
    }

    public void SetCurrentMapID()
    {
        unsafe
        {
            int* MapIDPtr = (int*)(Mod.BaseAddress + 0xC5B374);
            if (MapIDPtr == null) return;
            if (*MapIDPtr != MapID)
            {
                PrevMapID = MapID;
                MapID = *MapIDPtr;
                Console.WriteLine($"Map ID changed to: {MapID}");
            }
        }
    }

    // TODO: Hook this instead of having a thread
    public void GameLoop()
    {
        while (true)
        {
            SetCurrentLevelID();
            SetCurrentMapID();
            Thread.Sleep(500);
        }
    }

    public static int? GetApID(int level, int prevLevel)
    {
        return level switch
        {
            > 4 => level - 4,
            0 => level,
            > 0 and < 5 when prevLevel == 0 => prevLevel,
            > 0 and < 5 when prevLevel > 4 => prevLevel - 4,
            _ => null
        };
    }

    private static List<IAsmHook> _asmHooks = new List<IAsmHook>();
    private static IReverseWrapper<LevelComplete> _reverseWrapOnLevelComplete = default!;
    private static IReverseWrapper<LevelSIPComplete> _reverseWrapOnLevelSIP = default!;

    public void SetupHooks(IReloadedHooks hooks)
    {
        string[] completeLevelHook =
        {
            "use32",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnLevelComplete, out _reverseWrapOnLevelComplete)}",
        };
        _asmHooks.Add(hooks.CreateAsmHook(completeLevelHook, (int)(Mod.BaseAddress + 0x4B80CB), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] completeLevelSIPHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnLevelSIP, out _reverseWrapOnLevelSIP)}",
            "popad",
            "popfd",

        };
        _asmHooks.Add(hooks.CreateAsmHook(completeLevelSIPHook, (int)(Mod.BaseAddress + 0x313967), AsmHookBehaviour.ExecuteAfter).Activate());
    }

    [Function(CallingConventions.Cdecl)]
    public delegate void LevelComplete();

    private static void OnLevelComplete()
    {
        int level = Mod.GameInstance!.LevelID;
        int prevLevel = Mod.GameInstance!.PrevLevelID;

        int? apID = GetApID(level, prevLevel) + 450;
        if (apID is int id)
            CheckAndReportLocation(id);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void LevelSIPComplete();
        private static void OnLevelSIP()
    {
        int level = Mod.GameInstance!.LevelID;

        int? apID = GetApID(level, 0); // SIP shouldn't need prev level so just pass 0
        if (apID is int id)
            CheckAndReportLocation(id + 475);
    }

    private static void CheckAndReportLocation(int apID)
    {
        if (Mod.LHP_Archipelago!.IsLocationChecked(apID))
            return;

        Console.WriteLine($"Checking location for AP ID: {apID}");
        Mod.LHP_Archipelago?.CheckLocation(apID);
    }
}
