using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using System.Numerics;

namespace LHP2_Archi_Mod;

public class Game
{
    /*
    Main class that handles all of the game hooks that we implement, variables we watch, & send/receive items.
    Current implementation of the LHP2 Archipelago has the player syncs up to the received items and/or checked
    locations based on what the player is doing (i.e. in a shop, in the hub, in a level, etc.). Most of this is
    managed with game hooks. Generally we only sync up on state changes (i.e. map update, menu opened, shop opened, etc.) 
    unless it is a priority item like spells.
    */

    // This lock is used for PrevInShop, PrevInLevelSelect, PrevInMenu because of the Hint System
    public readonly object StateLock = new();
    public int PrevLevelID { get; private set; } = -1;
    public int LevelID { get; private set; } = -1;
    public int PrevMapID { get; private set; } = -1;
    public int MapID { get; private set; } = -1;
    public bool PrevInShop { get; private set; } = false;
    public bool PrevInLevelSelect { get; private set; } = false;
    public bool PrevInMenu { get; private set; } = false;
    public int CurrentCharID { get; private set; } = 0;
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
    public const int MaxItemID = 1030;

    // Used to check if the game menu is loaded before connecting and trying to set up hooks
    public static void IsGameLoaded()
    {
        Mod.Logger!.WriteLineAsync("Checking to see if game is loaded");
        int rewriteNumber = 0;
        while (!IsMenuLoaded())
        {
            if (rewriteNumber % 10 == 0)
                Mod.Logger!.WriteLineAsync("Waiting for menu to load");
            rewriteNumber++;
            System.Threading.Thread.Sleep(500);

        }
    }

    // Helper Function to check if menu is loaded 
    // TODO: Currently broken out cause we previously checked to see if save file was loaded as part of setup
    public static unsafe bool IsMenuLoaded()
    {
        try
        {
            if (Mod.GameInstance!.LevelID > 0)
                return true;
            byte** basePtr = (byte**)(Mod.BaseAddress + 0xC4ED1C);
            if (basePtr == null || *basePtr == null)
                return false;

            byte* finalPtr = *basePtr + 0x190;
            if (finalPtr == null)
                return false;

            return *finalPtr != 0;
        }
        catch (Exception ex)
        {
            Mod.Logger!.WriteLineAsync($"Error in checking if menu is loaded: {ex.Message}");
            return false;
        }
    }

    public static unsafe bool IsPlayerControllable()
    {
        try
        {
            byte** basePtr = (byte**)(Mod.BaseAddress + 0xC5763C);
            if (basePtr == null || *basePtr == null)
                return false;

            byte* finalPtr = *basePtr + 0x119C;
            if (finalPtr == null)
                return false;

            return *finalPtr == 1;
        }
        catch (Exception ex)
        {
            Mod.Logger!.WriteLineAsync($"Error in InGame check: {ex.Message}");
            return false;
        }
    }

