using System.Reflection.Metadata;
using System.Text;

namespace LHP2_Archi_Mod;

/// <summary>
/// Handles hub-related game state management, including collectibles, time travel, and map adjustments.
/// </summary>
public class HubHandler
{
    public static unsafe byte* HubBaseAddress => *(byte**)(Mod.BaseAddress + 0xC5B3B4);
    private static unsafe byte* GoldBrickBaseAddress => *(byte**)(Mod.BaseAddress + 0xC54554);
    private static unsafe byte* RedBrickPurchBaseAddress => *(byte**)(Mod.BaseAddress + 0xC575F4);
    private static unsafe long* StudTotalBaseAddress => *(long**)(Mod.BaseAddress + 0xC5B600);
    private static unsafe byte* PurpleCountAddress => (byte*)StudTotalBaseAddress + 0x30;
    private static unsafe byte* RedBrickSaveFileAddress => PurpleCountAddress + 0x04;
    private static unsafe byte* RedBrickEnabledAddress => (byte*)(Mod.BaseAddress + 0x94CEF3);
    public static unsafe byte* GhostPathBaseAddress => *(byte**)(Mod.BaseAddress + 0xC55F2C);
    private static unsafe byte* MapFlagsBaseAddress => *(byte**)(Mod.BaseAddress + 0xC5D5F4);
    private static unsafe byte* HogwartWarpEntranceBaseAddress => *(byte**)(Mod.BaseAddress + 0x00C4EE5C);
    private static unsafe byte* SecondPointerWarp => *(byte**)(HogwartWarpEntranceBaseAddress + 0x04);

    // These null addresses don't have a fixed pointer (the saved data is a unordered collection). We handle this by doing a byte search over the collection.
    private static unsafe byte* leaky2LondonAddress = null;
    private static unsafe byte* hogsPathAddress = null;
    private static unsafe byte* wildernessAddress = null;
    private static unsafe byte* quadAddress = null;
    private static unsafe byte* hogsStatAddress = null;
    private static unsafe byte* classLobbyAddress = null;
    private static unsafe byte* kingsCrossAddress = null;
    private static unsafe byte* foyerAddress = null;

    // These are the bit flags that handle the different collectibles stored in the same memory address.
    [Flags]
    public enum BitMask
    {
        None = 0,
        SpecialFlag = 1 << 0,
        Entered = 1 << 2,
        RedBrick = 1 << 4,
        GoldBrick = 1 << 5,
        StudentInPeril = 1 << 6,
    }

    // This is a Dictionary that holds all Maps respective memory offset and as well as the Archi ID Offset - from start of the item/location table (i.e. tent will always be Archi ID+0, wilderness will always be Archi ID+1, etc.)
    private static readonly Dictionary<int, int> hubOffsets = new()
    {
        {0x32, 0}, // Y8 Tent
        {0x49A, 0}, // Y7 Tent
        {0x3E, 1}, // Y8 Wilderness
        {0x4A6, 1}, // Y7 Wilderness
        {0x4A, 2}, // Y8 Cafe
        {0x4B2, 2}, // Y7 Cafe
        {0x62, 3}, // Y8 London
        {0x4D6, 3}, // Y7 London
        {0x81E, 3}, // Y6 London
        {0xCF2, 3}, // Y5 London
        {0x6E, 4}, // Y8 King's Cross
        {0x4E2, 4}, // Y7 King's Cross
        {0x82A, 4}, // Y6 King's Cross
        {0xCFE, 4}, // Y5 King's Cross
        {0x86, 5}, // Y8 Hogsmeade Path
        {0x4FA, 5}, // Y7 Hogsmeade Path
        {0x842, 5}, // Y6 Hogsmeade Path
        {0x92, 6}, // Y8 Hogsmeade
        {0x506, 6}, // Y7 Hogsmeade
        {0x84E, 6}, // Y6 Hogsmeade
        {0x9E, 7}, // Y8 Hogsmeade Station
        {0x512, 7}, // Y7 Hogsmeade Station
        {0x866, 7}, // Y6 Hogsmeade Station
        {0xD22, 7}, // Y5 Hogsmeade Station
        {0xAA, 8}, // Y8 Hogwarts Path
        {0x51E, 8}, // Y7 Hogwarts Path
        {0x872, 8}, // Y6 Hogwarts Path
        {0xD3A, 8}, // Y5 Hogwarts Path
        {0xB6, 9}, // Y8 Herbology Greenhouse
        {0x52A, 9}, // Y7 Herbology Greenhouse
        {0x87E, 9}, // Y6 Herbology Greenhouse
        {0xD46, 9}, // Y5 Herbology Greenhouse
        {0xC2, 10}, // Y8 Training Grounds
        {0x536, 10}, // Y7 Training Grounds
        {0x88A, 10}, // Y6 Training Grounds
        {0xD52, 10}, // Y5 Training Grounds
        {0xCE, 11}, // Y8 Courtyard
        {0x542, 11}, // Y7 Courtyard
        {0x8A2, 11}, // Y6 Courtyard
        {0xD5E, 11}, // Y5 Courtyard
        {0xDA, 12}, // Y8 RoR Corridor
        {0x55A, 12}, // Y7 RoR Corridor
        {0x8AE, 12}, // Y6 RoR Corridor
        {0xD82, 12}, // Y5 RoR Corridor
        {0xE6, 13}, // Y8 Weasley Storage
        {0x566, 13}, // Y7 Weasley Storage
        {0x8BA, 13}, // Y6 Weasley Storage
        {0xD8E, 13}, // Y5 Weasley Storage
        {0xF2, 14}, // Y8 Weasley Courtyard
        {0x572, 14}, // Y7 Weasley Courtyard
        {0x8C6, 14}, // Y6 Weasley Courtyard
        {0xD9A, 14}, // Y5 Weasley Courtyard
        {0xFE, 15}, // Y8 Great Hall
        {0x57E, 15}, // Y7 Great Hall
        {0x8D2, 15}, // Y6 Great Hall
        {0xDB2, 15}, // Y5 Great Hall
        {0x10A, 16}, // Y8 Great Hall Lobby
        {0x58A, 16}, // Y7 Great Hall Lobby
        {0x8DE, 16}, // Y6 Great Hall Lobby
        {0xDBE, 16}, // Y5 Great Hall Lobby
        {0x116, 17}, // Y8 Black Lake
        {0x122, 18}, // Y8 Quidditch Pitch
        {0x12E, 19}, // Y8 Thestral Forest
        {0x596, 19}, // Y7 Thestral Forest
        {0x8EA, 19}, // Y6 Thestral Forest
        {0xDD6, 19}, // Y5 Thestral Forest
        {0x13A, 20}, // Y8 Hogwarts Grounds
        {0x5A2, 20}, // Y7 Hogwarts Grounds
        {0x8F6, 20}, // Y6 Hogwarts Grounds
        {0xDE2, 20}, // Y5 Hogwarts Grounds
        {0x146, 21}, // Y8 Ravenclaw Tower
        {0x5AE, 21}, // Y7 Ravenclaw Tower
        {0x902, 21}, // Y6 Ravenclaw Tower
        {0x152, 22}, // Y8 Gryffindor Common Room
        {0x5BA, 22}, // Y7 Gryffindor Common Room
        {0x90E, 22}, // Y6 Gryffindor Common Room
        {0xDEE, 22}, // Y5 Gryffindor Common Room
        {0x15E, 23}, // Y8 House Corridor
        {0x5C6, 23}, // Y7 House Corridor
        {0x91A, 23}, // Y6 House Corridor
        {0xDFA, 23}, // Y5 House Corridor
        {0x16A, 24}, // Y8 Astronomy Tower
        {0x5D2, 24}, // Y7 Astronomy Tower
        {0x896, 24}, // Y6 Astronomy Tower
        {0x176, 25}, // Y8 Agua Charms
        {0x5DE, 25}, // Y7 Agua Charms
        {0x926, 25}, // Y6 Agua Charms
        {0x182, 26}, // Y8 Diffindo Charms
        {0x5EA, 26}, // Y7 Diffindo Charms
        {0x932, 26}, // Y6 Diffindo Charms
        {0xE06, 26}, // Y5 Diffindo Charms
        {0x18E, 27}, // Y8 Potions Classroom
        {0x5F6, 27}, // Y7 Potions Classroom
        {0x93E, 27}, // Y6 Potions Classroom
        {0x19A, 28}, // Y8 Divination
        {0x602, 28}, // Y7 Divination
        {0x94A, 28}, // Y6 Divination
        {0xE12, 28}, // Y5 Divination
        {0x1A6, 29}, // Y8 DADA Classroom
        {0x60E, 29}, // Y7 DADA Classroom
        {0x956, 29}, // Y6 DADA Classroom
        {0xE1E, 29}, // Y5 DADA Classroom
        {0x1B2, 30}, // Y8 Divination Courtyard
        {0x61A, 30}, // Y7 Divination Courtyard
        {0x962, 30}, // Y6 Divination Courtyard
        {0xE2A, 30}, // Y5 Divination Courtyard
        {0x1BE, 31}, // Y8 Classroom Lobby
        {0x626, 31}, // Y7 Classroom Lobby
        {0x96E, 31}, // Y6 Classroom Lobby
        {0xE36, 31}, // Y5 Classroom Lobby
        {0x1D6, 32}, // Y8 Grand Staircase
        {0x63E, 32}, // Y7 Grand Staircase
        {0x986, 32}, // Y6 Grand Staircase
        {0xE4E, 32}, // Y5 Grand Staircase
        {0x1E2, 33}, // Y8 Library
        {0x64A, 33}, // Y7 Library
        {0x992, 33}, // Y6 Library
        {0x1EE, 34}, // Y8 Foyer
        {0x656, 34}, // Y7 Foyer
        {0x99E, 34}, // Y6 Foyer
        {0xE5A, 34}, // Y5 Foyer
        {0X112A, 35}, // Y8 Madam Malkin's
        {0x1172, 35}, // Y7 Madam Malkin's
        {0x11BA, 35}, // Y6 Madam Malkin's
        {0x11EA, 35}, // Y5 Madam Malkin's
        {0x1136, 36}, // Y8 Knockturn Alley
        {0x117E, 36}, // Y7 Knockturn Alley
        {0x11C6, 36}, // Y6 Knockturn Alley
        {0x120E, 36}, // Y5 Knockturn Alley
        {0x1142, 37}, // Y8 Leaky Cauldron
        {0x118A, 37}, // Y7 Leaky Cauldron
        {0x11D2, 37}, // Y6 Leaky Cauldron
        {0x121A, 37}, // Y5 Leaky Cauldron
        {0x114E, 38}, // Y8 WWW
        {0x1196, 38}, // Y7 WWW
        {0x11F6, 38}, // Y6 WWW
        {0x1226, 38}, // Y5 WWW
        {0x115A, 39}, // Y8 Diagon Alley
        {0x11A2, 39}, // Y7 Diagon Alley
        {0x1202, 39}, // Y6 Diagon Alley
        {0x1232, 39}, // Y5 Diagon Alley
    };

