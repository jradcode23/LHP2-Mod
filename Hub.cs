namespace LHP2_Archi_Mod;

public class Hub
{
    private static unsafe readonly byte* hubBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC5B3B4);
    private static unsafe readonly byte* goldBrickBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC54554);
    private static unsafe readonly byte* RedBrickPurchBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC575F4);
    private static unsafe readonly byte* SpellBaseAddress = (byte*)(Mod.BaseAddress + 0xB06AB0);

    [Flags]
    public enum BitMask
    {
        None = 0,
        SpecialFlag = 1 << 1,
        Entered = 1 << 2,
        RedBrick = 1 << 4,
        GoldBrick = 1 << 5,
        StudentInPeril = 1 << 6,
    }

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
        {0x840, 7}, // Y5 Hogsmeade Station
        {0xAA, 8}, // Y8 Hogwarts Path
        {0x51E, 8}, // Y7 Hogwarts Path
        {0x872, 8}, // Y6 Hogwarts Path
        {0xD3A, 8}, // Y5 Hogwarts Path
        {0xB6, 9}, // Y8 Herbology Greenhouse
        {0x52A, 9}, // Y7 Herbology Greenhouse
        {0x87E, 9}, // Y6 Herbology Greenhouse
        {0xC2, 10}, // Y8 Training Grounds
        {0x536, 10}, // Y7 Training Grounds
        {0x88A, 10}, // Y6 Training Grounds
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

    public static int GetHubID(int offset)
    {
        offset *= 4;
        offset += 2;
        return hubOffsets.TryGetValue(offset, out int sipID) ? sipID : -1;
    }

    public static unsafe void UnlockHubGB(int ID)
    {
        var kvp = hubOffsets.FirstOrDefault(k => k.Value == ID);
        if (kvp.Key == 0 && kvp.Value == 0)
        {
            Console.WriteLine($"[Hub] Could not find Hub RB offset for ID {ID}");
            return;
        }
        byte* ptr = hubBaseAddress + (nuint)kvp.Key;;
        if (ptr == null || hubBaseAddress == null) 
        {
            Console.WriteLine($"[Hub] Can't Unlock Hub RB, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.GoldBrick;
    }

    public static unsafe void UnlockHubRB(int ID)
    {
        var kvp = hubOffsets.FirstOrDefault(k => k.Value == ID);
        if (kvp.Key == 0 && kvp.Value == 0)
        {
            Console.WriteLine($"[Hub] Could not find Hub RB offset for ID {ID}");
            return;
        }
        byte* ptr = hubBaseAddress + (nuint)kvp.Key;;
        if (ptr == null || hubBaseAddress == null) 
        {
            Console.WriteLine($"[Hub] Can't Unlock Hub RB, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.RedBrick;
    }

    public static unsafe void UnlockHubSIP(int ID)
    {
        var kvp = hubOffsets.FirstOrDefault(k => k.Value == ID);
        if (kvp.Key == 0 && kvp.Value == 0)
        {
            Console.WriteLine($"[Hub] Could not find Hub SIP offset for ID {ID}");
            return;
        }
        byte* ptr = hubBaseAddress + (nuint)kvp.Key;;
        if (ptr == null || hubBaseAddress == null) 
        {
            Console.WriteLine($"[Hub] Can't Unlock Hub SIP, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.StudentInPeril;
    }

    public static unsafe void ResetHub()
    {
        foreach (var kvp in hubOffsets)
        {
            byte* ptr = hubBaseAddress + (nuint)kvp.Key;

            if (ptr == null)
            {
                Console.WriteLine($"Hub pointer invalid at offset 0x{kvp.Key:X}");
                continue;
            }
            *ptr &= unchecked((byte)~(byte)BitMask.RedBrick);
            *ptr &= unchecked((byte)~(byte)BitMask.StudentInPeril);
        }
    }

    public static unsafe void ReceivedRedBrickUnlock(int id)
    {
        int byteOffset = id / 8;
        int bitOffset = id % 8;

        byte* ptr = RedBrickPurchBaseAddress + byteOffset;
        if (ptr == null || RedBrickPurchBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock Red Brick, null pointer at 0x{(nuint)ptr:X}");
            return;
        } 
        *ptr |= (byte)(1 << bitOffset);
    }

    public static unsafe void ResetRedBrickUnlock()
    {
        for(int i = 0; i < 3; i++)
        {
            byte* ptr = RedBrickPurchBaseAddress + i;
            *ptr = 0;
        }
    }
    
    //TODO: Consider making Gold Bricks not static
    public static byte goldBrickCount { get; private set; } = 0;

    public static void ReceivedGoldBrick ()
    {
        goldBrickCount ++;
        
    }

    public static unsafe void GetGoldBrickCount ()
    {
        byte* ptr = goldBrickBaseAddress + 0x01A7A;
        if (ptr == null || goldBrickBaseAddress == null) 
        {
            Console.WriteLine($"Can't Show Gold Bricks, null pointer at 0x{(nuint)ptr:X}");
            return;
        } 
        *ptr = goldBrickCount;
    }
    
    public static void ResetGoldBrickCount()
    {
        goldBrickCount = 0;
    }

    public static unsafe void UnlockSpell(int id)
    {

        int byteOffset = id / 8;
        int bitOffset = id % 8;

        byte* ptr = SpellBaseAddress + byteOffset;

        if (ptr == null)
        {
            Console.WriteLine("SpellBaseAddress: null pointer");
            return;
        }
        *ptr |= (byte)(1 << bitOffset);
    }

    public static unsafe void ResetSpells()
    {
        for(int i = 0; i < 4; i++)
        {
            byte* ptr = SpellBaseAddress + i;
            *ptr = 0;
        }

        // Set the Default Spells
        UnlockSpell(0); //Wingardium Leviosa
        UnlockSpell(20); //Pets
        UnlockSpell(21); //Invisibility Cloak
        UnlockSpell(22); //Avada
        UnlockSpell(24); //Lumos Part 1
        UnlockSpell(25); //Lumos Part 2
        UnlockSpell(31); //Unknown Spell
    }

}