    // After Connecting, this function reads initial game variables and NOPs code that we don't want running
    public static void ModifyInstructions()
    {
        // Read initial game values upon connecting
        Mod.GameInstance!.LevelID = Memory.Instance.Read<int>(Mod.BaseAddress + 0xADDB7C);
        Mod.GameInstance!.MapID = Memory.Instance.Read<int>(Mod.BaseAddress + 0xC5B374);
        Mod.GameInstance!.PrevLevelID = Mod.GameInstance!.LevelID;
        Mod.GameInstance!.PrevMapID = Mod.GameInstance!.MapID;
        Mod.Logger!.WriteLineAsync($"Initial Level ID: {Mod.GameInstance!.LevelID}, Map ID: {Mod.GameInstance!.MapID}");

        WriteN0CUT5Flag();

        // NOP Gold Brick Amount Corrector #1
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x332694, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        // NOP Gold Brick Amount Corrector #2
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB8B, [0x90, 0x90, 0x90]);
        // NOP Gold Brick Amount Corrector #3 (Only runs when 200+)
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x42EB90, [0x90, 0x90, 0x90]);

        // Removes the games check to see if player is in Freeplay mode and forces the game to 
        // always checking individual level completion to enable save and exit
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x40A264, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);

        // Unlock Current Level Story
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B817E, [0x90, 0x90, 0x90, 0x90, 0x90]);
        // // Unlock Current Level Freeplay
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B8165, [0x90, 0x90, 0x90, 0x90, 0x90]);
        //NOP Unlock Next Story Level
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4B809C, [0x90, 0x90, 0x90, 0x90, 0x90]);

        // NOP Hint System
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3C733D, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        // NOP Call to Hint System that doesn't clear old value
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x43D212, [0x90, 0x90, 0x90, 0x90, 0x90]);

        ShopPrices.SetShopPrices(Mod.LHP2_Archipelago!.SlotDataInstance!.CheaperShops);
    }

    // This function turns on the N0CUT5 Cheat Code so cutscenes don't show
    public static unsafe void WriteN0CUT5Flag()
    {
        int* cutsceneBaseAddress = (int*)(Mod.BaseAddress + 0xB06F2C);
        nuint ptr = (nuint)(*cutsceneBaseAddress + 0xA4);

        // Write N0CUT5 flag to game
        Memory.Instance.Write(ptr, (byte)0x01);
    }

    /* 
    This function blocks the code that checks if lesson has been completed while in the lesson
    thus allowing the player to return to diagon. Also prevents the game from showing that you completed the lesson
    Currently only used to prevent the game from constantly showing you unlocked apparition
    WARNING: DADA, Specs, Agua, & Reducto Lessons softlock if this is enabled during those lessons
    */
    public static void LessonReturnToHubNOP()
    {
        // Allows Return to Diagon Alley in Abilities Lessons (Thestral Forest) 
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x161D1, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x40F42, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        // Allows Return to Diagon Alley in Spell Lessons (Diffindo)
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x33355A, [0x90, 0x90]);
    }

    // Restores the code effects from the function above to original behavior.
    public static void LessonRestoreReturnToHub()
    {
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x161D1, [0x0F, 0x84, 0x2D, 0xFF, 0xFF, 0xFF]); // harry2.exe+161D1 - 0F84 2DFFFFFF
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x40F42, [0x0F, 0x84, 0xE3, 0x01, 0x00, 0x00]); // harry2.exe+40F42 - 0F84 E3010000        
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x33355A, [0x74, 0x03]); //harry2.exe+33355A - 74 03                
    }

    // Main function that handles updating the game state to match items or locations.
    public static void ManageItem(int ItemID)
    {

        LevelHandler.LevelData level;

        switch (ItemID)
        {
            case < 213: // Handle Character
                CharacterHandler.UnlockCharacter(ItemID);
                break;
            case < 426: // Handle Token
                int token = ItemID - tokenOffset;
                CharacterHandler.UnlockToken(token);
                break;
            case < 450: // Handle Horcrux
                break;
            case < 475: // Handle Level Unlock
                level = LevelHandler.ConvertIDToLeveData(ItemID - levelOffset);
                LevelHandler.UnlockLevel(level);
                break;
            case < 499: // Handle In Level Student in Peril
                level = LevelHandler.ConvertIDToLeveData(ItemID - SIPOffset);
                LevelHandler.UnlockStudentInPeril(level);
                break;
            case < 550: // Handle Hub SIP
                HubHandler.UnlockHubSIP(ItemID - HubSIPOffset);
                break;
            case < 574: // Handle Gryffindor Crests
                level = LevelHandler.ConvertIDToLeveData(ItemID - GryfCrestOffset);
                LevelHandler.UnlockGryffindorCrest(level);
                break;
            case < 598: // Handle Slytherin Crests
                level = LevelHandler.ConvertIDToLeveData(ItemID - SlythCrestOffset);
                LevelHandler.UnlockSlytherinCrest(level);
                break;
            case < 622: // Handle Ravenclaw Crests
                level = LevelHandler.ConvertIDToLeveData(ItemID - RavenCrestOffset);
                LevelHandler.UnlockRavenclawCrest(level);
                break;
            case < 646: // Handle Hufflepuff Crests
                level = LevelHandler.ConvertIDToLeveData(ItemID - HuffleCrestOffset);
                LevelHandler.UnlockHufflepuffCrest(level);
                break;
            case < 699: // Handle True Wizards
                level = LevelHandler.ConvertIDToLeveData(ItemID - TrueWizardOffset);
                LevelHandler.UnlockTrueWizard(level);
                break;
            case < 700: // Purple Stud - see APHandler ItemReceived for implementation
                break;
            case < 900: // Handle Gold Brick
                HubHandler.ReceivedGoldBrick(ItemID);
                break;
            case < 935: // Handle Red Brick Collected/Purchasable
                HubHandler.UnlockHubRB(ItemID - RedBrickCollectOffset);
                break;
            case < 975: // Handle Red Brick Purchase
                HubHandler.ReceivedRedBrickUnlock(ItemID - RedBrickPurchOffset);
                break;
            case < 1027: // Handle Spells
                SpellHandler.UnlockSpell(ItemID - SpellPurchOffset, Mod.GameInstance!.CurrentCharID);
                break;
            default:
                Mod.Logger!.WriteLineAsync($"Unknown item received: {ItemID}");
                break;
        }
    }

    /* 
    Dark Times is level 0, Hub is levels 1-4. This is a helper function to conver level ID to AP ID.
    upon check completion. The final status screens are considered part of leaky cauldron map ID so 
    if Map ID is 1-4, we evaluate the previous level ID instead of the current one.
    */
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

    // Reverse wrappers for our hooks
    public static List<IAsmHook> _asmHooks = [];
    private static IReverseWrapper<LevelComplete> _reverseWrapOnLevelComplete = default!;
    private static IReverseWrapper<LevelSIPComplete> _reverseWrapOnLevelSIP = default!;
    private static IReverseWrapper<TrueWizardComplete> _reverseWrapOnTrueWizard = default!;
    private static IReverseWrapper<CrestsComplete> _reverseWrapOnCrests = default!;
    private static IReverseWrapper<RedBrickPurchase> _reverseWrapOnRedBrickPurch = default!;
    private static IReverseWrapper<GoldBrickPurchase> _reverseWrapOnGoldBrickPurch = default!;
    private static IReverseWrapper<SpellUnlock> _reverseWrapOnSpellUnlock = default!;
    private static IReverseWrapper<HubCharacterCollected> _reverseWrapOnHubCharacterCollected = default!;
    private static IReverseWrapper<LevelCharacterCollected> _reverseWrapOnLevelCharacterCollected = default!;
    private static IReverseWrapper<CharacterPurchased> _reverseWrapOnCharacterPurchased = default!;
    private static IReverseWrapper<HubSIP> _reverseWrapOnHubSIP = default!;
    private static IReverseWrapper<HubGB> _reverseWrapOnHubGB = default!;
    private static IReverseWrapper<HubRB> _reverseWrapOnHubRB = default!;
    private static IReverseWrapper<HubGhostPath> _reverseWrapOnHubGhostPath = default!;
    private static IReverseWrapper<UpdateLevel> _reverseWrapOnLevelUpdate = default!;
    private static IReverseWrapper<UpdateMap> _reverseWrapOnMapUpdate = default!;
    private static IReverseWrapper<OpenCloseShop> _reverseWrapOnShopUpdate = default!;
    private static IReverseWrapper<OpenMenu> _reverseWrapOnOpenMenu = default!;
    private static IReverseWrapper<ReduceMenuCount> _reverseWrapOnReduceMenuCount = default!;
    private static IReverseWrapper<CloseMenu> _reverseWrapOnCloseMenu = default!;
    private static IReverseWrapper<CharacterCmp> _reverseWrapOnCharacterCmp = default!;
    private static IReverseWrapper<OpenPolyjuicePot> _reverseWrapOnOpenPolyjuicePot = default!;
    private static IReverseWrapper<ClosePolyjuicePot> _reverseWrapOnClosePolyjuicePot = default!;
    private static IReverseWrapper<ChangeCharacters> _reverseWrapOnChangeCharacters = default!;
    private static IReverseWrapper<ChangeYears> _reverseWrapChangeYears = default!;
    private static IReverseWrapper<HandleInterruptedMessage> _reverseWrapOnHandleInterruptedMessage = default!;

    // Modifying the associated assembly of our game to call our functions
    // TODO: Future proof this from game updates by implementing signature scanning
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

        string[] completeTrueWizardHook =
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

        string[] purchaseRedBrickHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnRedBrickPurchase, out _reverseWrapOnRedBrickPurch)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(purchaseRedBrickHook, (int)(Mod.BaseAddress + 0x8928), AsmHookBehaviour.ExecuteAfter).Activate());

        string[] purchaseGoldBrickHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnGoldBrickPurchase, out _reverseWrapOnGoldBrickPurch)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(purchaseGoldBrickHook, (int)(Mod.BaseAddress + 0x9039), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] spellUnlockHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnSpellUnlock, out _reverseWrapOnSpellUnlock)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(spellUnlockHook, (int)(Mod.BaseAddress + 0x33358B), AsmHookBehaviour.ExecuteFirst).Activate());

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

        string[] levelCharacterCollectedHook =
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

        string[] rescueHubSIPHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubSIP, out _reverseWrapOnHubSIP)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(rescueHubSIPHook, (int)(Mod.BaseAddress + 0x313BF1), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] collectHubGBHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubGB, out _reverseWrapOnHubGB)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(collectHubGBHook, (int)(Mod.BaseAddress + 0x3137EF), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] collectHubRBHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubRB, out _reverseWrapOnHubRB)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(collectHubRBHook, (int)(Mod.BaseAddress + 0x71E92), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] handleHubGhostPathUpdatesHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHubGhostPath, out _reverseWrapOnHubGhostPath)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(handleHubGhostPathUpdatesHook, (int)(Mod.BaseAddress + 0x37CE63), AsmHookBehaviour.DoNotExecuteOriginal).Activate());

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
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnShopChange, out _reverseWrapOnShopUpdate)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(openCloseShopHook, (int)(Mod.BaseAddress + 0x417074), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] openMenuHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnOpenMenu, out _reverseWrapOnOpenMenu)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(openMenuHook, (int)(Mod.BaseAddress + 0x212145), AsmHookBehaviour.ExecuteFirst).Activate());

        // The game's vanilla behavior is to check if the character is purchased before allowing the player to switch to them in freeplay.
        // This hook adjusts that behavior so it doesn't check in levels, but does check in hub.
        string[] characterCmpHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnCharacterCmp, out _reverseWrapOnCharacterCmp)}",
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
        _asmHooks.Add(hooks.CreateAsmHook(characterCmpHook, (int)(Mod.BaseAddress + 0x41890C), AsmHookBehaviour.DoNotExecuteOriginal).Activate());

        string[] reduceMenuCountHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnReduceMenuCount, out _reverseWrapOnReduceMenuCount)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(reduceMenuCountHook, (int)(Mod.BaseAddress + 0x212244), AsmHookBehaviour.ExecuteAfter).Activate());

        string[] closeMenuHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnCloseMenu, out _reverseWrapOnCloseMenu)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(closeMenuHook, (int)(Mod.BaseAddress + 0x218347), AsmHookBehaviour.ExecuteAfter).Activate());

        string[] openPolyjuicePotHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnOpenPolyjuicePot, out _reverseWrapOnOpenPolyjuicePot)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(openPolyjuicePotHook, (int)(Mod.BaseAddress + 0x3C69A0), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] closePolyjuicePotHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnClosePolyjuicePot, out _reverseWrapOnClosePolyjuicePot)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(closePolyjuicePotHook, (int)(Mod.BaseAddress + 0x3C69B0), AsmHookBehaviour.ExecuteFirst).Activate());

        // Handles Spell logic when you change charactesr
        string[] changeCharactersHook =
        {
            "use32",
            "push edx",
            "mov edx, ebp",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnChangeCharacters, out _reverseWrapOnChangeCharacters)}",
            "popad",
            "popfd",
            "pop edx",
        };
        _asmHooks.Add(hooks.CreateAsmHook(changeCharactersHook, (int)(Mod.BaseAddress + 0x5440EC), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] changeYearsHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnChangeYears, out _reverseWrapChangeYears)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(changeYearsHook, (int)(Mod.BaseAddress + 0x3A584B), AsmHookBehaviour.ExecuteAfter).Activate());

        string[] handleInterruptedMessageHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnHandleInterruptedMessage, out _reverseWrapOnHandleInterruptedMessage)}",
            "popad",
            "popfd",
        };
        // Normal Zero Out Hint Game Code
        _asmHooks.Add(hooks.CreateAsmHook(handleInterruptedMessageHook, (int)(Mod.BaseAddress + 0x3C77E7), AsmHookBehaviour.ExecuteFirst).Activate());
        // Walking through Loading Zone Zero Out Hint Game Code
        _asmHooks.Add(hooks.CreateAsmHook(handleInterruptedMessageHook, (int)(Mod.BaseAddress + 0x3C727B), AsmHookBehaviour.ExecuteFirst).Activate());

    }

    [Function(CallingConventions.Fastcall)]
    public delegate void LevelComplete();

    private static void OnLevelComplete()
    {
        int level = Mod.GameInstance!.LevelID;
        int prevLevel = Mod.GameInstance!.PrevLevelID;

        int? apID = GetApID(level, prevLevel);
        if (apID is int id)
        {
            CheckAndReportLocation(id + levelOffset);
            if (id == 12)
            {
                CheckAndReportLocation(1026); // Apparition is unlocked at the end of 7 Harrys
            }
            if (id == 23) // The Flaw in the Plan - Check win con
            {
                CheckWinCon();
            }
        }
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
                    Mod.Logger!.WriteLineAsync($"Unknown Crest Completed value: {value}. Please report to the devs.");
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
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleGoldBrickPurchases < 1)
        {
            Mod.Logger!.WriteLineAsync("Gold Brick Purchase detected but gold brick purchases aren't shuffled, ignoring.");
            return;
        }
        int itemId = BitOperations.TrailingZeroCount(ebx);
        CheckAndReportLocation(itemId + GoldBrickPurchOffset);

    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.edx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void SpellUnlock(int eax, int edx);
    private static void OnSpellUnlock(int eax, int edx)
    {
        // Only run if in Joke Shop or in 7 Harrys Delum/Bag lesson
        if (Mod.GameInstance!.MapID == 369 || Mod.GameInstance!.MapID == 375
            || Mod.GameInstance!.MapID == 383 || Mod.GameInstance!.MapID == 387 || Mod.GameInstance!.MapID == 166)
        {
            Mod.Logger!.WriteLineAsync($"Spell Unlock Function Ran: EDX is 0x{edx:X} and EAX is 0x{eax:X}");

            if (edx == 0x80000) // Handles Herm Bag Unlock
            {
                CheckAndReportLocation(1025);
                return;
            }

            int itemId = BitOperations.TrailingZeroCount(eax);
            itemId += SpellPurchOffset;

            if (itemId <= 975 || (itemId > 994 && itemId != 1001)) // 1001 is to handle Delum Lesson, rest are joke spells
            {
                return; // Ignore non purchased spells that are unlocked
            }

            if (itemId < 995 && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleJokeSpells < 1)
            {
                return; // Ignore joke spells if they aren't shuffled
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
            Mod.Logger!.WriteLineAsync("Error getting Level Token Item ID");
            Mod.Logger!.WriteLineAsync($"EAX is: 0x{eax:X}");
            Mod.Logger!.WriteLineAsync($"EDX is: 0x{edx:X}");
            Mod.Logger!.WriteLineAsync("Map ID is: " + Mod.GameInstance!.MapID);
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
            Mod.Logger!.WriteLineAsync("Error getting Level Token Item ID");
            Mod.Logger!.WriteLineAsync($"EBX is: 0x{ebx:X}");
            Mod.Logger!.WriteLineAsync("Map ID is: " + Mod.GameInstance!.MapID);
            return;
        }
        CheckAndReportLocation(itemID + tokenOffset);
    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.ecx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void CharacterPurchased(IntPtr ecx, int eax);
    private static void OnCharacterPurchased(IntPtr ecx, int eax)
    {
        bool prevInShop;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInShop = Mod.GameInstance!.PrevInShop;
        }
        if ((Mod.GameInstance!.MapID == 366 || Mod.GameInstance!.MapID == 372
            || Mod.GameInstance!.MapID == 378 || Mod.GameInstance!.MapID == 382) && prevInShop == true) //Make sure Player is in Robe Shop
        {
            int itemID = CharacterHandler.GetPurchaseCharacterID(ecx, eax);
            if (itemID == -1)
            {
                Mod.Logger!.WriteLineAsync("Error getting Purchased Character ID");
                Mod.Logger!.WriteLineAsync($"EAX is: {eax:X}");
                Mod.Logger!.WriteLineAsync($"ECX is: {ecx:X}");
                Mod.Logger!.WriteLineAsync("Map ID is: " + Mod.GameInstance!.MapID);
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
        if (itemID == -1)
        {
            Mod.Logger!.WriteLineAsync("Error getting SIP ID from Hub");
            Mod.Logger!.WriteLineAsync($"EDX is: 0x {edx:X}");
            int lookupvalue = edx * 4 + 2;
            Mod.Logger!.WriteLineAsync($"Lookup Value should be: 0x{lookupvalue:X}");
            Mod.Logger!.WriteLineAsync("Map ID is: " + Mod.GameInstance!.MapID);
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
        if (itemID == -1)
        {
            Mod.Logger!.WriteLineAsync("Error getting GB ID from Hub");
            Mod.Logger!.WriteLineAsync($"EAX is: 0x{eax:X}");
            int lookupvalue = eax * 4 + 2;
            Mod.Logger!.WriteLineAsync($"Lookup Value should be: 0x{lookupvalue:X}");
            Mod.Logger!.WriteLineAsync("Map ID is: " + Mod.GameInstance!.MapID);
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
        if (itemID == -1)
        {
            Mod.Logger!.WriteLineAsync("Error getting RB ID from Hub");
            Mod.Logger!.WriteLineAsync($"EAX is: 0x{eax:X}");
            int lookupvalue = eax * 4 + 2;
            Mod.Logger!.WriteLineAsync($"Lookup Value should be: 0x{lookupvalue:X}");
            Mod.Logger!.WriteLineAsync("Map ID is: " + Mod.GameInstance!.MapID);
            return;
        }
        CheckAndReportLocation(itemID + RedBrickCollectOffset);

    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.edx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void HubGhostPath(int eax, int edx);
    private static void OnHubGhostPath(int eax, int edx)
    {
        HubHandler.HandleGhostPaths(eax, edx);
    }

    [Function([FunctionAttribute.Register.eax],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateLevel(int value);
    private static void OnLevelChange(int value)
    {
        Mod.GameInstance!.PrevLevelID = Mod.GameInstance!.LevelID;
        Mod.GameInstance!.LevelID = value;
        Mod.Logger!.WriteLineAsync($"Level ID updated to {value}.");
    }

    [Function([FunctionAttribute.Register.ecx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateMap(int value);
    private static void OnMapChange(int value)
    {
        Mod.GameInstance!.PrevMapID = Mod.GameInstance!.MapID;
        Mod.GameInstance!.MapID = value;

        // When leaving Y7 London, ensure that Code is running as normal (disabled in Y7 London cause of apparition)
        if (Mod.GameInstance!.PrevMapID == 104 && !Mod.LHP2_Archipelago!.IsLocationChecked(1027))
        {
            LessonRestoreReturnToHub();
        }
        Mod.Logger!.WriteLineAsync($"Map ID updated to {value}.");
        Mod.LHP2_Archipelago!.SendMapID(value);
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
        HubHandler.UpdateHorcruxCount();
        LevelHandler.ImplementMapLogic(value);

        // Load Red Bricks Enabled if Previous map was 402 (menu)
        if (Mod.GameInstance!.PrevMapID == 402)
        {
            HubHandler.LoadRedBricksEnabled();
        }
    }

    [Function([FunctionAttribute.Register.edx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void ChangeCharacters(int edx);

    public static unsafe void OnChangeCharacters(int edx)
    {
        ushort* initialValue = (ushort*)(Mod.BaseAddress + 0xC5F4C4);
        if (*initialValue == 0xFFFF)
        {
            Mod.Logger!.WriteLineAsync($"Character Changed, Spell Function ran, EDX: {edx:X}");
            Mod.GameInstance!.CurrentCharID = edx;
            SpellHandler.ResetSpells();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
            SpellHandler.SpellMapLogic(Mod.GameInstance!.MapID);
            SpellHandler.HandleSpellVisibility();
        }
    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.esp],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenCloseShop(int eax, int esp);
    private static void OnShopChange(int eax, int esp)
    {
        bool eaxBit0Set = (eax & 1) != 0;
        int lastNibble = esp & 0xF;

        if (eaxBit0Set && lastNibble == 0x08)
        {
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInLevelSelect = true;
            }
            Mod.Logger!.WriteLineAsync("Level Selector Opened");
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, MaxItemID);
            HubHandler.UpdateHorcruxCount();
        }

        if (eaxBit0Set && lastNibble == 0x0C)
        {
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInShop = true;
            }
            Mod.Logger!.WriteLineAsync("Shop Opened");
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(tokenOffset, levelOffset - 25);
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(0, tokenOffset - 1);
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(RedBrickCollectOffset, RedBrickPurchOffset - 17);
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(RedBrickPurchOffset, 1026);
            HubHandler.UpdateHorcruxCount();
        }
        else
        {
            bool prevInLevelSelect, prevInShop;
            lock (Mod.GameInstance!.StateLock)
            {
                prevInLevelSelect = Mod.GameInstance!.PrevInLevelSelect;
                prevInShop = Mod.GameInstance!.PrevInShop;
            }

            if (!eaxBit0Set && prevInLevelSelect)
            {
                lock (Mod.GameInstance!.StateLock)
                {
                    Mod.GameInstance!.PrevInLevelSelect = false;
                }
                Mod.Logger!.WriteLineAsync("Level Selector Closed");

                // Game enters a level before thinking you are out of shop, so if we stay in hub, resetting items/locations here
                if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
                {
                    ResetItems();
                    Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
                    Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
                    HubHandler.UpdateHorcruxCount();
                }
            }
            else if (!eaxBit0Set && prevInShop)
            {
                lock (Mod.GameInstance!.StateLock)
                {
                    Mod.GameInstance!.PrevInShop = false;
                }
                Mod.Logger!.WriteLineAsync("Shop Selector Closed");

                // Game enters a level before thinking you are out of shop, so if we stay in hub, resetting levels here
                if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
                {
                    ResetItems();
                    SpellHandler.ResetSpells();
                    Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
                    Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
                    HubHandler.UpdateHorcruxCount();
                }
            }
        }
    }

    // Picking up a collectable in hub triggers menu code too. edi was always 2 when pausing and EBP was always 6 when pausing.
    [Function([FunctionAttribute.Register.edi],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenMenu(int edi);
    private static unsafe void OnOpenMenu(int edi)
    {
        if (edi != 2)
        {
            return;
        }

        // Take into account that menu opens when selecting freeplay/story.
        bool prevInShop;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInShop = Mod.GameInstance!.PrevInShop;
        }

        if (prevInShop == true)
        {
            return;
        }

        // Quality of life update so that players can more quickly change years
        byte* menuCheatAddress = (byte*)(Mod.BaseAddress + 0xC575E0);
        byte[] bytes = [24, 4, 0, 17, 26, 26];
        for (int i = 0; i < 6; i++)
        {
            Memory.Instance.Write((nuint)(menuCheatAddress + i), bytes[i]);
        }

        // If in level, want to sync to locations except for Red bricks & Spells
        if (Mod.GameInstance!.LevelID < 1 || Mod.GameInstance!.LevelID > 4)
        {
            Mod.Logger!.WriteLineAsync("Menu Opened");
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInMenu = true;
            }
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(0, RedBrickPurchOffset - 1);
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(RedBrickPurchOffset, MaxItemID);
            HubHandler.UpdateHorcruxCount();
        }
        else // In Hub, want to show all items
        {
            Mod.Logger!.WriteLineAsync("Menu Opened");
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInMenu = true;
            }
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, MaxItemID);
            HubHandler.GetGoldBrickCount();
            HubHandler.UpdateHorcruxCount();
            SpellHandler.UnlockAllPassiveSpells();
        }
    }

    [Function([], FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate bool CharacterCmp();

    private static bool OnCharacterCmp()
    {
        // Only run CMP on menu map or levels 1–4
        if (Mod.GameInstance!.MapID == 402)
            return false;

        if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
            return false;

        return true;   // skip CMP everywhere else
    }

    [Function([FunctionAttribute.Register.edi],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void ReduceMenuCount(int edi);
    private static void OnReduceMenuCount(int edi)
    {
        bool prevInMenu;
        int prevMapID;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInMenu = Mod.GameInstance!.PrevInMenu;
            prevMapID = Mod.GameInstance!.MapID;
        }
        Console.WriteLine($"Reduce Menu Count Triggered. PrevInMenu: {prevInMenu}, PrevMapID: {prevMapID}, EDI: {edi}");
        if (!prevInMenu || prevMapID == 402 || edi != 1) // Only trigger when in menu, not on main menu, and when menu level goes back to 1
        {
            return;
        }
        HubHandler.SaveRedBricksEnabled();
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void CloseMenu();
    private static void OnCloseMenu()
    {
        // Take into account that this code runs multiple times.
        bool prevInMenu;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInMenu = Mod.GameInstance!.PrevInMenu;
        }

        if (!prevInMenu)
        {
            return;
        }

        lock (Mod.GameInstance!.StateLock)
        {
            Mod.GameInstance!.PrevInMenu = false;
        }
        Mod.Logger!.WriteLineAsync("Menu Closed");
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
        HubHandler.UpdateHorcruxCount();
        LevelHandler.ImplementMapLogic(Mod.GameInstance!.MapID);
        SpellHandler.SpellMapLogic(Mod.GameInstance!.MapID);
        HubHandler.SaveRedBricksEnabled();
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void OpenPolyjuicePot();
    private static unsafe void OnOpenPolyjuicePot()
    {
        byte* cauldronBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC54290);
        nuint cauldronItem = Memory.Instance.Read<nuint>((nuint)(cauldronBaseAddress + 0x68));
        bool prevInLevelSelect;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInLevelSelect = Mod.GameInstance!.PrevInLevelSelect;
        }
        if (cauldronItem != 4 || prevInLevelSelect == true) // Only trigger on opening the Polyjuice Pot
        {
            return;
        }
        Mod.Logger!.WriteLineAsync("Polyjuice Pot Opened");
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, tokenOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void ClosePolyjuicePot();
    private static unsafe void OnClosePolyjuicePot()
    {
        byte* cauldronBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC54290);
        nuint cauldronItem = Memory.Instance.Read<nuint>((nuint)(cauldronBaseAddress + 0x68));
        bool prevInLevelSelect;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInLevelSelect = Mod.GameInstance!.PrevInLevelSelect;
        }
        if (cauldronItem != 4 || prevInLevelSelect == true) // Only trigger on opening the Polyjuice Pot
        {
            return;
        }
        Mod.Logger!.WriteLineAsync("Polyjuice Pot Closed");
        Memory.Instance.Write<byte>((nuint)(cauldronBaseAddress + 0x68), 0); // Reset cauldron item selected to 0
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
        LevelHandler.ImplementMapLogic(Mod.GameInstance!.MapID);
        SpellHandler.SpellMapLogic(Mod.GameInstance!.MapID);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void ChangeYears();
    private static unsafe void OnChangeYears()
    {
        // Only run in the Character Customization Room
        if (Mod.GameInstance!.MapID == 365 || Mod.GameInstance!.MapID == 371
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
            string yearString = new(chars);
            Mod.Logger!.WriteLineAsync($"Year Requested is: {yearString}");

            switch (yearString)
            {
                case "YEAR05" when Mod.GameInstance!.LevelID != 1:
                    HubHandler.SwitchYears(5);
                    HubHandler.AdjustHubMaps(5);
                    break;
                case "YEAR06" when Mod.GameInstance!.LevelID != 2:
                    HubHandler.SwitchYears(6);
                    HubHandler.AdjustHubMaps(6);
                    break;
                case "YEAR07" when Mod.GameInstance!.LevelID != 3:
                    HubHandler.SwitchYears(7);
                    HubHandler.AdjustHubMaps(7);
                    break;
                case "YEAR08" when Mod.GameInstance!.LevelID != 4:
                    HubHandler.SwitchYears(8);
                    HubHandler.AdjustHubMaps(8);
                    break;
                default:
                    break;
            }
        }
        else
        {
            Mod.Logger!.WriteLineAsync("Please move to the Character Customization Room to change years.");
            return;
        }
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void HandleInterruptedMessage();
    private static void OnHandleInterruptedMessage()
    {
        HintSystem.HandleInterruptedMessage();
    }

    private static void ResetItems()
    {
        HubHandler.ResetGoldBrickCount();
        HubHandler.ResetRedBrickUnlock();
        SpellHandler.ResetSpells();
        LevelHandler.ResetLevels();
        CharacterHandler.ResetTokens();
        CharacterHandler.ResetUnlocks();
        HubHandler.ResetHub();
    }

    public static void CheckAndReportLocation(int apID)
    {
        if (Mod.LHP2_Archipelago!.IsLocationChecked(apID))
        {
            Mod.Logger!.WriteLineAsync($"Location for AP ID: {apID} already checked");
            return;
        }
        Mod.Logger!.WriteLineAsync($"Checking location for AP ID: {apID}");
        Mod.LHP2_Archipelago!.CheckLocation(apID);
    }

    public static void CheckWinCon()
    {
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 0)
        {
            int horcruxesReceived = Mod.LHP2_Archipelago!.CountItemsCheckedInRange(440, 446);
            int requiredHorcruxes = Mod.LHP2_Archipelago!.SlotDataInstance!.NumberOfRequiredHorcruxes;
            if (requiredHorcruxes == -1)
            {
                Mod.Logger!.WriteLineAsync("Can't Determine if the game is completed, Horcrux slot data not available.");
                return;
            }
            Mod.Logger!.WriteLineAsync($"Player Has Received {horcruxesReceived} Horcruxes");
            if (horcruxesReceived >= requiredHorcruxes)
            {
                Mod.LHP2_Archipelago!.Release();
            }
        }
    }
}
