using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using System.Resources;

namespace LHP2_Archi_Mod;

public class Game
{
    public int PrevLevelID { get; private set; } = -1;
    public int LevelID { get; private set; } = -1;
    public int PrevMapID { get; private set; } = -1;
    public int MapID { get; private set; } = -1;
    public bool PrevInShop { get; private set; } = false;
    public bool PrevInMenu { get; private set; } = false;
    public const int tokenOffset = 213;
    public const int levelOffset = 450;
    public const int SIPOffset = 475;
    public const int GryfCrestOffset = 550;
    public const int SlythCrestOffset = 574;
    public const int RavenCrestOffset = 598;
    public const int HuffleCrestOffset = 622;
    public const int TrueWizardOffset = 675;
    public const int startingItem = 450;
    public const int TotalItems = 700;
    public static void CheckGameLoaded()
    {
        Console.WriteLine("Checking to see if game is loaded");
        int rewriteNumber = 0;
        while(!MenuLoaded())
        {
            if (rewriteNumber % 10 == 0)
                Console.WriteLine("Waiting for menu to load");
            rewriteNumber++;
            System.Threading.Thread.Sleep(500);

        }
        Console.WriteLine("Menu loaded, setting up hooks. Please wait to Connect to the server before loading a save file.");
    }

    public static void CheckSaveFileLoaded()
    {
        int rewriteNumber = 0;
        Mod.InitOnMenu();
        while(!PlayerControllable())
        {
            if (rewriteNumber % 10 == 0)
                Console.WriteLine("Waiting for game to be loaded");
            rewriteNumber++;
            System.Threading.Thread.Sleep(500);
        }
        Console.WriteLine("Save File Loaded");
    }

    public static unsafe bool MenuLoaded()
    {
        try
        {
            byte** basePtr = (byte**)(Mod.BaseAddress + 0xC4ED1C);
            if (basePtr == null || *basePtr == null)
                return false;

            byte* finalPtr = (byte*)(*basePtr + 0x190);
            if (finalPtr == null)
                return false;

            return *finalPtr != 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in checking if menu is loaded: {ex.Message}");
            return false;
        }
    }

    // Currently unused, but leaving in for future items that are instant benefit/detriment and not collectables
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

    public static void writeN0CUT5Flag()
    {
        unsafe
        {
            int* cutsceneBaseAddress = (int*)(Mod.BaseAddress + 0xB06F2C);
            nuint ptr = (nuint)(*cutsceneBaseAddress + 0xA4);

            // Write N0CUT5 flag to game
            Memory.Instance.Write(ptr, (byte) 0x01 );
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
            writeN0CUT5Flag();

            // NOP GB Corrector #1
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x332694, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            // NOP GB Corrector #2
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB8B, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            //// NOP GB Corrector #3 (crashes game? - only used when GB >200 so not end of world)
            //Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB90, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });

            // Unlock Current Level Story
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B817E, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            // Unlock Current Level Freeplay
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B8165, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            //NOP Unlock Next Story Level
            // Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B809C, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
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
                case < 213:
                    Character.UnlockCharacter(ItemID);
                    break;
                case < 426:
                    int token = ItemID - tokenOffset;
                    Character.UnlockToken(token);
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
                case < 725:
                    Bricks.ReceivedGoldBrick();
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
    private static IReverseWrapper<OpenMenu> _reverseWrapOnOpenMenu = default!;
    private static IReverseWrapper<CloseMenu> _reverseWrapOnCloseMenu = default!;


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
        _asmHooks.Add(hooks.CreateAsmHook(completeLevelHook, (int)(Mod.BaseAddress + 0x4B80CB), AsmHookBehaviour.ExecuteAfter).Activate());

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
         _asmHooks.Add(hooks.CreateAsmHook(completeTrueWizardHook, (int)(Mod.BaseAddress + 0x5B2A83), AsmHookBehaviour.ExecuteAfter).Activate());

        string[] completeHogwartsCrestHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHouseCrest, out _reverseWrapOnCrests)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(completeHogwartsCrestHook, (int)(Mod.BaseAddress + 0x16C0A), AsmHookBehaviour.ExecuteAfter).Activate());

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