    // Helper function to convert from the address the game is going to write to, to the Archi ID offset
    public static int GetHubID(int offset)
    {
        // Use the same approach the game does to convert from assembly register to final offset in the container
        offset *= 4;
        offset += 2;
        return hubOffsets.TryGetValue(offset, out int ID) ? ID : -1;
    }

    // Helper function to Collect a Red Brick. Takes the Archi ID offset and looks up the memory offset
    public static unsafe void UnlockHubRB(int ID)
    {
        var kvp = hubOffsets.FirstOrDefault(k => k.Value == ID);

        if (kvp.Key == 0 && kvp.Value == 0)
        {
            Game.PrintToLog($"[Hub] Could not find Hub RB offset for ID {ID}");
            return;
        }

        byte* ptr = HubBaseAddress + (nuint)kvp.Key;

        if (ptr == null || HubBaseAddress == null)
        {
            Game.PrintToLog($"[Hub] Can't Unlock Hub RB, null pointer at 0x{(nuint)ptr:X}");
            return;
        }

        *ptr |= (byte)BitMask.RedBrick;
    }

    // Helper function to unlock a Hub SIP. Takes the Archi ID offset and looks up the memory offset
    public static unsafe void UnlockHubSIP(int ID)
    {
        var kvp = hubOffsets.FirstOrDefault(k => k.Value == ID);

        if (kvp.Key == 0 && kvp.Value == 0)
        {
            Game.PrintToLog($"[Hub] Could not find Hub SIP offset for ID {ID}");
            return;
        }

        byte* ptr = HubBaseAddress + (nuint)kvp.Key;

        if (ptr == null || HubBaseAddress == null)
        {
            Game.PrintToLog($"[Hub] Can't Unlock Hub SIP, null pointer at 0x{(nuint)ptr:X}");
            return;
        }

        *ptr |= (byte)BitMask.StudentInPeril;
    }

    /// <summary>
    /// Resets hub collectibles (red bricks and students in peril) for game state transitions.
    /// </summary>
    public static unsafe void ResetHub()
    {
        bool shuffleRedbricks = Mod.LHP2_Archipelago!.SlotDataInstance!.ShuffleRedBricks != 2;
        foreach (var kvp in hubOffsets)
        {
            byte* ptr = HubBaseAddress + (nuint)kvp.Key;

            if (ptr == null)
            {
                Game.PrintToLog($"Hub pointer invalid at offset 0x{kvp.Key:X}");
                continue;
            }
            if (shuffleRedbricks)
            {
                *ptr &= unchecked((byte)~(byte)BitMask.RedBrick);
            }
            *ptr &= unchecked((byte)~(byte)BitMask.StudentInPeril);
        }
    }


    /// <summary>
    /// Unlocks a purchased red brick by converting the Archi ID to a bit array index.
    /// </summary>
    /// <param name="id">The Archi ID of the purchased red brick.</param>
    public static unsafe void ReceivedRedBrickUnlock(int id)
    {
        int byteOffset = id / 8;
        int bitOffset = id % 8;

        byte* ptr = RedBrickPurchBaseAddress + byteOffset;

        if (ptr == null || RedBrickPurchBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock Red Brick, null pointer at 0x{(nuint)ptr:X}");
            return;
        }

        *ptr |= (byte)(1 << bitOffset);
    }

    /// <summary>
    /// Resets the purchased red brick unlock array for game state transitions.
    /// </summary>
    public static unsafe void ResetRedBrickUnlock()
    {
        for (int i = 0; i < 3; i++)
        {
            byte* ptr = RedBrickPurchBaseAddress + i;
            *ptr = 0;
        }
    }

