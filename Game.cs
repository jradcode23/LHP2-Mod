using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;

namespace LHP2_Archi_Mod;

public class Game
{
    public int PrevLevelID { get; private set; } = -1;
    public int LevelID { get; private set; } = -1;
    public int PrevMapID { get; private set; } = -1;
    public int MapID { get; private set; } = -1;
    public bool PrevInShop { get; private set; } = false;
    public const int levelOffset = 450;
    public const int SIPOffset = 475;
    public const int GryfCrestOffset = 550;
    public const int SlythCrestOffset = 574;
    public const int RavenCrestOffset = 598;
    public const int HuffleCrestOffset = 622;
    public const int TrueWizardOffset = 675;
    public const int startingItem = 450;
    public const int TotalItems = 700;

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
            Mod.GameInstance!.LevelID = Memory.Instance.Read<int>(Mod.BaseAddress + 0xADDB7C);
            Mod.GameInstance!.MapID = Memory.Instance.Read<int>(Mod.BaseAddress + 0xC5B374);
            Mod.GameInstance!.PrevLevelID = Mod.GameInstance!.LevelID;
            Mod.GameInstance!.PrevMapID = Mod.GameInstance!.MapID;
            Console.WriteLine($"Initial Level ID: {Mod.GameInstance!.LevelID}, Map ID: {Mod.GameInstance!.MapID}");
            Level.MakeAllBoardsVisible();
            //TODO: Look into having shop open when you first load in
            int* cutsceneBaseAddress = (int*)(Mod.BaseAddress + 0xC5D5F4);
            nuint ptr = (nuint)(*cutsceneBaseAddress + 0x12);

            // Make all cutscenes skippable (except Lesson Outros) TODO: look into N0CUT5 to make it better
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

