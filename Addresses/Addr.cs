namespace LHP2_Archi_Mod.Addresses;

// Game addresses per version: the mod's own values on 32-bit, signatures on 64-bit (0 = unknown)
internal static class Addr
{
    private static bool Is64 => GameVersion.Is64;
    private static nuint V(uint off32, string sig) => Is64 ? Addr64.Get(sig) : Mod.BaseAddress + off32;
    private static nuint V(uint off32, string sig, uint extra64) => Is64 ? Plus(Addr64.Get(sig), extra64) : Mod.BaseAddress + off32;
    private static nuint Only32(uint off32) => Is64 ? 0 : Mod.BaseAddress + off32;
    private static nuint Plus(nuint a, uint extra) => a == 0 ? 0 : a + extra;

    // ---------------------------------------------------------------- game state
    public static nuint MapID => V(0xC5B374, "MapID");
    public static nuint LevelID => V(0xADDB7C, "LevelID");
    public static nuint MainMenuPtr => V(0xC4ED1C, "MainMenu");
    public static nuint PlayerControllablePtr => V(0xC5763C, "CharListBuffer1");
    public static nuint FreeplayFlag => V(0xC5B5DC, "FreeplayFlag");
    public static nuint CheatBasePtr => V(0xB06F2C, "CheatBase");
    public static nuint N0CUT5String => V(0x8968A4, "N0CUT5String");
    public static nuint CheatInput => V(0xC575E0, "CheatInput");
    public static nuint CheatCodeCallback => V(0xC575F8, "CheatCodeCallback"); // unknown-code hook
    public static nuint CauldronPtr => V(0xC54290, "Cauldron");
    public static nuint MenuIdGlobal => Is64 ? Addr64.Get("MenuIdGlobal") : 0; // 64-bit only

    // "Character being changed" value (0xFFFF) of the P1 / P2 holders
    public static nuint CharChangeP1 => V(0xC5F4C4, "CharHolderP1", 0x10);
    public static nuint CharChangeP2 => V(0xC5F4D0, "CharHolderP1", 0x18 + 0x10);

    // ---------------------------------------------------------------- studs
    public static nuint StudValueToWriteP1 => V(0xC5E408, "StudValuesP1");
    public static nuint StudValueToWriteP2 => V(0xC5E410, "StudValuesP1", 8);
    public static nuint InLevelStudsP1 => V(0xC53E88, "InLevelStudsP1");
    public static nuint InLevelStudsP2 => V(0xC53EA0, "InLevelStudsP1", 0x18);
    public static nuint StudTotalPtr => V(0xC5B600, "StudTotalPtr");

    // ---------------------------------------------------------------- characters / players
    public static nuint Player1Ptr => V(0xC53930, "Player1");
    public static nuint Player2Ptr => V(0xC53934, "Player2");
    // Player 2 opening the Polyjuice pot; 64-bit: the pot loops over Player1/Player2
    public static nuint CauldronPlayer2Ptr => Is64 ? Addr64.Get("Player2") : Mod.BaseAddress + 0xC5D68C;

    // ---------------------------------------------------------------- death link (PlayerHandler)
    public static nuint WorldObjectPtr => V(0xC5E358, "WorldObject"); // stud spawner argument
    public static nuint DuelCameraPtr => V(0xC4ECB0, "DuelCamera");
    // Game functions called by the mod (cdecl on 32-bit, x64 convention on 64-bit)
    public static nuint LoseStudsFunction => V(0x312DC0, "Fn_LoseStuds");
    public static nuint StudDropSpawnerFunction => V(0x318420, "Fn_StudDropSpawner");
    public static nuint CharacterSaveBasePtr => V(0xC546E4, "CharacterSaveBase");
    public static nuint CharacterTableBasePtr => V(0xB06ED0, "CharacterTextBase");