    /*
    This is a QOL update that we wrote to automatically enable the same red bricks that the player had enabled prior to reloading the save file since that is only a feature in the Collection edition.
    We run this save feature after the player leaves the Extras menu.
    */
    public static unsafe void SaveRedBricksEnabled()
    {
        // Create a new byte array with 24 bits, 1 for each red brick
        byte[] enabledArray = new byte[3];
        for (int i = 0; i < 24; i++)
        {
            // Each Red Brick Enabled/Disabled is a byte, 0x18 bytes apart
            byte* ptr = RedBrickEnabledAddress + i * 0x18;
            bool isEnabled = *ptr != 0;

            if (isEnabled)
            {
                // Convert from byte index to bit index
                int byteIndex = i / 8;
                int bitIndex = i % 8;
                // Write to the new array when it is enabled
                enabledArray[byteIndex] |= (byte)(1 << bitIndex);
            }
        }
        // Write our new array to the save file address that we determine would be appropriate.
        for (int i = 0; i < 3; i++)
        {
            *(RedBrickSaveFileAddress + i) = enabledArray[i];
        }
    }

    // This helper function is the inverse of the one above. It reads the save file and enables the red bricks as applicable.
    public static unsafe void LoadRedBricksEnabled()
    {
        byte[] enabledArray = new byte[3];
        for (int i = 0; i < 3; i++)
        {
            enabledArray[i] = *(RedBrickSaveFileAddress + i);
        }
        for (int i = 0; i < 24; i++)
        {
            int byteIndex = i / 8;
            int bitIndex = i % 8;
            bool isEnabled = (enabledArray[byteIndex] & (1 << bitIndex)) != 0;
            byte* ptr = RedBrickEnabledAddress + i * 0x18;
            *ptr = isEnabled ? (byte)1 : (byte)0;
        }
    }

    // This is a helper function to handle when the player receives a purple stud.
    public static unsafe void HandlePurpleStud()
    {
        // Because we switch between game states, verify how many purple studs the player has received
        int purpleStudCount = Mod.LHP2_Archipelago!.CountItemsReceivedWithId(699);

        if (purpleStudCount == 0)
        {
            return;
        }

        if (StudTotalBaseAddress == null || PurpleCountAddress == null)
        {
            Game.PrintToLog($"Can't Update Stud Total, null pointer at 0x{(nuint)StudTotalBaseAddress:X}");
            return;
        }

        /* 
        In Purple Count Address, I write to the save file how many purple studs we have given the player. 
        This is so we don't duplicate any or miss giving them any.
        We then verify here if they are missing any in their stud total and add 10k if so
        */
        if (*PurpleCountAddress < purpleStudCount)
        {
            *PurpleCountAddress += 1;
            *StudTotalBaseAddress += 10000;
        }
    }

    public static byte GoldBrickCount { get; private set; } = 0;
    public static byte HorcruxCount { get; private set; } = 0;

    /* 
    Instead of writing Gold Bricks to the specific location (i.e. Diagon Alley Gold Brick), we just increase the total and display the updated count.
    Initially we were going to have only individual GBs, however, since the game them tied to other items/spells, we decided to make it so they were packs of 5.
    */
    public static void ReceivedGoldBrick(int id)
    {
        if (id == 700)
        {
            GoldBrickCount++;
        }
        else if (id == 701)
        {
            GoldBrickCount += 5;
        }
    }

    // This helper function runs when pausing the menu to make sure that we display the correct count of GBs
    // TODO: Update this to use the AP count function rather than a separate variable.
    public static unsafe void UpdateGoldBrickCount()
    {
        byte* ptr = GoldBrickBaseAddress + 0x01A7A;
        if (ptr == null || GoldBrickBaseAddress == null)
        {
            Game.PrintToLog($"Can't Show Gold Bricks, null pointer at 0x{(nuint)ptr:X}");
            return;
        }
        *ptr = GoldBrickCount;
    }

    // This helper function resets the gold count variable to 0
    public static void ResetGoldBrickCount()
    {
        GoldBrickCount = 0;
    }