        string[] OpenMenuHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(onOpenMenu, out _reverseWrapOnOpenMenu)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(OpenMenuHook, (int)(Mod.BaseAddress + 0x212145), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] CloseMenuHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(onCloseMenu, out _reverseWrapOnCloseMenu)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(CloseMenuHook, (int)(Mod.BaseAddress + 0x218347), AsmHookBehaviour.ExecuteAfter).Activate());
    }

     [Function(CallingConventions.Fastcall)]
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
    }

    [Function(new FunctionAttribute.Register[] { FunctionAttribute.Register.ecx }, 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateMap(int value);
    private static void OnMapChange(int value)
    {
        Mod.GameInstance!.PrevMapID = Mod.GameInstance!.MapID;
        Mod.GameInstance!.MapID = value;
        Console.WriteLine($"Map ID updated to {value}.");
        ResetToLocations(value);
    }

    [Function(new FunctionAttribute.Register[] { FunctionAttribute.Register.eax, FunctionAttribute.Register.esp}, 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenCloseShop(int eax, int esp);
    private static void onShopChange(int eax, int esp)
    {
        bool eaxBit0Set = (eax & 1) != 0;
        int lastNibble = esp & 0xF;
        Console.WriteLine($"Last Nibble: {lastNibble}");

        //TODO: Test in different years
        if(eaxBit0Set && lastNibble == 0x08)
        {
            Mod.GameInstance!.PrevInShop = true;
            Console.WriteLine("Level Selector Opened");
            ResetToItems(Mod.GameInstance!.MapID);
        }
        if(eaxBit0Set && lastNibble == 0x0C)
        {
            Mod.GameInstance!.PrevInShop = true;
            Console.WriteLine("Shop Opened");
            ResetToItems(Mod.GameInstance!.MapID);
        }
        else if(!eaxBit0Set && Mod.GameInstance!.PrevInShop)
        {
            Mod.GameInstance!.PrevInShop = false;
            Console.WriteLine("Shop or Level Selector Closed");

            // Game enters a level before thinking you are out of shop, so if we stay in hub, resetting levels here
            if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
            {
                ResetToLocations(Mod.GameInstance!.MapID);
            }
        }
    }

    // Picking up a collectable in hub triggers menu? edi was always 2 when pausing and EBP was always 6 when pausing.
    [Function(new FunctionAttribute.Register[] { FunctionAttribute.Register.edi }, 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenMenu(int edi);
    private static void onOpenMenu(int edi)
    {
        if(edi != 2)
        {
            return;
        }
        // Take into account that menu opens when selecting freeplay/story. Additionally, ignore if not in hub
        if(Mod.GameInstance!.PrevInShop == true || Mod.GameInstance!.LevelID < 1 || Mod.GameInstance!.LevelID > 4)
        {
            return;
        }
        Console.WriteLine("Menu Opened");
        Mod.GameInstance!.PrevInMenu = true;
        ResetToItems(Mod.GameInstance!.MapID);
        Bricks.GetGoldBrickCount();
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void CloseMenu();
    private static void onCloseMenu()
    {
        // Take into account that this code runs multiple times. Additionally, ignore if not in hub
        if(!Mod.GameInstance!.PrevInMenu || Mod.GameInstance!.LevelID < 1 || Mod.GameInstance!.LevelID > 4)
        {
            return;
        }
        Mod.GameInstance!.PrevInMenu = false;
        Console.WriteLine("Menu Closed");
        ResetToLocations(Mod.GameInstance!.MapID);
    }

    private static void ResetToItems(int mapID)
    {
        Bricks.ResetGoldBrickCount();
        Level.ResetLevels();
        Character.ResetTokens();
        Character.ResetUnlocks();
        Mod.LHP2_Archipelago!.UpdateItemsReceived();
        Level.ImplementMapLogic(mapID);
    }

    private static void ResetToLocations(int mapID)
    {
        Bricks.ResetGoldBrickCount();
        Level.ResetLevels();
        Character.ResetTokens();
        Character.ResetUnlocks();
        Mod.LHP2_Archipelago!.UpdateLocationsChecked();
        Level.ImplementMapLogic(mapID);
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