    // ---------------------------------------------------------------- levels / hub
    public static nuint LevelBasePtr => V(0xC55F2C, "LevelBase");
    public static nuint HubBasePtr => V(0xC5B3B4, "HubBase");
    public static nuint GoldBrickBasePtr => V(0xC54554, "GoldBrickBase");
    public static nuint RedBrickPurchasedPtr => V(0xC575F4, "RedBrickPurchased");
    public static nuint MapFlagsBasePtr => V(0xC5D5F4, "MapFlags"); // *_TECH sections
    // First subscriber of an Event object: [Event+4/+8] -> node, [node+4/+8] -> LegoGameThing
    public static nuint HogwartsWarpBasePtr => V(0xC4EE5C, "WarpEvent");
    public static nuint MapY5Ptr => V(0xB06914, "MapY5");
    public static nuint MapY6Ptr => V(0xB06918, "MapY6");
    public static nuint MapY7Ptr => V(0xB0691C, "MapY7");
    public static nuint MapY8Ptr => V(0xB06920, "MapY8");
    public static nuint MapDarkTimesPtr => V(0xB069AC, "MapDarkTimes");
    public static nuint MapY5DiagonPtr => V(0xB06A30, "MapY5Diagon");

    // ---------------------------------------------------------------- spells
    public static nuint SpellBase => V(0xB06AB0, "SpellBase");
    public static nuint SpellVisibility => V(0xB067C4, "SpellVisibility");
    public static nuint SpellSelectionPtr => V(0xC55968, "SpellSelection");
    public static nuint LessonVariable => V(0x94E4D4, "LessonVariable");

    // ---------------------------------------------------------------- hints / texts
    public static nuint HintTimer => V(0xC5839C, "HintTimer");
    public static nuint HintPtr => V(0xC5838C, "HintPtr");
    public static nuint HintColor => V(0xC58391, "HintColor");
    public static nuint MessageBlockPtr => V(0xC58388, "MessageBlock");
    public static nuint HintTextBasePtr => Only32(0xB16324); // 64-bit: see HintSystem
    public static nuint TextTablePtr => V(0xAE6E58, "TextTable");
    public static nuint NewGameTextPtr => Only32(0xB16750); // 64-bit: MenuNewGameOwner
    public static nuint MenuNewGameOwner => Is64 ? Addr64.Get("MenuNewGameOwner") : 0; // main menu
    public static nuint ScreenEmptyByte => V(0xAD98D9, "ScreenState", 1);
    // Hub cutscene: a byte on 32-bit, an 8-byte pointer on 64-bit
    public static nuint HubCutsceneByte => V(0xC5B224, "HubCutscene");

    // ---------------------------------------------------------------- shops
    public static nuint GoldBrickShopPrice => V(0x94B13C, "GoldBrickShopPrice");
    public static nuint RedBrickShopPrices => V(0x94CEE4, "RedBrickShopTable", 0x8);
    public static nuint RedBrickEnabled => V(0x94CEF3, "RedBrickShopTable", 0x17);
    public static nuint JokeShopTable => V(0x94E514, "JokeShopTable"); // 19 entries
    public static nuint SingleSlotCharacterPricesPtr => Only32(0xADBF90); // 64-bit: PricesA/B
    public static nuint MultiSlotCharacterPricesPtr => Only32(0xAE157C);
    // 64-bit: one table of 223 prices (0x20-byte entries), held by two identical objects
    public static nuint CharacterPricesA => Is64 ? Addr64.Get("CharacterPricesA") : 0;
    public static nuint CharacterPricesB => Is64 ? Addr64.Get("CharacterPricesB") : 0;
    public static nuint CharacterPricesC => Is64 ? Addr64.Get("CharacterPricesC") : 0; // shop grid

    // Text pointers of the 24 red bricks in the Leaky Cauldron shop (32-bit addresses)
    private static readonly uint[] RedBrickTextPointers32 =
    [
        0xAD9DB4, 0xAD9DA4, 0xAD9984, 0xAD9D44, 0xAD9D94, 0xAD9A74, 0xAD9D24, 0xAD9AC4, 0xAD9BB4, 0xAD9BF4, 0xAD9C24, 0xAD9C54,
        0xAD9C84, 0xAD9AA4, 0xAD9C04, 0xAD9CF4, 0xAD9BA4, 0xAD9A34, 0xAD9A44, 0xAD9A54, 0xAD9D64, 0xAD9D34, 0xAD9B74, 0xAD9C94,
    ];