    // This is a helper function that verifies the count of horcruxes received and updates the on screen text
    public static void UpdateWinConText()
    {
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 0)
        {
            HorcruxCount = (byte)Mod.LHP2_Archipelago!.CountItemsCheckedInRange(440, 446);
            HintSystem.DisplayHorcruxCount(HorcruxCount);
        }
        if (Mod.LHP2_Archipelago!.SlotDataInstance!.EndGoal == 2)
        {
            byte levelsBeaten = (byte)Mod.LHP2_Archipelago!.CountLocationsCheckedInRange(450, 473);
            HintSystem.DisplayLevelsBeaten(levelsBeaten);
        }
    }
    private static Dictionary<string, Map>? GameMaps;
    private static unsafe ushort* Y5MapBase => *(ushort**)(Mod.BaseAddress + 0xB06914); // Y5 London
    private static unsafe ushort* Y6MapBase => *(ushort**)(Mod.BaseAddress + 0xB06918); // Y6 London
    private static unsafe ushort* Y7MapBase => *(ushort**)(Mod.BaseAddress + 0xB0691C); // Y7 London
    private static unsafe ushort* Y8MapBase => *(ushort**)(Mod.BaseAddress + 0xB06920); // Y8 London
    private static unsafe ushort* DarkTimesMapBase => *(ushort**)(Mod.BaseAddress + 0xB069AC); // Dark Times

    public static unsafe void InitializeGameMaps()
    {
        if (GameMaps != null)
            return;

        GameMaps = new Dictionary<string, Map>
        {
            { "Y5London", new Map("Y5London", Y5MapBase + 0x32) },
            { "Y6London", new Map("Y6London", Y6MapBase + 0x32) },
            { "Y7London", new Map("Y7London", Y7MapBase + 0x32) },
            { "Y8London", new Map("Y8London", Y8MapBase + 0x32) },
            { "Y5Foyer", new Map("Y5Foyer", Y5MapBase + 0x960 + 0x32) },
            { "Y6Foyer", new Map("Y6Foyer", Y6MapBase + 0xA00 + 0x32) },
            { "Y7Foyer", new Map("Y7Foyer", Y7MapBase + 0xA00 + 0x32) },
            { "Y8Foyer", new Map("Y8Foyer", Y8MapBase + 0xA50 + 0x32) },
            { "Y5ClassLobby", new Map("Y5ClassLobby", Y5MapBase + 0x870 + 0x32) },
            { "Y6ClassLobby", new Map("Y6ClassLobby", Y6MapBase + 0x8C0 + 0x32) },
            { "Y7ClassLobby", new Map("Y7ClassLobby", Y7MapBase + 0x8C0 + 0x32) },
            { "Y8ClassLobby", new Map("Y8ClassLobby", Y8MapBase + 0x910 + 0x32) },
            { "Y6Library", new Map("Y6Library", Y6MapBase + 0x9B0 + 0x32) },
            { "Y7Library", new Map("Y7Library", Y7MapBase + 0x9B0 + 0x32) },
            { "Y8Library", new Map("Y8Library", Y8MapBase + 0xA00 + 0x32) },
            { "Y6Potions", new Map("Y6Potions", Y6MapBase + 0x780 + 0x32) },
            { "Y7Potions", new Map("Y7Potions", Y7MapBase + 0x780 + 0x32) },
            { "Y8Potions", new Map("Y8Potions", Y8MapBase + 0x7D0 + 0x32) },
            { "Y6HogsmeadePath", new Map("Y6HogsmeadePath", Y6MapBase + 0xF0 + 0x32) },
            { "Y7HogsmeadePath", new Map("Y7HogsmeadePath", Y7MapBase + 0xF0 + 0x32) },
            { "Y8HogsmeadePath", new Map("Y8HogsmeadePath", Y8MapBase + 0xF0 + 0x32) },
            { "Y6AstronomyTower", new Map("Y6AstronomyTower", Y6MapBase + 0x320 + 0x32) },
            { "Y7AstronomyTower", new Map("Y7AstronomyTower", Y7MapBase + 0x690 + 0x32) },
            { "Y8AstronomyTower", new Map("Y8AstronomyTower", Y8MapBase + 0x6E0 + 0x32) },
            { "Y6RavenclawTower", new Map("Y6RavenclawTower", Y6MapBase + 0x5F0 + 0x32) },
            { "Y7RavenclawTower", new Map("Y7RavenclawTower", Y7MapBase + 0x5A0 + 0x32) },
            { "Y8RavenclawTower", new Map("Y8RavenclawTower", Y8MapBase + 0x5F0 + 0x32) },
            { "Y6AguaCharms", new Map("Y6AguaCharms", Y6MapBase + 0x6E0 + 0x32) },
            { "Y7AguaCharms", new Map("Y7AguaCharms", Y7MapBase + 0x6E0 + 0x32) },
            { "Y8AguaCharms", new Map("Y8AguaCharms", Y8MapBase + 0x730 + 0x32) },
            { "Y8Quidditch", new Map("Y8Quidditch", Y8MapBase + 0x500 + 0x32) },
            { "Y8BlackLake", new Map("Y8BlackLake",  Y8MapBase + 0x4B0 + 0x32) },
            { "DarkTimes", new Map("DarkTimes", DarkTimesMapBase + 0x32) },
            { "OutOfRetirement", new Map("OutOfRetirement", DarkTimesMapBase - 0x1AFE)},
            { "TheSevenHarrys", new Map("TheSevenHarrys", DarkTimesMapBase - 0x3C1E)},
            { "TheThiefsDownfall", new Map("TheThiefsDownfall", DarkTimesMapBase - 0x54CE)},
        };
    }
    // TODO: Grab Hogwarts Path, Grounds, Div Courtyard, and Dorm Lobby

    // The following function is our current implementation of how to time travel & Fast Travel back to Hogwarts.
    public static unsafe void FastTravel(string mapRequested)
    {
        // Verify that the player has completed DADA banned in game before time travelling
        // The game doesn't really allow you to be in future years if DADA banned isn't completed
        byte* y5GhostPtr = GhostPathBaseAddress + 0x20;
        if ((*y5GhostPtr & (1 << 2)) == 0)
        {
            Game.PrintToLog("Please complete DADA Banned Lesson before changing years");
            return;
        }

        byte* y6GhostPtr = GhostPathBaseAddress + 0x34;
        if (mapRequested == "Y6QUAD" || mapRequested == "Y6FOYE")
        {
            if ((*y6GhostPtr & (1 << 2)) == 0 || (*y6GhostPtr & (1 << 3)) == 0)
            {
                Game.PrintToLog("Please complete Specs Lesson & Arrive to Y6 Hogwarts before fast travelling to Year 6 Hogwarts.");
                return;
            }
        }

        if (!TryGetTargetYear(mapRequested, out int year))
        {
            Game.PrintToLog($"Unknown Year Requested: {mapRequested}.");
            return;
        }

        AdjustHubMaps(year);

        Map? currentMap = GetCurrentLondonConstantPtr(Mod.GameInstance!.LevelID);
        if (currentMap == null)
        {
            Game.PrintToLog($"Critical Error cannot fast travel. LevelID is: {Mod.GameInstance!.LevelID}");
            return;
        }

        Map? destinationMap = TryGetFastTravelMapValue(mapRequested);
        if (destinationMap == null)
        {
            Game.PrintToLog($"Unknown fast travel destination: {mapRequested}.");
            return;
        }

        byte* N0CUT5CheatCodeConstant = (byte*)(Mod.BaseAddress + 0x8968A4);
        HintSystem.SetMessageText(mapRequested, (uint)N0CUT5CheatCodeConstant);

        currentMap.UpdateMap(destinationMap.MapConstant);
        AdjustLeakyMapConstants(destinationMap.MapConstant, destinationMap.EntranceConstant);
    }

    private static Map? LookUpMap(string mapKey)
    {
        return GameMaps!.TryGetValue(mapKey, out Map? map)
        ? map
        : null;
    }

    private static Map? GetCurrentLondonConstantPtr(int levelId)
    {
        string? mapKey = levelId switch
        {
            1 => "Y5London",
            2 => "Y6London",
            3 => "Y7London",
            4 => "Y8London",
            _ => null
        };

        if (mapKey == null || GameMaps == null)
        {
            return null;
        }

        return LookUpMap(mapKey);
    }

    private static bool TryGetTargetYear(string mapRequested, out int year)
    {
        year = 0;
        if (string.IsNullOrEmpty(mapRequested) || mapRequested.Length < 2 || mapRequested[0] != 'Y')
        {
            return false;
        }

        year = mapRequested[1] switch
        {
            '5' => 5,
            '6' => 6,
            '7' => 7,
            '8' => 8,
            _ => 0
        };

        return year != 0;
    }

    private static Map? TryGetFastTravelMapValue(string mapRequested)
    {

        string? mapKey = mapRequested switch
        {
            "Y5LOND" => "Y5London",
            "Y6LOND" => "Y6London",
            "Y7LOND" => "Y7London",
            "Y8LOND" => "Y8London",
            "Y5FOYE" => "Y5Foyer",
            "Y6FOYE" => "Y6Foyer",
            "Y7FOYE" => "Y7Foyer",
            "Y8FOYE" => "Y8Foyer",
            "Y5QUAD" => "Y5Quad",
            "Y6QUAD" => "Y6Quad",
            "Y7QUAD" => "Y7Quad",
            "Y8QUAD" => "Y8Quad",
            _ => null
        };

        if (mapKey == null || GameMaps == null)
        {
            return null;
        }

        return LookUpMap(mapKey);
    }

    /*
    These following functions are used to update the Map ID Constant for Out of Retirement, The Seven Harrys, and The Thief's Downfall. In story mode, the Leaky2London loading zone is updated to send you to 1 of these 3 levels. As such, we are updating the Map Constants so that it sends the player to London instead of sending them to the level. We change all 3 of them, cause sometimes they don't transfer very well across time travelling.
    */
    public static void ChangeLeakyLoadingZones(int level)
    {
        Map? map = level switch
        {
            1 => LookUpMap("Y5London"),
            2 => LookUpMap("Y6London"),
            3 => LookUpMap("Y7London"),
            4 => LookUpMap("Y8London"),
            _ => null
        };

        if (map == null)
        {
            Game.PrintToLog($"Could not properly adjust Leaky loading zone. Level: {level}");
            return;
        }
        AdjustLeakyMapConstants(map.MapConstant, map.EntranceConstant);
    }

    // Broken out into a separate function cause time travel wasn't always working so they need to be updated often
    private static void AdjustLeakyMapConstants(ushort mapValue, byte entranceValue)
    {
        Map? OutOfRetirement = LookUpMap("OutOfRetirement");
        Map? TheSevenHarrys = LookUpMap("TheSevenHarrys");
        Map? TheThiefsDownfall = LookUpMap("TheThiefsDownfall");

        if (OutOfRetirement == null || TheSevenHarrys == null || TheThiefsDownfall == null)
        {
            Game.PrintToLog("One of the Level Map's is Null. Please alert a dev.");
            return;
        }

        OutOfRetirement.UpdateMap(mapValue);
        OutOfRetirement.UpdateEntrance(entranceValue);
        TheSevenHarrys.UpdateMap(mapValue);
        TheSevenHarrys.UpdateEntrance(entranceValue);
        TheThiefsDownfall.UpdateMap(mapValue);
        TheThiefsDownfall.UpdateEntrance(entranceValue);

    }

    // This function returns the Level Map IDs back to their intended ID. Used upon level selector opening
    public static void RestoreLeakyLoadingZones()
    {
        Map? OutOfRetirement = LookUpMap("OutOfRetirement");
        Map? TheSevenHarrys = LookUpMap("TheSevenHarrys");
        Map? TheThiefsDownfall = LookUpMap("TheThiefsDownfall");

        if (OutOfRetirement == null || TheSevenHarrys == null || TheThiefsDownfall == null)
        {
            Game.PrintToLog("One of the Level Maps is Null. Please alert a dev.");
            return;
        }

        OutOfRetirement.RestoreMap();
        TheSevenHarrys.RestoreMap();
        TheThiefsDownfall.RestoreMap();
    }

    // This helper function fixes the constants if the player leaves Leaky Cualdron
    public static void VerifyLondonMapIDs()
    {
        Map? Y5 = LookUpMap("Y5London");
        Map? Y6 = LookUpMap("Y6London");
        Map? Y7 = LookUpMap("Y7London");
        Map? Y8 = LookUpMap("Y8London");

        if (Y5 == null || Y6 == null || Y7 == null || Y8 == null)
        {
            Game.PrintToLog("One of the London Maps is Null. Please alert a dev.");
            return;
        }

        Y5.RestoreMap();
        Y6.RestoreMap();
        Y7.RestoreMap();
        Y8.RestoreMap();

    }

    public static void UpdateDarkTimesMap()
    {
        Map? DarkTimes = LookUpMap("DarkTimes");

        if (DarkTimes == null)
        {
            Game.PrintToLog("Dark Times is Null. Please Report to a dev.");
            return;
        }

        DarkTimes.UpdateMap(388);
    }

    public static void RestoreDarkTimesMap()
    {
        Map? DarkTimes = LookUpMap("DarkTimes");

        if (DarkTimes == null)
        {
            Game.PrintToLog("Dark Times is Null. Please Report to a dev.");
            return;
        }

        DarkTimes.UpdateMap(361);
    }

    // // The missing maps will make you time travel to a different year, these next series of functions are to change the Map constants in the future years so you don't time travel.
    // public static unsafe void UpdateMissingMapConstants(int level)
    // {
    //     if (level < 1 || level > 4)W
    //     {
    //         return;
    //     }

    //     bool applyY5Overrides = level == 1;

    //     SetFutureYearMapConstants(GetMapConstantPtr("Y6Library"), GetMapConstantPtr("Y7Library"), GetMapConstantPtr("Y8Library"),
    //         applyY5Overrides ? Y5Foyer : null,
    //         Y6Library, Y7Library, Y8Library);

    //     SetFutureYearMapConstants(GetMapConstantPtr("Y6Potions"), GetMapConstantPtr("Y7Potions"), GetMapConstantPtr("Y8Potions"),
    //         applyY5Overrides ? Y5ClassLobby : null,
    //         Y6Potions, Y7Potions, Y8Potions);

    //     SetFutureYearMapConstants(GetMapConstantPtr("Y6HogsmeadePath"), GetMapConstantPtr("Y7HogsmeadePath"), GetMapConstantPtr("Y8HogsmeadePath"),
    //         applyY5Overrides ? Y5HogwartsPath : null,
    //         Y6HogsmeadePath, Y7HogsmeadePath, Y8HogsmeadePath);

    //     SetFutureYearMapConstants(GetMapConstantPtr("Y6AstronomyTower"), GetMapConstantPtr("Y7AstronomyTower"), GetMapConstantPtr("Y8AstronomyTower"),
    //         applyY5Overrides ? Y5DivinationCourtyard : null,
    //         Y6AstronomyTower, Y7AstronomyTower, Y8AstronomyTower);

    //     SetFutureYearMapConstants(GetMapConstantPtr("Y6RavenclawTower"), GetMapConstantPtr("Y7RavenclawTower"), GetMapConstantPtr("Y8RavenclawTower"),
    //         applyY5Overrides ? Y5DormLobby : null,
    //         Y6RavenclawTower, Y7RavenclawTower, Y8RavenclawTower);

    //     SetFutureYearMapConstants(GetMapConstantPtr("Y6AguaCharms"), GetMapConstantPtr("Y7AguaCharms"), GetMapConstantPtr("Y8AguaCharms"),
    //         applyY5Overrides ? Y5ClassLobby : null,
    //         Y6AguaCharms, Y7AguaCharms, Y8AguaCharms);

    //     ushort? groundsMap = level switch
    //     {
    //         1 => Y5Grounds,
    //         2 => Y6Grounds,
    //         3 => Y7Grounds,
    //         _ => null
    //     };

    //     SetSingleMapConstant(GetMapConstantPtr("Y8Quidditch"), groundsMap, Y8Quidditch);
    //     SetSingleMapConstant(GetMapConstantPtr("Y8BlackLake"), groundsMap, Y8BlackLake);
    // }

    // private static unsafe void SetFutureYearMapConstants(
    //     ushort* y6Ptr,
    //     ushort* y7Ptr,
    //     ushort* y8Ptr,
    //     ushort? mapValue,
    //     ushort y6Default,
    //     ushort y7Default,
    //     ushort y8Default)
    // {
    //     if (mapValue.HasValue)
    //     {
    //         *y6Ptr = mapValue.Value;
    //         *y7Ptr = mapValue.Value;
    //         *y8Ptr = mapValue.Value;
    //     }
    //     else
    //     {
    //         *y6Ptr = y6Default;
    //         *y7Ptr = y7Default;
    //         *y8Ptr = y8Default;
    //     }
    // }

    // private static unsafe void SetSingleMapConstant(ushort* ptr, ushort? mapValue, ushort defaultValue)
    // {
    //     *ptr = mapValue ?? defaultValue;
    // }

    /*
    In Year 5, the game will warp you back to Hogwarts instead of having you walk back.
    This essentially locks you out of London through Quad until you have diffindo.
    To prevent all of these items from being locked behind diffindo, we clear out this warp so the player has to walk back.
    */
    public static unsafe void ClearReturnToHogwartsLocation()
    {
        byte* entrancePTR = SecondPointerWarp + 0x31C;
        for (int i = 0; i < 0x40; i++)
        {
            byte* ptr = entrancePTR + i;
            *ptr = 0;
        }
    }

    /*
    In The Seven Harrys, the game ensures you can't return to leaky cauldron unless you unlock apparition by completing the level in story mode.
    If you try, it will warp you back to Inside the Burrow instead.
    To get around this since this isn't applicable for the Archi, we revert the value in that address back to The Leaky Cauldron.
    To note: you can use this to warp back to any map, however, the game still thinks you are in a level if it isn't to leaky so that causes some issues.
    */
    public static unsafe void FixReturnToLeakyCauldron()
    {
        byte* returnToLeakyPTR = SecondPointerWarp + 0x2FC;
        byte year = (byte)(Mod.GameInstance!.PrevLevelID + 0x4);
        if (year < 5 || year > 8)
        {
            Game.PrintToLog($"Could not get the proper year to return to. Year: {year}");
            return;
        }
        string hubName = $"{year}HubLeakyCauldron";
        Game.PrintToLog($"Return to Leaky address: 0x{(uint)returnToLeakyPTR:X}, year: {year}");
        HintSystem.SetMessageText(hubName, (uint)returnToLeakyPTR);
    }

    /*
    Years 6-7 first ghost path is into a level that we skip in the archi.
    Year 8 also only has levels and doesn't have any hub tasks
    This helper function marks those levels as completed.
    */
    public static unsafe void CompleteStartingGhostLevels()
    {
        byte* y6GhostPtr = GhostPathBaseAddress + 0x34;
        byte* y7GhostPtr = GhostPathBaseAddress + 0x48;
        byte* y8GhostPtr = GhostPathBaseAddress + 0x5C;

        const byte CompletedBit = 1 << 1;
        const byte YearCompleteMask = 0x7E;

        if ((*y6GhostPtr & CompletedBit) == 0)
            *y6GhostPtr |= CompletedBit;

        if ((*y7GhostPtr & CompletedBit) == 0)
            *y7GhostPtr |= CompletedBit;

        if (*y8GhostPtr != YearCompleteMask)
            *y8GhostPtr = YearCompleteMask;
    }

    /*
    This helper function is used to handle updating the Hub Ghost tasks.
    The ghost task updates upon level completion, lesson completion, among other things.
    We have turned off the vanilla treatment and updated it to match our own functionality.
    This includes sending archi locations, marking level tasks as complete, or nothing.
    */
    public static unsafe void HandleGhostPaths(int eax, int edx)
    {
        // Game.PrintToLog("Handling Ghost Paths");
        ushort dx = (ushort)(edx & 0xFFFF); // Game only writes dx to this address, converting for safety
        // Game.PrintToLog($"eax: 0x{eax:X}, edx: 0x{edx:X}, dx: 0x{dx:X}");

        byte* y5GhostPtr = GhostPathBaseAddress + 0x20;
        byte* y5GhostPtr2 = GhostPathBaseAddress + 0x21;
        byte* y6GhostPtr = GhostPathBaseAddress + 0x34;
        byte* y6GhostPtr2 = GhostPathBaseAddress + 0x35;
        byte* y7GhostPtr = GhostPathBaseAddress + 0x48;

        // Handle Y5 Ghost Tasks
        if (eax == (int)y5GhostPtr)
        {
            switch (dx)
            {
                case 0x2: // Arrive at Hogwarts Y5
                    *y5GhostPtr |= 1 << 1; // Mark Arrive at Hogwarts Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1006))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1006);
                    break;
                case 0x4: // DADA Banned Lesson
                    *y5GhostPtr |= 1 << 2; // Mark DADA Banned Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1007))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1007);
                    break;
                case 0x8: // Thestral Forest Lesson
                    *y5GhostPtr |= 1 << 3; // Mark Thestral Forest Complete
                    *y5GhostPtr |= 1 << 4; // Mark Dumbledore's Army Story Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1008))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1008);
                    break;
                case 0x20: // Dueling Lesson
                    *y5GhostPtr |= 1 << 5; // Mark Dueling Complete
                    *y5GhostPtr |= 1 << 6; // Mark Focus! Story Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1009))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1009);
                    break;
                case 0x80: // Diffindo Lesson
                    *y5GhostPtr |= 1 << 7; // Mark Diffindo Complete
                    *y5GhostPtr2 |= 1 << 0; // Mark Kreacher Discomfort Story Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1010))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1010);
                    break;
                case 0x200: // Patroneous Lesson
                    *y5GhostPtr2 |= 1 << 1; // Mark Patronus Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1011))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1011);
                    break;
                case 0x400: // Befriend Grawp Lesson
                    *y5GhostPtr2 |= 1 << 2; // Mark Befriend Grawp
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1012))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1012);
                    break;
                case 0x800: // Snape's Worst Memory
                    *y5GhostPtr2 |= 1 << 3; // Mark Snape's Worst Memory Story Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1013))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1013);
                    break;
                case 0x1000: // OWLs Lesson
                    *y5GhostPtr2 |= 1 << 4; // Mark OWLs Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1014))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1014);
                    // Game doesn't open WW Courtyard if these 2 are marked complete. We handle this upon map update after the fact
                    // *y5GhostPtr2 |= 1 << 5; // Mark A Giant Viruoso Story Complete
                    // *y5GhostPtr2 |= 1 << 6; // Mark A Veiled Threat Story Complete
                    Game.CheckAndReportLocation(1015); // Send Y5 Story Complete
                    break;
                default:
                    Game.PrintToLog($"Y5 Level Beaten, doing nothing: 0x{dx:X}");
                    break;
            }
        }
        // Handle Y6 Ghost Tasks
        else if (eax == (int)y6GhostPtr)
        {
            switch (dx)
            {
                case 0x4: // Specs Lesson
                    *y6GhostPtr |= 1 << 2; // Mark Specs Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1016))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1016);
                    break;
                case 0x8: // Arrive at Hogwarts Y6
                    *y6GhostPtr |= 1 << 3; // Mark Arrive at Hogwarts Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1017))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1017);
                    break;
                case 0x10: // Draught of Living Death Lesson
                    *y6GhostPtr |= 1 << 4; // Mark Draught of Living Death Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1018))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1018);
                    break;
                case 0x20: // Dumbledore's First Lesson
                    *y6GhostPtr |= 1 << 5; // Mark Dumbledore's First Lesson Complete
                    *y6GhostPtr |= 1 << 6; // Mark Just Desserts Story Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1019))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1019);
                    break;
                case 0x80: // Aguamenti Lesson
                    *y6GhostPtr |= 1 << 7; // Mark Aguamenti Lesson Complete
                    *y6GhostPtr2 |= 1 << 0; // Mark A Not So Merry Christmas Story Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1020))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1020);
                    break;
                case 0x200: // Reducto Lesson
                    *y6GhostPtr2 |= 1 << 1; // Mark Reducto Lesson Complete
                    if (Mod.LHP2_Archipelago!.IsLocationChecked(1021))
                    {
                        break;
                    }
                    Game.CheckAndReportLocation(1021);
                    break;
                case 0x400: // Dumledore's Second Lesson
                    *y6GhostPtr2 |= 1 << 2; // Mark Dumledore's Second Lesson Complete
                    *y6GhostPtr2 |= 1 << 3; // Mark Love Hurts Story Complete
                    *y6GhostPtr2 |= 1 << 4; // Mark Felix Felicis Story Complete
                    *y6GhostPtr2 |= 1 << 5; // Mark The Horcrux and The Hand Story Complete
                    Game.CheckAndReportLocation(1022); // Send Dumbledore's Second Lesson
                    Game.CheckAndReportLocation(1023); // Send Y6 Story Complete
                    break;
                default:
                    Game.PrintToLog($"Y6 Level Beaten, doing nothing: 0x{dx:X}");
                    break;
            }
        }
        // Handle Y7 Ghost Tasks
        else if (eax == (int)y7GhostPtr)
        {
            if (dx == 4) // Cafe Fight
            {
                *y7GhostPtr = 254; // Mark all Y7 Ghost Paths as Complete
                if (Mod.LHP2_Archipelago!.IsLocationChecked(1027))
                {
                    return;
                }
                Game.CheckAndReportLocation(1027);
            }
            else
            {
                Game.PrintToLog($"Y7 Level Beaten, doing nothing with eax: 0x{eax:X} and dx: 0x{dx:X}");
            }

        }
        else
        {
            Game.PrintToLog($"Y8 Level Beaten, doing nothing with eax: 0x{eax:X} and dx: 0x{dx:X}");
        }
    }

    /*
    Because we are skipping levels and allowing time travel, there are certain things that break.
    These following functions correct these broken things in the save file.
    We call this function when the player is trying to switch years cause they will reload the save.
    This function will do a byte search through the save file Map container and update the game flags as needed.
    There are several downsides to this method (i.e. the save file info isn't written until the player enters that map at least once and map reloads are required).
    TODO: with the breakthrough with loading zones and with security doors, see if we can update the information instantly.
    */
    public static unsafe void AdjustHubMaps(int year)
    {
        leaky2LondonAddress = GetHubMapAddress("HubLeakyCauldron", 0xB7B); // Leaky2London Loading Zone
        hogsPathAddress = GetHubMapAddress("HogsApproach", 0); // HogPath2Courtyard Loading Zone
        wildernessAddress = GetHubMapAddress("ForestHub", 0); // Wilderness
        quadAddress = GetHubMapAddress("Quad", 0); // Quad
        hogsStatAddress = GetHubMapAddress("HogsStation", 0); //HogsStation
        classLobbyAddress = GetHubMapAddress("ClassLobby", 0x1318); // Class Lobby
        kingsCrossAddress = GetHubMapAddress("KingsCross", 0x7B); // King's Cross
        foyerAddress = GetHubMapAddress("Foyer", 0); // Foyer
        byte* y6GhostPtr = HubHandler.GhostPathBaseAddress + 0x34;
        byte* y6GhostPtr2 = HubHandler.GhostPathBaseAddress + 0x35;

        AdjustLeakyCauldron();
        AdjustHogsPath();
        CompleteStartingGhostLevels();
        AdjustFoyer();
        // If applicable, update Wilderness and Quad so tokens spawn and invisible barriers are gone
        if (year == 7 || year == 8)
        {
            AdjustWilderness();
            AdjustQuad();
        }
        // Adjust Hogsmeade Station if the player enters it and the suitcases are still blocking the exit in other years
        if (year != 6 && (!Mod.LHP2_Archipelago!.IsLocationChecked(1016) || (*y6GhostPtr & (1 << 2)) == 0))
        {
            AdjustHogsStat();
        }
        // Adjust the Y5 Charms loading zone if the player hasn't completed reducto (so they can enter diffindo again if needed)
        if (year != 6 && (!Mod.LHP2_Archipelago!.IsLocationChecked(1021) || (*y6GhostPtr2 & (1 << 0)) == 0))
        {
            AdjustClassLobby();
        }
        // Add the train back since it isn't there in Y7
        if (year != 7)
        {
            AdjustKingsCross();
        }
    }

    // Helper function to ensure that First Level loading zones aren't active in Leaky Cauldron
    private static unsafe void AdjustLeakyCauldron()
    {
        if (leaky2LondonAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("Leaky Cauldron Save info hasn't been written yet.");
            return;
        }
        Game.PrintToLog($"Turning Off Leaky Cutscenes: Address is 0x{(nuint)leaky2LondonAddress:X}");
        *leaky2LondonAddress |= 1 << 0; // Ensure Normal Loading Zones are on
        *leaky2LondonAddress |= 1 << 1; // Ensure Normal Loading Zones are on

        *leaky2LondonAddress &= unchecked((byte)~(1 << 2)); // Clear Out of Retirement Cutscene
        *leaky2LondonAddress &= unchecked((byte)~(1 << 3)); // Clear Seven Harrys Cutscene

        byte* leaky2LondonAddress2 = leaky2LondonAddress + 0x2;
        Game.PrintToLog($"Address of second Leaky Cauldron flag is 0x{(nuint)leaky2LondonAddress2:X}");
        *leaky2LondonAddress2 &= unchecked((byte)~(1 << 4)); // Clear Thief's Downfall Cutscene
    }

    // Helper function to ensure that Y5/6 Hogwarts Intro Cutscenes don't carry over and that HogsPath opens up
    // TODO: find the objects that make up the gate lock so we don't have a floating lock
    private static unsafe void AdjustHogsPath()
    {
        if (hogsPathAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("HogsPath Save info hasn't been written yet.");
            return;
        }
        hogsPathAddress += 0xA2; //Move Flag to Gate 1
        *hogsPathAddress = 4;
        hogsPathAddress += 0x1; // Move Flag to Gate 1 part 2
        *hogsPathAddress |= 1 << 4;
        *hogsPathAddress |= 1 << 5;
        hogsPathAddress += 0xD; // Move Flag to Gate 2 part 1
        *hogsPathAddress = 4;
        hogsPathAddress += 0x1; // Move Flag to Gate 2 part 2
        *hogsPathAddress |= 1 << 4;
        *hogsPathAddress |= 1 << 5;
        hogsPathAddress += 0x19DF; // Adjust to the correct address for the loading zone flags
        Game.PrintToLog($"Updating HogsPath Flags; Address is 0x{(nuint)hogsPathAddress:X}");
        *hogsPathAddress &= unchecked((byte)~(1 << 5)); // Clear Y5 Hogs Intro Cutscene
        *hogsPathAddress |= 1 << 7; // Clear Y6 Hogs Intro Cutscene (Note that this one is inverted logic in-game)
        hogsPathAddress -= 0x359; // Adjust to open hogsmeade
        Game.PrintToLog($"Adjusted HogsPath Address to 0x{(nuint)hogsPathAddress:X}");
        *hogsPathAddress |= 1 << 2; // Open the gate to Hogsmeade
    }

    // Helper function to remove the invisible walls in the wilderness and make sure the Xeno token spawns
    private static unsafe void AdjustWilderness()
    {
        if (wildernessAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("Wilderness Save info hasn't been written yet.");
            return;
        }
        Game.PrintToLog("Updating Wilderness Flags");
        byte* invisibleWallFlag = wildernessAddress + 0x2905;
        Game.PrintToLog($"Address of Wilderness Invisible Wall Flag is 0x{(nuint)invisibleWallFlag:X}");
        *invisibleWallFlag &= unchecked((byte)~(1 << 2)); // Turn off the invisible wall by the rock pile
        invisibleWallFlag += 0x03;
        *invisibleWallFlag &= unchecked((byte)~(1 << 6)); // Turn off the invisible wall by the snowman
        invisibleWallFlag += 0x16;
        *invisibleWallFlag &= unchecked((byte)~(1 << 2)); // Turn off the invisible wall by the wrecking ball  
        invisibleWallFlag += 0x03;
        *invisibleWallFlag &= unchecked((byte)~(1 << 6)); // Turn off the invisible wall by the Lake  
        byte* xenoTokenFlag = wildernessAddress + 0x2D8F;
        Game.PrintToLog($"Address of Wilderness Xenophilius Token Flag is 0x{(nuint)xenoTokenFlag:X}");
        *xenoTokenFlag |= 1 << 3; // Ensure the Xenophilius token spawns
        xenoTokenFlag -= 0x12;
        *xenoTokenFlag |= 1 << 3; // Ensure the Token has a hitbox
    }

    // Helper function to ensure that McGonagall token spawns
    private static unsafe void AdjustQuad()
    {
        if (quadAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("Quad Save info hasn't been written yet.");
            return;
        }
        Game.PrintToLog($"Updating Quad Flags; Address is 0x{(nuint)quadAddress:X}");
        byte* mcgBlackFlag = quadAddress + 0x20B6;
        Game.PrintToLog($"Address of Quad McGonagall Flag is 0x{(nuint)mcgBlackFlag:X}");
        *mcgBlackFlag |= 1 << 1; // Ensure the Token is spawned
        mcgBlackFlag += 0x1A;
        *mcgBlackFlag |= 1 << 1; // Ensure the Token has a hitbox
    }

    // Helper function to remove the suitcases and invisible wall from Hogsmeade station if needed
    private static unsafe void AdjustHogsStat()
    {
        if (hogsStatAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("Hogs Station Save info hasn't been written yet.");
            return;
        }
        Game.PrintToLog($"Updating Hogs Station Flags. Address is 0x{(nuint)hogsStatAddress:X}");
        hogsStatAddress += 0xA3;
        Game.PrintToLog($"First Hogs Station Flag Address is 0x{(nuint)hogsStatAddress:X}");
        *hogsStatAddress = 82; // Suitcase 1
        hogsStatAddress += 7;
        *hogsStatAddress = 82; // Suitcase 2
        hogsStatAddress += 7;
        *hogsStatAddress = 82; // Suitcase 3
        hogsStatAddress += 7;
        *hogsStatAddress = 64; // Invisible wall 1 (side wall)
        hogsStatAddress += 7;
        *hogsStatAddress = 64; // Invisible wall 2 (front wall)
        hogsStatAddress += 5;
        *hogsStatAddress = 225; // Small Suitcase 1
        hogsStatAddress += 31;
        *hogsStatAddress = 225; // Small Suitcase 2
        hogsStatAddress += 31;
        *hogsStatAddress = 225; // Small Suitcase 3
        hogsStatAddress += 47;
        *hogsStatAddress &= unchecked((byte)~(1 << 2)); // Suitcase 4
        hogsStatAddress += 3;
        *hogsStatAddress &= unchecked((byte)~(1 << 6)); // Suitcase 5
        hogsStatAddress += 2;
        *hogsStatAddress &= unchecked((byte)~(1 << 2)); // Suitcase 6
    }

    // Helper function to ensure that the player can enter into diffindo lesson before reducto lesson
    private static unsafe void AdjustClassLobby()
    {
        if (classLobbyAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("Class Lobby Save info hasn't been written yet.");
            return;
        }

        *classLobbyAddress &= unchecked((byte)~(1 << 6)); // Clear out being forced into reducto lesson
    }

    // Helper function to add back the train
    private static unsafe void AdjustKingsCross()
    {
        if (kingsCrossAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("Kings Cross Save info hasn't been written yet.");
            return;
        }
        Game.PrintToLog($"Updating Kings Cross Flags. Address is 0x{(nuint)kingsCrossAddress:X}");

        for (int i = 0; i < 7; i++)
        {
            *kingsCrossAddress |= 1 << 6; // Bit 6 makes each compartment visible
            kingsCrossAddress += 0x7; // Adjust the address to each train compartment
        }
        kingsCrossAddress += 0x521;
        *kingsCrossAddress = 255; // Resets the train position after using it
        kingsCrossAddress += 0x8;
        *kingsCrossAddress = 255; // Makes the train move when using it
        kingsCrossAddress += 0x202;
        *kingsCrossAddress = 255; // Make the train appear in all years but 7
    }

    // Helper function to open the lower foyer door & to open the door to Great Hall Lobby
    private static unsafe void AdjustFoyer()
    {
        if (foyerAddress == MapFlagsBaseAddress + 0x40)
        {
            Game.PrintToLog("Foyer Address Save Info hasn't been written yet.");
            return;
        }
        Game.PrintToLog($"Updating Foyer Flags. Address is 0x{(nuint)foyerAddress:X}");

        // Open the lower foyer door
        // Adjust the first wall's 4 flags
        byte* foyerDoorAddress = foyerAddress + 0x86;
        *foyerDoorAddress |= 1 << 5;
        foyerDoorAddress += 1;
        *foyerDoorAddress = 88;
        foyerDoorAddress += 1;
        *foyerDoorAddress = 204;
        foyerDoorAddress += 1;
        *foyerDoorAddress &= unchecked((byte)~(1 << 3));

        //Adjust the second wall's 4 flags
        foyerDoorAddress += 0x4;
        *foyerDoorAddress |= 1 << 5;
        foyerDoorAddress += 1;
        *foyerDoorAddress = 88;
        foyerDoorAddress += 1;
        *foyerDoorAddress = 204;
        foyerDoorAddress += 1;
        *foyerDoorAddress &= unchecked((byte)~(1 << 3));

        //Adjust the third wall's 4 flags
        foyerDoorAddress += 0x4;
        *foyerDoorAddress |= 1 << 5;
        foyerDoorAddress += 1;
        *foyerDoorAddress = 88;
        foyerDoorAddress += 1;
        *foyerDoorAddress = 204;
        foyerDoorAddress += 1;
        *foyerDoorAddress &= unchecked((byte)~(1 << 3));

        // Remove the invisible wall
        foyerDoorAddress += 0x166B;
        *foyerDoorAddress |= 1 << 1;

        // Remove Door to Great Hall
        byte* greatHallDoorAddress = foyerAddress + 0x61;
        *greatHallDoorAddress = 76;
        greatHallDoorAddress += 0x1D0;
        *greatHallDoorAddress = 254;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress |= 1 << 0;
        greatHallDoorAddress += 3;
        *greatHallDoorAddress = 5;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress = 45;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress &= unchecked((byte)~(1 << 6));
        greatHallDoorAddress += 1;
        *greatHallDoorAddress = 192;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress = 84;
        greatHallDoorAddress += 6;
        *greatHallDoorAddress |= 1 << 5;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress = 84;
        greatHallDoorAddress += 5;
        *greatHallDoorAddress &= unchecked((byte)~(1 << 6));
        greatHallDoorAddress += 1;
        *greatHallDoorAddress |= 1 << 5;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress = 84;
        greatHallDoorAddress += 5;
        *greatHallDoorAddress &= unchecked((byte)~(1 << 6));
        greatHallDoorAddress += 1;
        *greatHallDoorAddress |= 1 << 5;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress = 84;
        greatHallDoorAddress += 5;
        *greatHallDoorAddress &= unchecked((byte)~(1 << 6));
        greatHallDoorAddress += 1;
        *greatHallDoorAddress |= 1 << 5;
        greatHallDoorAddress += 1;
        *greatHallDoorAddress = 84;
    }

    // This is the helper function to search the save file container for the address where the wanted map is stored
    private static unsafe byte* GetHubMapAddress(string mapName, int offset)
    {
        const int sectionSize = 300000; // Number of bytes we want to search in the Map container
        byte* startingAddress = MapFlagsBaseAddress + 0x40;
        byte* returningAddress = startingAddress;
        byte[] sectionHeader = Encoding.ASCII.GetBytes(mapName); // Map names are ASCII coded in the save file with flags right after

        // We use a nested for loop to search through the container for the map name
        for (int i = 0; i < sectionSize - sectionHeader.Length; i++)
        {
            // Check if the current address matches the section header
            bool isMatch = true;
            for (int j = 0; j < sectionHeader.Length; j++)
            {
                if (*(startingAddress + i + j) != sectionHeader[j])
                {
                    isMatch = false;
                    break;
                }
            }
            if (isMatch)
            {
                returningAddress = startingAddress + i + offset;
                break;
            }
        }
        Game.PrintToLog($"Map Flags Address for {mapName} is 0x{(nuint)returningAddress:X}");
        return returningAddress;
    }
}

class Map
{
    public string Name { get; private set; }
    public unsafe ushort* MapConstantAddress { get; private set; }
    public unsafe byte* EntranceConstantAddress { get; private set; }
    public ushort MapConstant { get; private set; }
    public byte EntranceConstant { get; private set; }

    public unsafe Map(string name, ushort* baseMapConstantAddress)
    {
        Name = name;
        MapConstantAddress = baseMapConstantAddress;
        EntranceConstantAddress = (byte*)(baseMapConstantAddress + 2);
        MapConstant = *baseMapConstantAddress;
        EntranceConstant = *EntranceConstantAddress;
    }

    public unsafe void UpdateMap(ushort map)
    {
        *MapConstantAddress = map;
    }

    public unsafe void UpdateEntrance(byte entrance)
    {
        *EntranceConstantAddress = entrance;
    }

    public unsafe void RestoreMap()
    {
        *MapConstantAddress = MapConstant;
        *EntranceConstantAddress = EntranceConstant;
    }
}