    public void ManageItem(int ItemID)
    {
    
        //// implement logic for in shop or not controllable
        //if (!PlayerControllable())
        //    return;

        Level.LevelData level;

        switch(ItemID)
        {
                case < 450:
                    Console.WriteLine($"Unknown item received: {ItemID}");
                    break;
                case < 475: // Levels
                    level = Level.ConvertIDToLeveData(ItemID - levelOffset);
                    Level.UnlockLevel(level);
                    break;
                case < 550:
                    level = Level.ConvertIDToLeveData(ItemID - SIPOffset);
                    Level.UnlockStudentInPeril(level);
                    break;
                case < 574:
                    level = Level.ConvertIDToLeveData(ItemID - GryfCrestOffset);
                    Level.UnlockGryffindorCrest(level);
                    break;
                case < 598:
                    level = Level.ConvertIDToLeveData(ItemID - SlythCrestOffset);
                    Level.UnlockSlytherinCrest(level);
                    break;
                case < 622:
                    level = Level.ConvertIDToLeveData(ItemID - RavenCrestOffset);
                    Level.UnlockRavenclawCrest(level);
                    break;
                case < 646:
                    level = Level.ConvertIDToLeveData(ItemID - HuffleCrestOffset);
                    Level.UnlockHufflepuffCrest(level);
                    break;
                case < 700:
                    level = Level.ConvertIDToLeveData(ItemID - TrueWizardOffset);
                    Level.UnlockTrueWizard(level);
                    break;
                default:
                    Console.WriteLine($"Unknown item received: {ItemID}");
                    break;
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
    private static IReverseWrapper<TrueWizardComplete> _reverseWrapOnTrueWizard = default!;
    private static IReverseWrapper<CrestsComplete> _reverseWrapOnCrests = default!;
    private static IReverseWrapper<UpdateLevel> _reverseWrapOnLevelUpdate = default!;
    private static IReverseWrapper<UpdateMap> _reverseWrapOnMapUpdate = default!;
    private static IReverseWrapper<OpenCloseShop> _reverseWrapOnShopUpdate = default!;


    public void SetupHooks(IReloadedHooks hooks)
    {
        string[] completeLevelHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnLevelComplete, out _reverseWrapOnLevelComplete)}",
            "popad",
            "popfd",
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

        string [] completeTrueWizardHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnTrueWizard, out _reverseWrapOnTrueWizard)}",
            "popad",
            "popfd",
        };
         _asmHooks.Add(hooks.CreateAsmHook(completeTrueWizardHook, (int)(Mod.BaseAddress + 0x5B2A83), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] completeHogwartsCrestHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHouseCrest, out _reverseWrapOnCrests)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(completeHogwartsCrestHook, (int)(Mod.BaseAddress + 0x16C0A), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] updateLevelHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnLevelChange, out _reverseWrapOnLevelUpdate)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(updateLevelHook, (int)(Mod.BaseAddress + 0x574661), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] updateMapIDHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnMapChange, out _reverseWrapOnMapUpdate)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(updateMapIDHook, (int)(Mod.BaseAddress + 0x356424), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] openCloseShopHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(onShopChange, out _reverseWrapOnShopUpdate)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(openCloseShopHook, (int)(Mod.BaseAddress + 0x417074), AsmHookBehaviour.ExecuteFirst).Activate());
    }

    [Function(CallingConventions.Cdecl)]
    public delegate void LevelComplete();

    private static void OnLevelComplete()
    {
        int level = Mod.GameInstance!.LevelID;
        int prevLevel = Mod.GameInstance!.PrevLevelID;

        int? apID = GetApID(level, prevLevel);
        if (apID is int id)
            CheckAndReportLocation(id + levelOffset);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void LevelSIPComplete();
    private static void OnLevelSIP()
    {
        int level = Mod.GameInstance!.LevelID;

        int? apID = GetApID(level, 0); // SIP shouldn't need prev level so just pass 0
        if (apID is int id)
            CheckAndReportLocation(id + SIPOffset);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void TrueWizardComplete();
    private static void OnTrueWizard()
    {
        int level = Mod.GameInstance!.LevelID;

        int? apID = GetApID(level, 0); // True Wizard shouldn't need prev level so just pass 0
        if (apID is int id)
            CheckAndReportLocation(id + TrueWizardOffset);
    }

    [Function(new FunctionAttribute.Register[] { FunctionAttribute.Register.eax }, 
        FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void CrestsComplete(int value);
    private static void OnHouseCrest(int value)
    {
        int level = Mod.GameInstance!.LevelID;
        int? apID = GetApID(level, 0); // Crests shouldn't need prev level so just pass 0
        if (apID is int id)
        {
            switch (value)
            {
                case 0x21C:
                    CheckAndReportLocation(id + GryfCrestOffset);
                    break;
                case 0x21E:
                    CheckAndReportLocation(id + SlythCrestOffset);
                    break;
                case 0x220:
                    CheckAndReportLocation(id + RavenCrestOffset);
                    break;
                case 0x222:
                    CheckAndReportLocation(id + HuffleCrestOffset);
                    break;
                default:
                    Console.WriteLine($"Unknown Crest Completed value: {value}. Please report to the devs.");
                    break;
            }
        }
    }

    [Function(new FunctionAttribute.Register[] { FunctionAttribute.Register.eax }, 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateLevel(int value);
    private static void OnLevelChange(int value)
    {
        Mod.GameInstance!.PrevLevelID = Mod.GameInstance!.LevelID;
        Mod.GameInstance!.LevelID = value;
        Console.WriteLine($"Level ID updated to {value}.");
        // Game enters a level before thinking you are out of shop, resetting level here in those cases to make sure House Crests load in properly
        if(value != 1 || value != 2 || value != 3 || value !=4)
        {
            Level.ResetLevels();
            Mod.LHP2_Archipelago!.UpdateLocationsChecked();
        }
    }

    [Function(new FunctionAttribute.Register[] { FunctionAttribute.Register.ecx }, 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateMap(int value);
    private static void OnMapChange(int value)
    {
        Mod.GameInstance!.PrevMapID = Mod.GameInstance!.MapID;
        Mod.GameInstance!.MapID = value;
        Console.WriteLine($"Map ID updated to {value}.");
    }

    [Function(new FunctionAttribute.Register[] { FunctionAttribute.Register.eax }, 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenCloseShop(int eax);
    private static void onShopChange(int eax)
    {
        bool bit0Set = (eax & 1) != 0;

        if(bit0Set)
        {
            Mod.GameInstance!.PrevInShop = true;
            Console.WriteLine("Shop opened");
            Level.ResetLevels();
            Mod.LHP2_Archipelago!.UpdateItemsReceived();
        }
        else if(!bit0Set && Mod.GameInstance!.PrevInShop)
        {
            Mod.GameInstance!.PrevInShop = false;
            Console.WriteLine("Shop closed");
            if (Mod.GameInstance!.LevelID == 1 || Mod.GameInstance!.LevelID == 2 || Mod.GameInstance!.LevelID == 3 || Mod.GameInstance!.LevelID == 4)
            {
                Level.ResetLevels();
                Mod.LHP2_Archipelago!.UpdateLocationsChecked();
            }
        }
    }

    private static void CheckAndReportLocation(int apID)
    {
        if (Mod.LHP2_Archipelago!.IsLocationChecked(apID))
        {
            Console.WriteLine($"Location for AP ID: {apID} already checked");
            return;
        }
        Console.WriteLine($"Checking location for AP ID: {apID}");
        Mod.LHP2_Archipelago!.CheckLocation(apID);
    }
}