    // 64-bit: the table's 0x10-byte entries become 0x18 bytes, text pointer at +8
    public static nuint RedBrickTextPointer(int index)
    {
        uint a32 = RedBrickTextPointers32[index];
        if (!Is64)
            return Mod.BaseAddress + a32;
        nuint entryAD9980 = Addr64.Get("TextEntryAD9980");
        if (entryAD9980 == 0)
            return 0;
        nuint tableBase = entryAD9980 - 0xC0;              // entry of +AD9900
        uint entry32 = a32 - 4;
        return tableBase + (entry32 - 0xAD9900) * 3 / 2 + 8;
    }
}

// Internal offsets and structure strides per game version (-1 = unknown on 64-bit)
internal static class Off
{
    private static bool Is64 => GameVersion.Is64;
    private static int V(int off32, int off64) => Is64 ? off64 : off32;

    public static int PtrSize => IntPtr.Size;
    public static int MenuLoadedFlag => V(0x190, -1); // 64-bit: see Game.IsMenuLoaded
    public static int PlayerControllableFlag => V(0x119C, 0x150C);
    public static int N0CUT5Flag => V(0xA4, 0x140);
    public static int CauldronSelectedItem => V(0x68, 0x2C); // 4 = Polyjuice
    // 64-bit: deduced from player 1 (+4, as on 32-bit), to confirm in game
    public static int CauldronSelectedItemP2 => V(0x6C, 0x30);

    // Player structure used by PlayerHandler; the 64-bit shifts aren't uniform
    public static int PlayerControlFlags => V(0x4D, 0x65);
    public static int PlayerDespawnedState => V(0x54, 0x6C);
    public static int PlayerDeathValue => V(0x55, 0x6D);
    public static int PlayerEffectTimer => V(0x504, 0x648);
    public static int PlayerStateEffect => V(0x54C, 0x6B0);
    public static int PlayerStateAnimation => V(0x558, 0x6BC);
    public static int PlayerMaxHealth => V(0xF0C, 0x1230);
    public static int PlayerCurrentHealth => V(0xF0D, 0x1231);
    public static int PlayerStudDropPosition => V(0xFCC, 0x133C);
    public static int PlayerStudDropFloat => V(0x1168, 0x14D8);
    public static int PlayerInvulnerabilityTimer => V(0x11B8, 0x1528);
    public static int DuelCameraState => V(0x1FD1, 0x29C1); // 1 during a duel
    public static int MessagePtrValue => V(0xFFC, 0x1740); // message #93
    public static int MessageRecordText => V(0x4, 0x8);
    public static int WinConMessageText => V(0xDF0, -1); // message #81
    public static int ShopText => V(0x274, -1);
    // Text pointer table: 8-byte entries and 12 more entries before these ones on 64-bit
    public static int PauseTextSlot => V(0xE5 * 4, 0xF1 * 8);
    public static int GoldBrickShopTextSlot => V(0xE0 * 4, 0xEC * 8);
    public static int NewGameText => V(0xC, -1);

    public static int RedBrickShopStride => V(0x18, 0x20);

    // Character table [CharacterTableBasePtr]: count, pointer array, fields of an entry
    public static int CharacterTableCount => V(0x30, 0x58);
    public static int CharacterTableArray => V(0x64, 0xA8);
    public static int CharacterEntryText => V(0x0C, 0x18);
    public static int CharacterEntryId => V(0x04, 0x08);
    public static int CharacterEntrySpells => V(0x58, 0x80);

    public static int GoldBrickCount => V(0x1A7A, 0x1A82);

    // LegoGameThing (entrance, map) pairs: return to the Leaky Cauldron, then warp to Hogwarts
    public static int WarpReturnMap => V(0x2FC, 0x6E0);
    public static int WarpHogwartsEntrance => V(0x31C, 0x700);

    // Mod storage in unused save header bytes ([StudTotalPtr]), same layout on both versions
    public static int PurpleStudCount => V(0x30, 0x30);
    public static int RedBrickSaveFile => V(0x34, 0x34);

    // Map constants: 0xA0-byte records, field +0x64 (64-bit: 0x118 bytes, field +0xC0)
    public static int MapRecordSize => V(0xA0, 0x118);
    public static int MapRecordConstant => V(0x64, 0xC0);

    // Spells: selection and active shooting spell
    public static int SpellSelectedP1 => V(0x18, 0x2C); // P2 at +4
    public static int ActiveShootingSpell => V(0xED3, 0x11EB); // shooting spell; spell image at +1
}
