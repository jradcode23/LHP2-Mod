using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using System.Numerics;
using System.Text;

namespace LHP2_Archi_Mod;

public class Game
{
    public int PrevLevelID { get; private set; } = -1;
    public int LevelID { get; private set; } = -1;
    public int PrevMapID { get; private set; } = -1;
    public int MapID { get; private set; } = -1;
    public bool PrevInShop { get; private set; } = false;
    public bool PrevInLevelSelect { get; private set; } = false;
    public bool PrevInMenu { get; private set; } = false;
    public const int tokenOffset = 213;
    public const int levelOffset = 450;
    public const int SIPOffset = 475;
    public const int HubSIPOffset = 499;
    public const int GryfCrestOffset = 550;
    public const int SlythCrestOffset = 574;
    public const int RavenCrestOffset = 598;
    public const int HuffleCrestOffset = 622;
    public const int TrueWizardOffset = 675;
    public const int GoldBrickPurchOffset = 700;
    public const int HubGBOffset = 716;
    public const int RedBrickCollectOffset = 900;
    public const int RedBrickPurchOffset = 950;
    public const int SpellPurchOffset = 975;
    public const int MaxItemID = 1005;
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
    }

    // public static void CheckSaveFileLoaded()
    // {
    //     int rewriteNumber = 0;

    //     while(!PlayerControllable())
    //     {
    //         if (rewriteNumber % 10 == 0)
    //             Console.WriteLine("Waiting for game to be loaded");
    //         rewriteNumber++;
    //         System.Threading.Thread.Sleep(500);
    //     }
    //     Console.WriteLine("Save File Loaded");
    // }

    public static unsafe bool MenuLoaded()
    {
        try
        {
            if (Mod.GameInstance!.LevelID > 0)
                return true;
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
    // public static unsafe bool PlayerControllable()
    // {
    //     try
    //     {
    //         byte** basePtr = (byte**)(Mod.BaseAddress + 0xC5763C);
    //         if (basePtr == null || *basePtr == null)
    //             return false;

    //         byte* finalPtr = (byte*)(*basePtr + 0x119C);
    //         if (finalPtr == null)
    //             return false;

    //         return *finalPtr == 1;
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error in InGame check: {ex.Message}");
    //         return false;
    //     }
    // }

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
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x332694, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
            // NOP GB Corrector #2
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB8B, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
            //// NOP GB Corrector #3 (crashes game? - only used when GB >200 so not end of world)
            // Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB90, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });

            // Removes the check for Freeplay mode and allows for always checking individual level completion to enable save and exit
            Memory.Instance.SafeWrite(Mod.BaseAddress + 0x40A264, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);

            // // Unlock Current Level Story // Crashes Dark Times
            // Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B817E, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
            // // Unlock Current Level Freeplay
            // Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B8165, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
            //NOP Unlock Next Story Level
            // Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B809C, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        }
    }

    public void ManageItem(int ItemID)
    {
    
        //// implement logic for in shop or not controllable
        //if (!PlayerControllable())
        //    return;

        LevelHandler.LevelData level;

        switch(ItemID)
        {
                case < 213:
                    CharacterHandler.UnlockCharacter(ItemID);
                    break;
                case < 426:
                    int token = ItemID - tokenOffset;
                    CharacterHandler.UnlockToken(token);
                    break;
                case < 475:
                    level = LevelHandler.ConvertIDToLeveData(ItemID - levelOffset);
                    LevelHandler.UnlockLevel(level);
                    break;
                case < 499:
                    level = LevelHandler.ConvertIDToLeveData(ItemID - SIPOffset);
                    LevelHandler.UnlockStudentInPeril(level);
                    break;
                case < 550:
                    HubHandler.UnlockHubSIP(ItemID - HubSIPOffset);
                    break;
                case < 574:
                    level = LevelHandler.ConvertIDToLeveData(ItemID - GryfCrestOffset);
                    LevelHandler.UnlockGryffindorCrest(level);
                    break;
                case < 598:
                    level = LevelHandler.ConvertIDToLeveData(ItemID - SlythCrestOffset);
                    LevelHandler.UnlockSlytherinCrest(level);
                    break;
                case < 622:
                    level = LevelHandler.ConvertIDToLeveData(ItemID - RavenCrestOffset);
                    LevelHandler.UnlockRavenclawCrest(level);
                    break;
                case < 646:
                    level = LevelHandler.ConvertIDToLeveData(ItemID - HuffleCrestOffset);
                    LevelHandler.UnlockHufflepuffCrest(level);
                    break;
                case < 700:
                    level = LevelHandler.ConvertIDToLeveData(ItemID - TrueWizardOffset);
                    LevelHandler.UnlockTrueWizard(level);
                    break;
                case < 900:
                    HubHandler.ReceivedGoldBrick();
                    break;
                case < 935:
                    HubHandler.UnlockHubRB(ItemID - RedBrickCollectOffset);
                    break;
                case < 975:
                    HubHandler.ReceivedRedBrickUnlock(ItemID - RedBrickPurchOffset);
                    break;
                case < 1005:
                    HubHandler.UnlockSpell(ItemID - SpellPurchOffset);
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

    public static List<IAsmHook> _asmHooks = [];
    private static IReverseWrapper<LevelComplete> _reverseWrapOnLevelComplete = default!;
    private static IReverseWrapper<LevelSIPComplete> _reverseWrapOnLevelSIP = default!;
    private static IReverseWrapper<TrueWizardComplete> _reverseWrapOnTrueWizard = default!;
    private static IReverseWrapper<CrestsComplete> _reverseWrapOnCrests = default!;
    private static IReverseWrapper<RedBrickPurchase> _reverseWrapOnRedBrickPurch = default!;
    private static IReverseWrapper<GoldBrickPurchase> _reverseWrapOnGoldBrickPurch = default!;
    private static IReverseWrapper<SpellPurchase> _reverseWrapOnSpellPurch = default!;
    private static IReverseWrapper<HubCharacterCollected> _reverseWrapOnHubCharacterCollected = default!;
    private static IReverseWrapper<LevelCharacterCollected> _reverseWrapOnLevelCharacterCollected = default!;
    private static IReverseWrapper<CharacterPurchased> _reverseWrapOnCharacterPurchased = default!;
    private static IReverseWrapper<HubSIP> _reverseWrapOnHubSIP = default!;
    private static IReverseWrapper<HubGB> _reverseWrapOnHubGB = default!;
    private static IReverseWrapper<HubRB> _reverseWrapOnHubRB = default!;
    private static IReverseWrapper<UpdateLevel> _reverseWrapOnLevelUpdate = default!;
    private static IReverseWrapper<UpdateMap> _reverseWrapOnMapUpdate = default!;
    private static IReverseWrapper<OpenCloseShop> _reverseWrapOnShopUpdate = default!;
    private static IReverseWrapper<OpenMenu> _reverseWrapOnOpenMenu = default!;
    private static IReverseWrapper<CloseMenu> _reverseWrapOnCloseMenu = default!;
    private static IReverseWrapper<CharacterCmp> _reverseWrapOnCharacterCmp = default!;
    private static IReverseWrapper<OpenPolyjuicePot> _reverseWrapOnOpenPolyjuicePot = default!;
    private static IReverseWrapper<ClosePolyjuicePot> _reverseWrapOnClosePolyjuicePot = default!;
    private static IReverseWrapper<ChangeYears> _reverseWrapChangeYears = default!;

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
        _asmHooks.Add(hooks.CreateAsmHook(completeHogwartsCrestHook, (int)(Mod.BaseAddress + 0x16C0A), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] purchaseRedBrick =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnRedBrickPurchase, out _reverseWrapOnRedBrickPurch)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(purchaseRedBrick, (int)(Mod.BaseAddress + 0x8928), AsmHookBehaviour.ExecuteAfter).Activate());
        
        string[] purchaseGoldBrick =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnGoldBrickPurchase, out _reverseWrapOnGoldBrickPurch)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(purchaseGoldBrick, (int)(Mod.BaseAddress + 0x9039), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] purchaseJokeSpell =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnSpellPurchase, out _reverseWrapOnSpellPurch)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(purchaseJokeSpell, (int)(Mod.BaseAddress + 0x33358B), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] hubCharacterCollectedHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubCharacterCollected, out _reverseWrapOnHubCharacterCollected)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(hubCharacterCollectedHook, (int)(Mod.BaseAddress + 0x42F6E), AsmHookBehaviour.ExecuteFirst).Activate());

        string [] levelCharacterCollectedHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnLevelCharacterCollected, out _reverseWrapOnLevelCharacterCollected)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(levelCharacterCollectedHook, (int)(Mod.BaseAddress + 0x42FD7), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] characterPurchasedHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnCharacterPurchased, out _reverseWrapOnCharacterPurchased)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(characterPurchasedHook, (int)(Mod.BaseAddress + 0x418968), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] hubSIPHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubSIP, out _reverseWrapOnHubSIP)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(hubSIPHook, (int)(Mod.BaseAddress + 0x313BF1), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] hubGBHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubGB, out _reverseWrapOnHubGB)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(hubGBHook, (int)(Mod.BaseAddress + 0x3137EF), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] hubRBHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubRB, out _reverseWrapOnHubRB)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(hubRBHook, (int)(Mod.BaseAddress + 0x71E92), AsmHookBehaviour.ExecuteFirst).Activate());

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

        string[] CharacterCmp =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(onCharacterCmp, out _reverseWrapOnCharacterCmp)}",
            "test al, al",
            "jnz skip_cmp", 

            // ---- RUN CMP PATH (original logic) ----
            "popad",
            "popfd",
            "cmp byte [eax + esi + 0x74], 0",
            "je bail",

            // Original success path: mov eax,1 ; jmp 418931
            "mov eax, 1",
            hooks.Utilities.GetAbsoluteJumpMnemonics((nint)Mod.BaseAddress + 0x418931, false),


            // ---- SKIP CMP PATH (pretend CMP passed) ----
            "skip_cmp:",
            "popad",
            "popfd",
            hooks.Utilities.GetAbsoluteJumpMnemonics((nint)Mod.BaseAddress + 0x418931, false),

            // ---- BAIL LABEL (failure path → 41891A) ----
            "bail:",
            hooks.Utilities.GetAbsoluteJumpMnemonics((nint)Mod.BaseAddress + 0x41891A, false),
        };
        _asmHooks.Add(hooks.CreateAsmHook(CharacterCmp, (int)(Mod.BaseAddress + 0x41890C), AsmHookBehaviour.DoNotExecuteOriginal).Activate());

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

        string[] OpenPolyjuicePotHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(onOpenPolyjuicePot, out _reverseWrapOnOpenPolyjuicePot)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(OpenPolyjuicePotHook, (int)(Mod.BaseAddress + 0x3C69A0), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] ClosePolyjuicePotHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(onClosePolyjuicePot, out _reverseWrapOnClosePolyjuicePot)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(ClosePolyjuicePotHook, (int)(Mod.BaseAddress + 0x3C69B0), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] ChangeYearsHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(onChangeYears, out _reverseWrapChangeYears)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(ChangeYearsHook, (int)(Mod.BaseAddress + 0x3A584B), AsmHookBehaviour.ExecuteAfter).Activate());
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

    [Function([FunctionAttribute.Register.eax], 
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

    [Function([FunctionAttribute.Register.ecx], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void RedBrickPurchase(int ecx);
    private static void OnRedBrickPurchase(int ecx)
    {
        CheckAndReportLocation(ecx + RedBrickPurchOffset);
    }

    [Function([FunctionAttribute.Register.ebx], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void GoldBrickPurchase(int ebx);
    private static void OnGoldBrickPurchase(int ebx)
    {
        int itemId = BitOperations.TrailingZeroCount(ebx);
        CheckAndReportLocation(itemId + GoldBrickPurchOffset);

    }

    // TODO: will need to be adjusted for unlockable spells later
    [Function([FunctionAttribute.Register.eax], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void SpellPurchase(int eax);
    private static void OnSpellPurchase(int eax)
    {
        if ((Mod.GameInstance!.MapID == 369 || Mod.GameInstance!.MapID == 375
            || Mod.GameInstance!.MapID == 383 || Mod.GameInstance!.MapID == 387) && Mod.GameInstance!.PrevInShop == true) //Make sure Player is in shop
        {
            int itemId = BitOperations.TrailingZeroCount(eax);
            itemId += SpellPurchOffset;
            if(itemId == 975 || itemId > 994)
            {
                return; // Ignore non purchased spells that are unlocked
            }
            CheckAndReportLocation(itemId);
        }
    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.edx], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void HubCharacterCollected(IntPtr eax, int edx);
    private static void OnHubCharacterCollected(IntPtr eax, int edx)
    {
        int itemID = CharacterHandler.GetHubTokenItemID(eax, edx);
        if (itemID == -1)
        {
            Console.WriteLine("Error getting Level Token Item ID");
            Console.WriteLine($"EAX is: 0x{eax:X}");
            Console.WriteLine($"EDX is: 0x{edx:X}");
            Console.WriteLine("Map ID is: " + Mod.GameInstance!.MapID);
            return;
        }
        CheckAndReportLocation(itemID + tokenOffset);
    }

    [Function([FunctionAttribute.Register.ebx], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void LevelCharacterCollected(int ebx);
    private static void OnLevelCharacterCollected(int ebx)
    {
        int itemID = CharacterHandler.GetLevelTokenItemID(ebx);
        if (itemID == -1)
        {
            Console.WriteLine("Error getting Level Token Item ID");
            Console.WriteLine($"EBX is: 0x{ebx:X}");
            Console.WriteLine("Map ID is: " + Mod.GameInstance!.MapID);
            return;
        }
        CheckAndReportLocation(itemID + tokenOffset);
    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.ecx], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void CharacterPurchased(IntPtr ecx, int eax);
    private static void OnCharacterPurchased(IntPtr ecx, int eax)
    {
        if((Mod.GameInstance!.MapID == 366 || Mod.GameInstance!.MapID == 372 
            || Mod.GameInstance!.MapID == 378 || Mod.GameInstance!.MapID == 382) && Mod.GameInstance!.PrevInShop == true) //Make sure Player is in shop
        {
            int itemID = CharacterHandler.GetPurchaseCharacterID(ecx, eax);
            if(itemID == -1)
            {
                Console.WriteLine("Error getting Purchased Character ID");
                Console.WriteLine($"EAX is: {eax:X}");
                Console.WriteLine($"ECX is: {ecx:X}");
                Console.WriteLine("Map ID is: " + Mod.GameInstance!.MapID);
                return;
            }
            CheckAndReportLocation(itemID);
        }
    }

    [Function([FunctionAttribute.Register.edx], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void HubSIP(int edx);
    private static void OnHubSIP(int edx)
    {

        int itemID = HubHandler.GetHubID(edx);
        if(itemID == -1)
        {
            Console.WriteLine("Error getting SIP ID from Hub");
            Console.WriteLine($"EDX is: 0x {edx:X}");
            int lookupvalue = edx * 4 + 2;
            Console.WriteLine($"Lookup Value should be: 0x{lookupvalue:X}");
            Console.WriteLine("Map ID is: " + Mod.GameInstance!.MapID);
            return;
        }
        CheckAndReportLocation(itemID + HubSIPOffset);

    }

    [Function([FunctionAttribute.Register.eax], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void HubGB(int eax);
    private static void OnHubGB(int eax)
    {

        int itemID = HubHandler.GetHubID(eax);
        if(itemID == -1)
        {
            Console.WriteLine("Error getting GB ID from Hub");
            Console.WriteLine($"EAX is: 0x{eax:X}");
            int lookupvalue = eax * 4 + 2;
            Console.WriteLine($"Lookup Value should be: 0x{lookupvalue:X}");
            Console.WriteLine("Map ID is: " + Mod.GameInstance!.MapID);
            return;
        }
        CheckAndReportLocation(itemID + HubGBOffset);

    }

    [Function([FunctionAttribute.Register.eax], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void HubRB(int eax);
    private static void OnHubRB(int eax)
    {

        int itemID = HubHandler.GetHubID(eax);
        if(itemID == -1)
        {
            Console.WriteLine("Error getting RB ID from Hub");
            Console.WriteLine($"EAX is: 0x{eax:X}");
            int lookupvalue = eax * 4 + 2;
            Console.WriteLine($"Lookup Value should be: 0x{lookupvalue:X}");
            Console.WriteLine("Map ID is: " + Mod.GameInstance!.MapID);
            return;
        }
        CheckAndReportLocation(itemID + RedBrickCollectOffset);

    }

    [Function([FunctionAttribute.Register.eax], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateLevel(int value);
    private static void OnLevelChange(int value)
    {
        Mod.GameInstance!.PrevLevelID = Mod.GameInstance!.LevelID;
        Mod.GameInstance!.LevelID = value;
        Console.WriteLine($"Level ID updated to {value}.");
    }

    [Function([FunctionAttribute.Register.ecx], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateMap(int value);
    private static void OnMapChange(int value)
    {
        Mod.GameInstance!.PrevMapID = Mod.GameInstance!.MapID;
        Mod.GameInstance!.MapID = value;
        Console.WriteLine($"Map ID updated to {value}.");
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
        LevelHandler.ImplementMapLogic(value);
    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.esp], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenCloseShop(int eax, int esp);
    private static void onShopChange(int eax, int esp)
    {
        bool eaxBit0Set = (eax & 1) != 0;
        int lastNibble = esp & 0xF;
        // Console.WriteLine($"Last Nibble: {lastNibble}");

        if(eaxBit0Set && lastNibble == 0x08)
        {
            Mod.GameInstance!.PrevInLevelSelect = true;
            Console.WriteLine("Level Selector Opened");
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, MaxItemID);
        }
        if(eaxBit0Set && lastNibble == 0x0C)
        {
            Mod.GameInstance!.PrevInShop = true;
            Console.WriteLine("Shop Opened");
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(tokenOffset, levelOffset - 25);
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(0, tokenOffset - 1);

            Mod.LHP2_Archipelago!.UpdateBasedOnItems(RedBrickCollectOffset, RedBrickPurchOffset - 1);
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(GoldBrickPurchOffset, MaxItemID);
        }
        else if(!eaxBit0Set && Mod.GameInstance!.PrevInLevelSelect)
        {
            Mod.GameInstance!.PrevInLevelSelect = false;
            Console.WriteLine("Level Selector Closed");

            // Game enters a level before thinking you are out of shop, so if we stay in hub, resetting levels here
            if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
            {
                ResetItems();
                Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
                Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
            }
        }
        else if(!eaxBit0Set && Mod.GameInstance!.PrevInShop)
        {
            Mod.GameInstance!.PrevInShop = false;
            Console.WriteLine("Shop Selector Closed");

            // Game enters a level before thinking you are out of shop, so if we stay in hub, resetting levels here
            if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
            {
                ResetItems();
                Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
                Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
            }
        }
    }

    // Picking up a collectable in hub triggers menu? edi was always 2 when pausing and EBP was always 6 when pausing.
    [Function([FunctionAttribute.Register.edi], 
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenMenu(int edi);
    private static unsafe void onOpenMenu(int edi)
    {
        if(edi != 2)
        {
            return;
        }
        // Take into account that menu opens when selecting freeplay/story.
        if(Mod.GameInstance!.PrevInShop == true)
        {
            return;
        } 

        byte* menuCheatAddress = (byte*)(Mod.BaseAddress + 0xC575E0);
        byte[] bytes = [24, 4, 0, 17, 26, 26];
        for (int i = 0; i < 6; i++)
        {
            Memory.Instance.Write<byte>((nuint)(menuCheatAddress + i), bytes[i]);
        }
        
        if (Mod.GameInstance!.LevelID < 1 || Mod.GameInstance!.LevelID > 4) // If in level, want to sync to locations so they can exit if level completed
        {
            Console.WriteLine("Menu Opened");
            Mod.GameInstance!.PrevInMenu = true;
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(0, MaxItemID);
        }
        else // In Hub, want to show all items
        {
            Console.WriteLine("Menu Opened");
            Mod.GameInstance!.PrevInMenu = true;
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, MaxItemID);
            HubHandler.GetGoldBrickCount();
        }
    }

    [Function([], FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate bool CharacterCmp();

    private static bool onCharacterCmp()
    {
        // Only run CMP on menu map or levels 1–4
        if (Mod.GameInstance!.MapID == 402)
            return false;

        if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
            return false;

        return true;   // skip CMP everywhere else
    }


    [Function(CallingConventions.Fastcall)]
    public delegate void CloseMenu();
    private static void onCloseMenu()
    {
        // Take into account that this code runs multiple times.
        if(!Mod.GameInstance!.PrevInMenu)
        {
            return;
        }
        Mod.GameInstance!.PrevInMenu = false;
        Console.WriteLine("Menu Closed");
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void OpenPolyjuicePot();
    private static unsafe void onOpenPolyjuicePot()
    {
        byte* cauldronBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC54290);
        nuint cauldronItem = Memory.Instance.Read<nuint>((nuint)(cauldronBaseAddress + 0x68));
        // Console.WriteLine($"Cauldron Item ID: {cauldronItem}");
        if(cauldronItem != 4 || Mod.GameInstance!.PrevInLevelSelect == true) // Only trigger on opening the Polyjuice Pot
        {
            return;
        }
        Console.WriteLine("Polyjuice Pot Opened");
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, tokenOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void ClosePolyjuicePot();
    private static unsafe void onClosePolyjuicePot()
    {
        byte* cauldronBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC54290);
        nuint cauldronItem = Memory.Instance.Read<nuint>((nuint)(cauldronBaseAddress + 0x68));
        if(cauldronItem != 4 || Mod.GameInstance!.PrevInLevelSelect == true) // Only trigger on opening the Polyjuice Pot
        {
            return;
        }
        Console.WriteLine("Polyjuice Pot Closed");
        Memory.Instance.Write<byte>((nuint)(cauldronBaseAddress + 0x68), 0); // Reset cauldron item to 0
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
    }

    [Function([], FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void ChangeYears();
    private static unsafe void onChangeYears()
    {
        if(Mod.GameInstance!.MapID == 365 || Mod.GameInstance!.MapID == 371 
            || Mod.GameInstance!.MapID == 377 || Mod.GameInstance!.MapID == 381)
        {
            byte* menuCheatAddress = (byte*)(Mod.BaseAddress + 0xC575E0);
            char[] chars = new char[6];
            for (int i = 0; i < 6; i++)
            {
                int position = Memory.Instance.Read<byte>((nuint)(menuCheatAddress + i));
                if (position >= 0 && position <= 25)
                {
                    chars[i] = (char)('A' + position);
                }
                else if (position >= 26 && position <= 35)
                {
                    // Maps to 0-9
                    chars[i] = (char)('0' + (position - 26));
                }
                else
                {
                    chars[i] = '_'; // Unknown character
                }
            }
            string yearString = new string(chars);
            Console.WriteLine($"Year Requested is: {yearString}");
            switch (yearString)
            {
                case "YEAR05" when Mod.GameInstance!.LevelID != 1:
                    HubHandler.SwitchYears(5);
                    HubHandler.TurnOffCutscenes();
                    break;
                case "YEAR06" when Mod.GameInstance!.LevelID != 2:
                    HubHandler.SwitchYears(6);
                    HubHandler.TurnOffCutscenes();
                    break;
                case "YEAR07" when Mod.GameInstance!.LevelID != 3:
                    HubHandler.SwitchYears(7);
                    HubHandler.TurnOffCutscenes();
                    break;
                case "YEAR08" when Mod.GameInstance!.LevelID != 4:
                    HubHandler.SwitchYears(8);
                    HubHandler.TurnOffCutscenes();
                    break;
                default:
                    break;
            }
        } else {   
            Console.WriteLine("Please move to the Character Customization Room to change years.");
            return;
        }
    }

    private static void ResetItems()
    {
        HubHandler.ResetGoldBrickCount();
        HubHandler.ResetRedBrickUnlock();
        HubHandler.ResetSpells();
        LevelHandler.ResetLevels();
        CharacterHandler.ResetTokens();
        CharacterHandler.ResetUnlocks();
        HubHandler.ResetHub();
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
