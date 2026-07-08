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
    // This lock is used for MapID and PrevMapID because of background threads
    public readonly object MapLock = new();
    public int PrevLevelID { get; private set; } = -1;
    public int LevelID { get; private set; } = -1;
    public int PrevMapID { get; private set; } = -1;
    public int MapID { get; private set; } = -1;
    public int MapID2 { get; private set; } = -1; // This is used for ths shop text because it constantly prints and could cause deadlocks
    public int MapID3 { get; private set; } = -1; // This is used for ths shop text because it constantly prints and could cause deadlocks
    public bool PrevInShop { get; private set; } = false;
    public bool PrevInShop2 { get; private set; } = false; // This is used for ths shop text because it constantly prints and could cause deadlocks
    public bool PrevInLevelSelect { get; private set; } = false;
    public bool PrevInMenu { get; private set; } = false;
    public int CurrentP1CharID { get; private set; } = 0;
    public int CurrentP2CharID { get; private set; } = 0;
    private static readonly int[] LeakyMapIDs = [368, 374, 380, 386];
    private static readonly int[] JokeShopMapIDs = [369, 375, 383, 387];
    private static readonly int[] KnockturnMapIDs = [367, 373, 379, 385];
    private static readonly int[] MadamMalkinMapIDs = [366, 372, 378, 382];
    private static readonly int[] DuelingMapIDs = [44, 73, 137, 157, 207, 223, 309, 324];
    private static readonly int[] FinalLevelMapIDs = [351, 339, 334, 329, 318, 308, 246, 235, 227, 220, 217, 207, 165, 156, 152, 148, 143, 137, 82, 73, 65, 58, 51, 44];
    private static readonly string[] FastTravelRequests = ["Y5LOND", "Y6LOND", "Y7LOND", "Y8LOND", "Y5FOYE", "Y6FOYE", "Y7FOYE", "Y8FOYE", "Y5QUAD", "Y6QUAD", "Y7QUAD", "Y8QUAD"];
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
    public string PlayerName = "";

    // Helper Function to help print to the terminal and log file with a consistent prefix
    public static void PrintToLog(string message)
    {
        Mod.Logger!.WriteLineAsync("[LHP2.archipelago.mod] " + message);
    }

    // Used to check if the game menu is loaded before connecting and trying to set up hooks
    public static void IsGameLoaded()
    {
        PrintToLog("Checking to see if game is loaded");
        int rewriteNumber = 0;
        while (!IsMenuLoaded())
        {
            if (rewriteNumber % 10 == 0)
                PrintToLog("Waiting for menu to load");
            rewriteNumber++;
            System.Threading.Thread.Sleep(500);

        }
    }

    // Helper Function to check if menu is loaded or player is controllable
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
            PrintToLog($"Error in checking if menu is loaded: {ex.Message}");
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
            PrintToLog($"Error in InGame check: {ex.Message}");
            return false;
        }
    }

    // After Connecting, this function reads initial game variables and NOPs code that we don't want running
    public static void ModifyInstructions()
    {
        // Read initial game values upon connecting
        Mod.GameInstance!.LevelID = Memory.Instance.Read<int>(Mod.BaseAddress + 0xADDB7C);
        int mapID;
        mapID = Memory.Instance.Read<int>(Mod.BaseAddress + 0xC5B374);
        lock (Mod.GameInstance!.MapLock)
        {
            Mod.GameInstance!.MapID = mapID;
            Mod.GameInstance!.PrevMapID = mapID;
        }
        PrintToLog($"Initial mapID: {mapID}, Initial levelID: {Mod.GameInstance!.LevelID}");
        Mod.GameInstance!.PrevLevelID = Mod.GameInstance!.LevelID;

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
        // NOP Resetting the Hint Color to 2 when walking through a door
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3C7274, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        // NOP Changing the hint color
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3C73C4, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        // NOP Resetting the Hint Color to 2 when walking through a door
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3C73B9, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
        // NOP Resetting Hint message constantly if the pets are out
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3C732C, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);

        // NOP Jump past check of spell unlocks in Specs lesson - Animation 1
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3EECC, [0x90, 0x90]);
        // NOP Jump past check of spell unlocks in Specs lesson - Animation 2
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3EF6C, [0x90, 0x90]);
        // NOP Jump past check of spell unlocks in Specs lesson - If ability active
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x6C497, [0x90, 0x90]);

        Shops.SetShopPrices(Mod.LHP2_Archipelago!.SlotDataInstance!.CheaperShops);

        // NOP Code that checks to see if a cheat code has already been entered (duplicate codes)
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3A55F2, [0x90, 0x90]);

        // NOP Code that forces to Dark Times upon save reload
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x3CB61, [0x90, 0x90]);
        // Change Dark Times Map Constant
        HubHandler.InitializeGameMaps();
        HubHandler.UpdateDarkTimesMap();

        // NOP Code that checks to see if you are in Herm Bag Lesson
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x41F9E, [0x90, 0x90]);

        // NOP Code that writes spells to Minifig file
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x4400D, [0x90, 0x90, 0x90]); // mov static into minifig part 1
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x44010, [0x90, 0x90, 0x90]); // mov static into minifig part 2
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x43DD5, [0x90, 0x90, 0x90]); // clears out all spells except green part 1
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x43DD8, [0x90, 0x90, 0x90]); // clears out all spells except green part 2
        Memory.Instance.SafeWrite(Mod.BaseAddress + 0x45BD1, [0xFF]); // And code that messes with green spells turning off

        Shops.SetShopPointers();
        SpellHandler.LockBoxes();
    }

    // This function turns on the N0CUT5 Cheat Code so cutscenes don't show
    public static unsafe void WriteN0CUT5Flag()
    {
        int* cutsceneBaseAddress = (int*)(Mod.BaseAddress + 0xB06F2C);
        nuint ptr = (nuint)(*cutsceneBaseAddress + 0xA4);

        // Write N0CUT5 flag to game
        Memory.Instance.SafeWrite(ptr, [0x01]);
    }

    /* 
    This function blocks the code that checks if lesson has been completed while in the lesson
    thus allowing the player to return to diagon. Also prevents the game from showing that you completed the lesson
    Currently only used to prevent the game from constantly showing you unlocked apparition
    WARNING: DADA, Specs, Agua, & Reducto Lessons softlock if this is enabled during those lessons
    */
    // public static void LessonReturnToHubNOP()
    // {
    //     // Allows Return to Diagon Alley in Abilities Lessons (Thestral Forest) 
    //     Memory.Instance.SafeWrite(Mod.BaseAddress + 0x161D1, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
    //     Memory.Instance.SafeWrite(Mod.BaseAddress + 0x40F42, [0x90, 0x90, 0x90, 0x90, 0x90, 0x90]);
    //     // Allows Return to Diagon Alley in Spell Lessons (Diffindo)
    //     Memory.Instance.SafeWrite(Mod.BaseAddress + 0x33355A, [0x90, 0x90]);
    // }

    // Restores the code effects from the function above to original behavior.
    // public static void LessonRestoreReturnToHub()
    // {
    //     Memory.Instance.SafeWrite(Mod.BaseAddress + 0x161D1, [0x0F, 0x84, 0x2D, 0xFF, 0xFF, 0xFF]); // harry2.exe+161D1 - 0F84 2DFFFFFF
    //     Memory.Instance.SafeWrite(Mod.BaseAddress + 0x40F42, [0x0F, 0x84, 0xE3, 0x01, 0x00, 0x00]); // harry2.exe+40F42 - 0F84 E3010000        
    //     Memory.Instance.SafeWrite(Mod.BaseAddress + 0x33355A, [0x74, 0x03]); //harry2.exe+33355A - 74 03                
    // }

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
            case < 1000: // Handle Spells
                SpellHandler.UnlockSpell(ItemID - SpellPurchOffset, Mod.GameInstance!.CurrentP1CharID, Mod.GameInstance!.CurrentP2CharID);
                break;
            case 1000: // Handle Delum
                break;
            case < 1027: // Handle Spells
                SpellHandler.UnlockSpell(ItemID - SpellPurchOffset, Mod.GameInstance!.CurrentP1CharID, Mod.GameInstance!.CurrentP2CharID);
                break;
            default:
                PrintToLog($"Unknown item received: {ItemID}");
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
    private static IReverseWrapper<CheckSpecsUnlock> _reverseWrapOnCheckSpecsUnlock = default!;
    private static IReverseWrapper<CheckHermBagUnlock> _reverseWrapHermBagUnlock = default!;
    private static IReverseWrapper<CheckPolyjuiceUnlock> _reverseWrapOnCheckPolyjuiceUnlock = default!;
    private static IReverseWrapper<SetDuelingHealth> _reverseWrapOnSetDuelingHealth = default!;
    private static IReverseWrapper<ShopItemSelected> _reverseWrapOnShopItemSelected = default!;
    private static IReverseWrapper<CharacterShopItemSelected> _reverseWrapOnCharacterShopItemSelected = default!;
    private static IReverseWrapper<StudCollected> _reverseWrapOnStudCollected = default!;

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
        _asmHooks.Add(hooks.CreateAsmHook(updateLevelHook, (int)(Mod.BaseAddress + 0x35641B), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] updateMapIDHook =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnMapUpdate, out _reverseWrapOnMapUpdate)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(updateMapIDHook, (int)(Mod.BaseAddress + 0x356424), AsmHookBehaviour.ExecuteAfter).Activate());

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
            "push edi",
            "mov edx, ebp",
            "mov edi, esi",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnChangeCharacters, out _reverseWrapOnChangeCharacters)}",
            "popad",
            "popfd",
            "pop edi",
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
        _asmHooks.Add(hooks.CreateAsmHook(changeYearsHook, (int)(Mod.BaseAddress + 0x3A55F2), AsmHookBehaviour.ExecuteAfter).Activate());

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

        string[] checkSpecsUnlockHook =
        {
            // Push and Pop all Registers except for the ECX register since we need it in our function
            "use32",
            "pushfd",
            "push eax",
            "push ebx",
            "push edx",
            "push esi",
            "push edi",
            "push ebp",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnCheckSpecsUnlock, out _reverseWrapOnCheckSpecsUnlock)}",
            "pop ebp",
            "pop edi",
            "pop esi",
            "pop edx",
            "pop ebx",
            "pop eax",
            "popfd",
        };
        // Specs Animation 1
        _asmHooks.Add(hooks.CreateAsmHook(checkSpecsUnlockHook, (int)(Mod.BaseAddress + 0x3EEE0), AsmHookBehaviour.DoNotExecuteOriginal).Activate());
        // Specs Animation 2
        _asmHooks.Add(hooks.CreateAsmHook(checkSpecsUnlockHook, (int)(Mod.BaseAddress + 0x3EF80), AsmHookBehaviour.DoNotExecuteOriginal).Activate());
        // If Specs is usable of not
        _asmHooks.Add(hooks.CreateAsmHook(checkSpecsUnlockHook, (int)(Mod.BaseAddress + 0x6C4AB), AsmHookBehaviour.DoNotExecuteOriginal).Activate());

        string[] checkHermBagUnlockHook =
        {
            // Push and Pop all Registers except for the ECX register since we need it in our function
            "use32",
            "pushfd",
            "push eax",
            "push ebx",
            "push edx",
            "push esi",
            "push edi",
            "push ebp",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnCheckHermBagUnlock, out _reverseWrapHermBagUnlock)}",
            "pop ebp",
            "pop edi",
            "pop esi",
            "pop edx",
            "pop ebx",
            "pop eax",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(checkHermBagUnlockHook, (int)(Mod.BaseAddress + 0x41FB2), AsmHookBehaviour.ExecuteAfter).Activate());

        string[] checkPolyjuiceUnlockHook =
        {
            "use32",
            "pushfd",
            "push ebx",
            "push ecx",
            "push edx",
            "push esi",
            "push edi",
            "push ebp",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnCheckPolyjuiceUnlock, out _reverseWrapOnCheckPolyjuiceUnlock)}",
            "pop ebp",
            "pop edi",
            "pop esi",
            "pop edx",
            "pop ecx",
            "pop ebx",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(checkPolyjuiceUnlockHook, (int)(Mod.BaseAddress + 0x1BCC3), AsmHookBehaviour.ExecuteAfter).Activate());

        string[] startDuelHook =
        {
            // Push and Pop all Registers except for the ECX register since we need it in our function
            "use32",
            "pushfd",
            "push eax",
            "push ebx",
            "push edx",
            "push esi",
            "push edi",
            "push ebp",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnSetDuelingHealth, out _reverseWrapOnSetDuelingHealth)}",
            "pop ebp",
            "pop edi",
            "pop esi",
            "pop edx",
            "pop ebx",
            "pop eax",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(startDuelHook, (int)(Mod.BaseAddress + 0x8C75E), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] shopItemSelected =
        {
            "use32",
            "pushfd",
            "pushad",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnShopItemSelected, out _reverseWrapOnShopItemSelected)}",
            "popad",
            "popfd",
        };
        _asmHooks.Add(hooks.CreateAsmHook(shopItemSelected, (int)(Mod.BaseAddress + 0x792C3), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] characterShopItemSelected =
        {
            "use32",
            "push ebx",
            "mov ebx, esi",
            "push eax",
            "push ecx",
            "push edx",
            "push ebp",
            "push edi",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnCharacterShopItemSelected, out _reverseWrapOnCharacterShopItemSelected)}",
            "pop edi",
            "pop ebp",
            "pop edx",
            "pop ecx",
            "pop eax",
            "pop ebx",
        };
        _asmHooks.Add(hooks.CreateAsmHook(characterShopItemSelected, (int)(Mod.BaseAddress + 0x88C0B), AsmHookBehaviour.ExecuteFirst).Activate());

        string[] studCollected =
        {
            "use32",
            "push edi",
            "push ecx",
            "mov edi, esi",
            "mov ecx, ebp",
            "push ebx",
            "push eax",
            "push edx",
            "push ebp",
            $"{hooks.Utilities.GetAbsoluteCallMnemonics(OnStudCollected, out _reverseWrapOnStudCollected)}",
            "pop ebp",
            "pop edx",
            "pop eax",
            "pop ebx",
            "pop ecx",
            "pop edi"
        };
        _asmHooks.Add(hooks.CreateAsmHook(studCollected, (int)(Mod.BaseAddress + 0x3B0AEC), AsmHookBehaviour.ExecuteFirst).Activate());

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
            CheckWinCon();
            HubHandler.UpdateWinConText();
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
                    PrintToLog($"Unknown Crest Completed value: {value}. Please report to the devs.");
                    break;
            }
        }
    }

    [Function([FunctionAttribute.Register.ecx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void RedBrickPurchase(int ecx);
    private static void OnRedBrickPurchase(int ecx)
    {
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleRedBricks == 1)
        {
            PrintToLog("Red Brick Purchase detected but red brick purchases aren't shuffled, ignoring.");
            return;
        }
        CheckAndReportLocation(ecx + RedBrickPurchOffset);
    }

    [Function([FunctionAttribute.Register.ebx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void GoldBrickPurchase(int ebx);
    private static void OnGoldBrickPurchase(int ebx)
    {
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleGoldBrickPurchases < 1)
        {
            PrintToLog("Gold Brick Purchase detected but gold brick purchases aren't shuffled, ignoring.");
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
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        if (mapID == 369 || mapID == 375
            || mapID == 383 || mapID == 387 || mapID == 166)
        {
            PrintToLog($"Spell Unlock Function Ran: EDX is 0x{edx:X} and EAX is 0x{eax:X}");

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
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleCharacterTokens == 2)
        {
            PrintToLog($"Hub Character Token Collected but not shuffled. EAX: 0x{eax:X}, EDX: 0x{edx:X}, Map ID: {mapID}");
            return;
        }
        int itemID = CharacterHandler.GetHubTokenItemID(eax, edx);
        if (itemID == -1)
        {
            PrintToLog("Error getting Level Token Item ID");
            PrintToLog($"EAX is: 0x{eax:X}");
            PrintToLog($"EDX is: 0x{edx:X}");
            PrintToLog("Map ID is: " + mapID);
            return;
        }
        CheckAndReportLocation(itemID + tokenOffset);
    }

    [Function([FunctionAttribute.Register.ebx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void LevelCharacterCollected(int ebx);
    private static void OnLevelCharacterCollected(int ebx)
    {
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        int itemID = CharacterHandler.GetLevelTokenItemID(ebx);
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleCharacterTokens == 2)
        {
            PrintToLog($"Level Character Token Collected but not shuffled. Map ID: {mapID}");
            CharacterHandler.UnlockToken(itemID); // Unlock the token so the player can continue to progress even if they can't save and exit a level.
            return;
        }
        if (itemID == -1)
        {
            PrintToLog("Error getting Level Token Item ID");
            PrintToLog($"EBX is: 0x{ebx:X}");
            PrintToLog("Map ID is: " + mapID);
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
        int mapID;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInShop = Mod.GameInstance!.PrevInShop;
        }
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        if (MadamMalkinMapIDs.Contains(mapID) && prevInShop == true) //Make sure Player is in Robe Shop
        {
            if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleCharacterTokens == 1)
            {
                PrintToLog("Character Purchase detected but character purchases aren't shuffled, ignoring.");
                return;
            }
            int itemID = CharacterHandler.GetPurchaseCharacterID(ecx, eax);
            if (itemID == -1)
            {
                PrintToLog("Error getting Purchased Character ID");
                PrintToLog($"EAX is: {eax:X}");
                PrintToLog($"ECX is: {ecx:X}");
                PrintToLog("Map ID is: " + mapID);
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
            PrintToLog("Error getting SIP ID from Hub");
            PrintToLog($"EDX is: 0x {edx:X}");
            int lookupvalue = edx * 4 + 2;
            PrintToLog($"Lookup Value should be: 0x{lookupvalue:X}");
            int mapID;
            lock (Mod.GameInstance!.MapLock)
            {
                mapID = Mod.GameInstance!.MapID;
            }
            PrintToLog("Map ID is: " + mapID);
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
            PrintToLog("Error getting GB ID from Hub");
            PrintToLog($"EAX is: 0x{eax:X}");
            int lookupvalue = eax * 4 + 2;
            PrintToLog($"Lookup Value should be: 0x{lookupvalue:X}");
            int mapID;
            lock (Mod.GameInstance!.MapLock)
            {
                mapID = Mod.GameInstance!.MapID;
            }
            PrintToLog("Map ID is: " + mapID);
            return;
        }
        CheckAndReportLocation(itemID + HubGBOffset);

    }

    [Function([FunctionAttribute.Register.eax],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void HubRB(int eax);
    private static void OnHubRB(int eax)
    {
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleRedBricks == 2)
        {
            PrintToLog($"Hub RB Collected but not shuffled. Map ID: {mapID}");
            return;
        }

        int itemID = HubHandler.GetHubID(eax);
        if (itemID == -1)
        {
            PrintToLog("Error getting RB ID from Hub");
            PrintToLog($"EAX is: 0x{eax:X}");
            int lookupvalue = eax * 4 + 2;
            PrintToLog($"Lookup Value should be: 0x{lookupvalue:X}");
            PrintToLog("Map ID is: " + mapID);
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
    private static unsafe void OnLevelChange(int value)
    {
        if (Mod.GameInstance!.LevelID == value)
        {
            PrintToLog($"Level ID stayed the same, no update. Level ID: {value}");
            return;
        }
        Mod.GameInstance!.PrevLevelID = Mod.GameInstance!.LevelID;
        Mod.GameInstance!.LevelID = value;
        PrintToLog($"Level ID updated to {value}.");
        HubHandler.UpdateWinConText();
        if (value is >= 1 and <= 4)
        {
            HubHandler.ChangeLeakyLoadingZones(value);
            HubHandler.UpdateMissingMapConstants(value);
        }
        if (value >= 1 && value <= 4 && (Mod.GameInstance!.PrevLevelID == 0 || Mod.GameInstance!.PrevLevelID > 4))
        {
            // Clear out the studs to write to the total so that we don't duplicate stud count
            // ulong* studTotalAddress = *(ulong**)(Mod.BaseAddress + 0xC5B600);
            // ulong* inLevelP1StudAddress = (ulong*)(Mod.BaseAddress + 0xC53E88);
            // ulong* inLevelP2StudAddress = (ulong*)(Mod.BaseAddress + 0xC53EA0);
            ulong* p1StudValueToWriteAddress = (ulong*)(Mod.BaseAddress + 0xC5E408);
            ulong* p2StudValueToWriteAddress = (ulong*)(Mod.BaseAddress + 0xC5E410);
            // *studTotalAddress += *inLevelP1StudAddress + *inLevelP2StudAddress;
            // *inLevelP1StudAddress = 0;
            // *inLevelP2StudAddress = 0;
            *p1StudValueToWriteAddress = 0;
            *p2StudValueToWriteAddress = 0;
        }
    }

    [Function([FunctionAttribute.Register.ecx],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void UpdateMap(int value);
    private static unsafe void OnMapUpdate(int value)
    {
        int mapID = value;
        int prevMapID;
        lock (Mod.GameInstance!.MapLock)
        {
            prevMapID = Mod.GameInstance!.MapID; // using map ID here cause it is before the update
        }
        if (mapID == prevMapID)
        {
            HintSystem.AddInterruptedMessageToFront("This map isn't available in this year. Please time travel to access", 0);
        }
        lock (Mod.GameInstance!.MapLock)
        {
            Mod.GameInstance!.PrevMapID = Mod.GameInstance!.MapID;
            Mod.GameInstance!.MapID = value;
            Mod.GameInstance!.MapID2 = value;
            Mod.GameInstance!.MapID3 = value;
            prevMapID = Mod.GameInstance!.PrevMapID;
        }

        // When leaving Y7 London, ensure that Code is running as normal (disabled in Y7 London cause of apparition)
        // if (prevMapID == 103)
        // {
        //     LessonRestoreReturnToHub();
        // }

        // When leaving Leaky & staying in Hub, we want to verify what the London ID is still correct
        if (LeakyMapIDs.Contains(prevMapID) && Mod.GameInstance!.LevelID is >= 1 and <= 4)
        {
            HubHandler.VerifyLondonMapIDs();
            HubHandler.ChangeLeakyLoadingZones(Mod.GameInstance!.LevelID);
        }

        PrintToLog($"Map ID updated to {value}.");
        Mod.LHP2_Archipelago!.SendMapID(value);
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
        LevelHandler.ImplementMapLogic(value);

        // Load Red Bricks Enabled if Previous map was 402 (menu)
        if (prevMapID == 402)
        {
            HubHandler.LoadRedBricksEnabled();
            HubHandler.RestoreDarkTimesMap();
        }

        // Make it so upon returning to diagon, the wilderness code runs instead of having to enter the code
        if ((mapID == 376 || mapID == 370) && (prevMapID == 99 || prevMapID == 5))
        {
            HubHandler.AdjustWilderness();
        }

        // Send Polyjuice Potion Check
        if (mapID == 168 && prevMapID == 169)
        {
            CheckAndReportLocation(1000); // Polyjuice Potion is unlocked after drinking it for the first time in story
        }

        // If you enter the level in story and complete it, there have been instances where the individual will time travel. This code makes the game think you are in freeplay upon the final map of the level. We did it on the final map because there were bugs caused (specifical in dark times) where P2 would be a story character that you couldn't control.
        if (FinalLevelMapIDs.Contains(mapID))
        {
            byte* freeplayFlag = (byte*)(Mod.BaseAddress + 0xC5B5DC);
            *freeplayFlag = 1;
        }
    }

    [Function([FunctionAttribute.Register.edx, FunctionAttribute.Register.edi],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void ChangeCharacters(int edx, int edi);

    public static unsafe void OnChangeCharacters(int edx, int edi)
    {
        ushort* initialP1Value = (ushort*)(Mod.BaseAddress + 0xC5F4C4);
        ushort* initialP2Value = (ushort*)(Mod.BaseAddress + 0xC5F4D0);
        if (*initialP1Value == 0xFFFF)
        {
            PrintToLog($"P1 Character Changed, Spell Function ran, EDX: {edx:X}");
            Mod.GameInstance!.CurrentP1CharID = edx;
            SpellHandler.ResetSpells();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
            int currentMapID;
            lock (Mod.GameInstance!.MapLock)
            {
                currentMapID = Mod.GameInstance!.MapID;
            }
            SpellHandler.SpellMapLogic(currentMapID);
            SpellHandler.HandleSpellVisibility();
            return;
        }
        if (*initialP2Value == 0xFFFF)
        {
            PrintToLog($"P2 Character Changed, Spell Function ran, EDX: {edx:X}");
            Mod.GameInstance!.CurrentP2CharID = edx;
            SpellHandler.ResetSpells();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
            int currentMapID;
            lock (Mod.GameInstance!.MapLock)
            {
                currentMapID = Mod.GameInstance!.MapID;
            }
            SpellHandler.SpellMapLogic(currentMapID);
            SpellHandler.HandleSpellVisibility();
            return;
        }
    }

    [Function([FunctionAttribute.Register.eax, FunctionAttribute.Register.esp],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate void OpenCloseShop(int eax, int esp);
    private static void OnShopChange(int eax, int esp)
    {
        bool eaxBit0Set = (eax & 1) != 0;
        int lastNibble = esp & 0xF;
        int ShopMapID;
        lock (Mod.GameInstance!.MapLock)
        {
            ShopMapID = Mod.GameInstance!.MapID;
        }

        if (eaxBit0Set && lastNibble == 0x08)
        {
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInLevelSelect = true;
            }
            PrintToLog("Level Selector Opened");
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, MaxItemID);
            HubHandler.RestoreLeakyLoadingZones();
            HubHandler.UpdateWinConText();
        }

        if (eaxBit0Set && lastNibble == 0x0C)
        {
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInShop = true;
                Mod.GameInstance!.PrevInShop2 = true;
            }
            PrintToLog("Shop Opened");
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(tokenOffset, levelOffset - 25);
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(0, tokenOffset - 1);
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(RedBrickCollectOffset, RedBrickPurchOffset - 17);
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(RedBrickPurchOffset, 1026);
            HubHandler.UpdateWinConText();

            // Joke Shop prices are set when save is loaded. So we handle that by changing it upon opening and closing that shop
            if (JokeShopMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleJokeSpells == 1)
            {
                Shops.SetJokeShopPrices(Mod.LHP2_Archipelago!.SlotDataInstance!.CheaperShops);
                Shops.UpdateJokeShopPointers();
            }

            if (KnockturnMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleGoldBrickPurchases == 1)
            {
                Shops.UpdateGoldBrickPointer();
            }

            if (LeakyMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleRedBricks != 1)
            {
                Shops.UpdateRedBrickPointers();
            }

            if (MadamMalkinMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleCharacterTokens != 1)
            {
                Shops.UpdateCharacterPointers();
            }
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
                PrintToLog("Level Selector Closed");

                // Game enters a level before thinking you are out of shop, so if we stay in hub, resetting items/locations here
                if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
                {
                    ResetItems();
                    Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
                    Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
                    HubHandler.ChangeLeakyLoadingZones(Mod.GameInstance!.LevelID);
                    HubHandler.UpdateWinConText();
                }
            }
            else if (!eaxBit0Set && prevInShop)
            {
                lock (Mod.GameInstance!.StateLock)
                {
                    Mod.GameInstance!.PrevInShop = false;
                    Mod.GameInstance!.PrevInShop2 = false;
                }
                PrintToLog("Shop Selector Closed");

                // Game enters a level before thinking you are out of shop, so if we stay in hub, resetting levels here
                if (Mod.GameInstance!.LevelID >= 1 && Mod.GameInstance!.LevelID <= 4)
                {
                    ResetItems();
                    SpellHandler.ResetSpells();
                    Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
                    Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
                    HubHandler.UpdateWinConText();
                }

                // Joke Shop prices are set when save is loaded. So we handle that by changing it upon opening and closing that shop
                if (JokeShopMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleJokeSpells == 1)
                {
                    Shops.ReverseJokeShopPriceChanges(Mod.LHP2_Archipelago!.SlotDataInstance!.CheaperShops);
                    Shops.ResetJokeShopPointers();
                }

                if (KnockturnMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleGoldBrickPurchases == 1)
                {
                    Shops.ResetGoldBrickPointer();
                }

                if (LeakyMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleRedBricks != 1)
                {
                    Shops.ResetRedBrickPointers();
                }

                if (MadamMalkinMapIDs.Contains(ShopMapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleCharacterTokens != 1)
                {
                    Shops.ResetCharacterPointers();
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
        byte[] bytes = [24, 31, 11, 14, 13, 3]; // Y5LOND
        for (int i = 0; i < 6; i++)
        {
            Memory.Instance.Write((nuint)(menuCheatAddress + i), bytes[i]);
        }

        // If in level, want to sync to locations except for Red bricks & Spells
        if (Mod.GameInstance!.LevelID < 1 || Mod.GameInstance!.LevelID > 4)
        {
            PrintToLog("Menu Opened");
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInMenu = true;
            }
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnLocations(0, RedBrickPurchOffset - 1);
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(RedBrickPurchOffset, MaxItemID);
            HubHandler.UpdateWinConText();

            if (Mod.GameInstance!.LevelID == 16)
            {
                HubHandler.FixReturnToLeakyCauldron();
            }

            if (Mod.GameInstance!.LevelID == 27) // Flaw in the Plan
            {
                if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 0)
                {
                    LevelHandler.LockLevel(LevelHandler.LevelData.TheFlawInThePlan);
                }
            }
        }
        else // In Hub, want to show all items
        {
            PrintToLog("Menu Opened");
            lock (Mod.GameInstance!.StateLock)
            {
                Mod.GameInstance!.PrevInMenu = true;
            }
            ResetItems();
            Mod.LHP2_Archipelago!.UpdateBasedOnItems(0, MaxItemID);
            HubHandler.UpdateGoldBrickCount();
            HubHandler.UpdateWinConText();
            SpellHandler.UnlockAllPassiveSpells();
        }
    }

    [Function([], FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate bool CharacterCmp();

    private static bool OnCharacterCmp()
    {
        // Only run CMP on menu map or levels 1–4
        lock (Mod.GameInstance!.MapLock)
        {
            if (Mod.GameInstance!.MapID == 402)
                return false;
        }

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
        int MapID;
        lock (Mod.GameInstance!.StateLock)
        {
            prevInMenu = Mod.GameInstance!.PrevInMenu;
        }
        lock (Mod.GameInstance!.MapLock)
        {
            MapID = Mod.GameInstance!.MapID;
        }
        if (!prevInMenu || MapID == 402 || edi != 1) // Only trigger when in menu, not on main menu, and when menu level goes back to 1
        {
            return;
        }
        PrintToLog($"Reduce Menu Count Triggered. PrevInMenu: {prevInMenu}, MapID: {MapID}, EDI: {edi}");
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
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        PrintToLog("Menu Closed");
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
        HubHandler.UpdateWinConText();
        LevelHandler.ImplementMapLogic(mapID);
        SpellHandler.SpellMapLogic(mapID);
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
        PrintToLog("Polyjuice Pot Opened");
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
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        if (cauldronItem != 4 || prevInLevelSelect == true) // Only trigger on opening the Polyjuice Pot
        {
            return;
        }
        PrintToLog("Polyjuice Pot Closed");
        Memory.Instance.SafeWrite((nuint)(cauldronBaseAddress + 0x68), [0x00]); // Reset cauldron item selected to 0
        ResetItems();
        Mod.LHP2_Archipelago!.UpdateBasedOnLocations(tokenOffset, SpellPurchOffset - 1);
        Mod.LHP2_Archipelago!.UpdateBasedOnItems(SpellPurchOffset, MaxItemID);
        LevelHandler.ImplementMapLogic(mapID);
        SpellHandler.SpellMapLogic(mapID);
    }

    [Function(CallingConventions.Fastcall)]
    public delegate void ChangeYears();
    private static unsafe void OnChangeYears()
    {
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }
        // Only run in the Character Customization Room
        if (LeakyMapIDs.Contains(mapID))
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
            string mapString = new(chars);
            PrintToLog($"Map Requested is: {mapString}");

            if (FastTravelRequests.Contains(mapString))
            {
                HubHandler.FastTravel(mapString);
            }
            else
            {
                PrintToLog($"Unknown Map Requested: {mapString}.");
            }

        }
        else
        {
            PrintToLog("Please move to the Leaky Cualdron.");
            return;
        }
    }

    [Function(CallingConventions.Fastcall)]
    private delegate void HandleInterruptedMessage();
    private static void OnHandleInterruptedMessage()
    {
        HintSystem.HandleInterruptedMessage();
    }

    [Function([],
    FunctionAttribute.Register.ecx, FunctionAttribute.StackCleanup.Callee)]
    private delegate int CheckSpecsUnlock();
    private static int OnCheckSpecsUnlock()
    {
        return SpellHandler.CheckSpecsUnlock();
    }

    [Function([],
    FunctionAttribute.Register.ecx, FunctionAttribute.StackCleanup.Callee)]
    private delegate int CheckHermBagUnlock();
    private static int OnCheckHermBagUnlock()
    {
        return SpellHandler.CheckHermBagUnlock();
    }

    [Function([FunctionAttribute.Register.eax],
    FunctionAttribute.Register.eax, FunctionAttribute.StackCleanup.Callee)]
    public delegate int CheckPolyjuiceUnlock(int eax);
    private static int OnCheckPolyjuiceUnlock(int eax)
    {
        int polyjuiceArray = SpellHandler.CheckPolyjuiceUnlock(eax);
        return polyjuiceArray;
    }

    [Function([FunctionAttribute.Register.ecx],
    FunctionAttribute.Register.ecx, FunctionAttribute.StackCleanup.Callee)]
    public delegate nuint SetDuelingHealth(nuint ecx);

    private static nuint OnSetDuelingHealth(nuint ecx)
    {
        int mapID;
        lock (Mod.GameInstance!.MapLock)
        {
            mapID = Mod.GameInstance!.MapID;
        }

        if (DuelingMapIDs.Contains(mapID) && Mod.LHP2_Archipelago!.SlotDataInstance!.FasterDuels == 1)
        {
            PrintToLog("Dueling Health Set to 1");
            ecx = (ecx & ~0xFFu) | 1u;
        }
        return ecx;
    }

    [Function([FunctionAttribute.Register.edx],
    FunctionAttribute.Register.edx, FunctionAttribute.StackCleanup.Callee)]
    public delegate void ShopItemSelected(int edx);
    private static void OnShopItemSelected(int edx)
    {
        if (!Mod.GameInstance!.PrevInShop2)
        {
            return;
        }

        if (JokeShopMapIDs.Contains(Mod.GameInstance!.MapID2) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleJokeSpells == 1)
        {
            Shops.HandleShopText(edx + SpellPurchOffset + 1); // Adding 1 to account for the first spell not being used
            return;
        }

        if (KnockturnMapIDs.Contains(Mod.GameInstance!.MapID2) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleGoldBrickPurchases == 1)
        {
            Shops.HandleShopText(edx + GoldBrickPurchOffset);
            return;
        }

        if (LeakyMapIDs.Contains(Mod.GameInstance!.MapID2) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleRedBricks != 1)
        {
            Shops.HandleShopText(edx + RedBrickPurchOffset);
            return;
        }
    }

    [Function([FunctionAttribute.Register.ebx],
    FunctionAttribute.Register.ebx, FunctionAttribute.StackCleanup.Callee)]
    public delegate void CharacterShopItemSelected(int edx);
    private static void OnCharacterShopItemSelected(int edx)
    {
        if (MadamMalkinMapIDs.Contains(Mod.GameInstance!.MapID3) && Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleCharacterTokens != 1)
        {
            int item = CharacterHandler.GetLevelTokenItemID(edx);
            if (item == -1)
            {
                return;
            }
            Shops.HandleShopText(item);
        }
    }

    [Function([FunctionAttribute.Register.edi, FunctionAttribute.Register.ecx],
    FunctionAttribute.Register.edi, FunctionAttribute.StackCleanup.Callee)]
    // edi is the stud value picked up and ebp is the address it is being written to
    public delegate void StudCollected(nuint edi, nuint ecx);
    private static unsafe void OnStudCollected(nuint edi, nuint ecx)
    {
        nuint* studTotalAddress = *(nuint**)(Mod.BaseAddress + 0xC5B600);
        nuint* inLevelP1StudAddress = (nuint*)(Mod.BaseAddress + 0xC53E88);
        nuint* inLevelP2StudAddress = (nuint*)(Mod.BaseAddress + 0xC53EA0);
        // PrintToLog($"Stud Total Address: 0x{(nuint)studTotalAddress:X}");
        // PrintToLog($"inLevelP1StudAddress: 0x{(nuint)inLevelP1StudAddress:X}");
        // PrintToLog($"inLevelP2StudAddress: 0x{(nuint)inLevelP2StudAddress:X}");
        // PrintToLog($"edi: 0x{edi:X}");
        // PrintToLog($"ecx: 0x{ecx:X}");

        if (ecx == (nuint)inLevelP1StudAddress || ecx == (nuint)inLevelP2StudAddress)
        {
            *studTotalAddress += edi;
        }

        return;
    }

    private static void ResetItems()
    {
        HubHandler.ResetGoldBrickCount();
        HubHandler.ResetRedBrickUnlock();
        SpellHandler.ResetSpells();
        LevelHandler.ResetLevels();
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleCharacterTokens != 2)
        {
            CharacterHandler.ResetTokens();
        }
        CharacterHandler.ResetUnlocks();
        HubHandler.ResetHub();
    }

    public static void CheckAndReportLocation(int apID)
    {
        if (Mod.LHP2_Archipelago!.IsLocationChecked(apID))
        {
            PrintToLog($"Location for AP ID: {apID} already checked");
            return;
        }
        PrintToLog($"Checking location for AP ID: {apID}");
        Mod.LHP2_Archipelago!.CheckLocation(apID);
    }

    public static void CheckWinCon()
    {
        // Defeat Voldemort
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 0 && Mod.GameInstance!.PrevLevelID == 27)
        {
            int horcruxesReceived = Mod.LHP2_Archipelago!.CountItemsCheckedInRange(440, 446);
            int requiredHorcruxes = Mod.LHP2_Archipelago!.SlotDataInstance!.NumberOfRequiredHorcruxes;
            if (requiredHorcruxes == -1)
            {
                PrintToLog("Can't Determine if the game is completed, Horcrux slot data not available.");
                return;
            }
            PrintToLog($"Player Has Received {horcruxesReceived} Horcruxes");
            if (horcruxesReceived >= requiredHorcruxes)
            {
                Mod.LHP2_Archipelago!.Release();
            }
            return;
        }

        // Levels Beaten
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 2)
        {
            int levelsCompleted = Mod.LHP2_Archipelago!.CountLocationsCheckedInRange(450, 473);
            int requiredLevels = Mod.LHP2_Archipelago!.SlotDataInstance!.NumberOfRequiredLevels;
            if (requiredLevels == -1)
            {
                PrintToLog("Can't Determine if the game is completed, Level slot data not available.");
                return;
            }
            PrintToLog($"Player Has Completed {levelsCompleted} Levels");
            if (levelsCompleted >= requiredLevels)
            {
                Mod.LHP2_Archipelago!.Release();
            }
            return;
        }
    }
}
