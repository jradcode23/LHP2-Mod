namespace LHP2_Archi_Mod;

public class Character
{
    private static unsafe readonly byte* characterBaseAddress = (byte*)(*(int*)(Mod.BaseAddress + 0xC546E4));
    private static readonly byte TokenOffset = 0xE;
    private static readonly byte unlockOffset = 0xE4;

    private static readonly Dictionary<int, int> characterMap = new Dictionary<int, int>
    {
        {0, 0x3}, // Hagrid
        {1, 0xB}, // Fang
        {2, 0x45}, // Hagrid Wedding
        {3, 0xA2}, // Professor Flitwick
        {4, 0xA5}, // Madam Malkin
        {5, 0xA7}, // Dobby
        {6, 0xA9}, // Kreacher
        {7, 0xAA}, // Tom Riddle Orphanage
        {8, 0xAB}, // Bogrod
        {9, 0xAD}, // Mundungus Fletcher
        {10, 0xAF}, // Griphook
        {11, 0xB4}, // Professor McGonagall
        {12, 0xB5}, // Madam Irma Pince
        {13, 0xB6}, // Professor Sprout
        {14, 0xB8}, // Madam Pomfrey
        {15, 0xB9}, // Professor Trelawny
        {16, 0xBB}, // Madam Rosmerta
        {17, 0xBC}, // Fat Friar
        {18, 0xBD}, // The Grey Lady
        {19, 0xBF}, // Fat Lady
        {20, 0xC2}, // Hermione (Ball Gown)
        {21, 0xC4}, // Bellatrix Lestrange
        {22, 0xC5}, // Emmeline Vance
        {23, 0xC6}, // Narcissa Malfoy
        {24, 0xC7}, // McGonagall (Pyjamas)
        {25, 0xCB}, // Mary Cattermole
        {26, 0xCE}, // McGonagall (Black)
        {27, 0xCF}, // Hermione (Gringotts)
        {28, 0xD1}, // Professor Grubbly-Plank
        {29, 0xD2}, // Bellatrix (Azkaban)
        {30, 0xD4}, // Death Eater
        {31, 0xD5}, // Dudley (Grey Top)
        {32, 0xD6}, // Professory Dumbledore
        {33, 0xD7}, // Harry Potter
        {34, 0xD8}, // Hermione Granger
        {35, 0xD9}, // Argus Filch
        {36, 0xDD}, // Madam Hooch
        {37, 0xDE}, // Vincent Crabbe
        {38, 0xDF}, // Gregory Goyle
        {39, 0xE8}, // Ginny Weasley
        {40, 0xE9}, // Arthur Weasley
        {41, 0xEA}, // Katie Bell
        {42, 0xEB}, // Lily Potter
        {43, 0xED}, // The Bloody Baron
        {44, 0xEE}, // Cornelius Fudge
        {45, 0xEF}, // Justin Finch-Fletchley
        {46, 0xF1}, // Cho Chang
        {47, 0xF2}, // Dean Thomas
        {48, 0xF7}, // Draco Malfoy
        {49, 0xFA}, // Lucius Malfoy
        {50, 0xFB}, // Draco (Suit)
        {51, 0xFC}, // Custom A
        {52, 0xFD}, // Custom B
        {53, 0xFE}, // Custom C
        {54, 0xFF}, // Custom D
        {55, 0x100}, // Custom E
        {56, 0x101}, // Custom G
        {57, 0x102}, // Custom H
        {58, 0x103}, // Custom I
        {59, 0x104}, // Custom J
        {60, 0x10A}, // Harry (Pyjamas)
        {61, 0x10F}, // Moaning Myrtle
        {62, 0x11C}, // James Potter (Ghost)
        {63, 0x120}, // Mad-Eye Moody
        {64, 0x121}, // Hannah Abbot
        {65, 0x122}, // Custom F
        {66, 0x127}, // Kingsley Shacklebolt
        {67, 0x128}, // Aberforth Dumbledore
        {68, 0x129}, // Albert Runcorn
        {69, 0x12A}, // Alecto Carrow
        {70, 0x12B}, // Amycus Carrow
        {71, 0x12C}, // Anthony Goldstein
        {72, 0x12D}, // Bathilda (Snake)
        {73, 0x12E}, // Blaise Zabini
        {74, 0x12F}, // Charity Burbage
        {75, 0x130}, // Charlie Weasley
        {76, 0x131}, // Cormac McLaggen
        {77, 0x132}, // Dedalus Diggle
        {78, 0x133}, // Dirk Cresswell
        {79, 0x134}, // Antonin Dolohov
        {80, 0x135}, // Dragomir Despard
        {81, 0x136}, // Elphias Doge
        {82, 0x137}, // Fenrir Greyback
        {83, 0x138}, // Grindelwald (Young)
        {84, 0x189}, // Grindelwald (Old)
        {85, 0x18B}, // Gregorovitch
        {86, 0x13D}, // Hestia Jones
        {87, 0x13E}, // Professor Slughorn
        {88, 0x13F}, // James Potter (Young)
        {89, 0x140}, // Lavender Brown
        {90, 0x142}, // Mafalda Hopkirk
        {91, 0x143}, // Marcus Belby
        {92, 0x14A}, // Luna (Purple Coat)
        {93, 0x14B}, // Hermione (Grey Coat)
        {94, 0x14C}, // Harry (Godric's Hollow)
        {95, 0x14D}, // Professor Umbridge
        {96, 0x14E}, // Fred Weasley
        {97, 0x14F}, // George Weasley

        {189, 0x246}, // Mrs. Black

        {212, 0x27F}, // Skeleton
    };

    public static int GetCharacterByteOffset(int id)
    {
        return characterMap[id];
    }

    public static int GetCharacterBitOffset(int id)
    {
        return characterMap[id] % 8;
    }

    public static unsafe void UnlockToken(int id)
    {
        int byteOffset = GetCharacterByteOffset(id) / 8;
        int bitOffset = GetCharacterBitOffset(id);

        byte* ptr = characterBaseAddress + byteOffset + TokenOffset;
        if (ptr == null || characterBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock Character Purchase, null pointer at 0x{(nuint)ptr:X}");
            Console.WriteLine($"Character Base address is: 0x{(nuint)characterBaseAddress:X}");
            Console.WriteLine($"byteOffset is: {byteOffset}");
            Console.WriteLine($"bitOffset is: {bitOffset}");
            return;
        } 
        *ptr |= (byte)(1 << bitOffset);
    }

    public static unsafe void ResetTokens()
    {
    foreach (var kvp in characterMap)
        {
            int bitIndex = kvp.Value;

            int byteOffset = bitIndex / 8;
            int bitOffset = bitIndex % 8;

            byte* ptr = characterBaseAddress + byteOffset + TokenOffset;
            if (ptr == null || characterBaseAddress == null) 
                {
                    Console.WriteLine($"Can't Reset Character Purchase, null pointer at 0x{(nuint)ptr:X}");
                    Console.WriteLine($"Character Base address is: 0x{(nuint)characterBaseAddress:X}");
                    Console.WriteLine($"byteOffset is: {byteOffset}");
                    Console.WriteLine($"bitOffset is: {bitOffset}");
                    return;
                } 
            *ptr &= (byte)~(1 << bitOffset);
        }
    }

    public static unsafe void UnlockCharacter(int id)
    {
        int byteOffset = GetCharacterByteOffset(id);

        byte* ptr = characterBaseAddress + byteOffset + unlockOffset;
        if (ptr == null || characterBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock Character, null pointer at 0x{(nuint)ptr:X}");
            Console.WriteLine($"Character Base address is: 0x{(nuint)characterBaseAddress:X}");
            Console.WriteLine($"byteOffset is: {byteOffset}");
            return;
        } 
        *ptr = 1;
    }

    public static unsafe void ResetUnlocks()
    {
    foreach (var kvp in characterMap)
        {
            int bitIndex = kvp.Value;

            int byteOffset = bitIndex;

            byte* ptr = characterBaseAddress + byteOffset + unlockOffset;
            if (ptr == null || characterBaseAddress == null) 
                {
                    Console.WriteLine($"Can't Unlock Character, null pointer at 0x{(nuint)ptr:X}");
                    Console.WriteLine($"Character Base address is: 0x{(nuint)characterBaseAddress:X}");
                    Console.WriteLine($"byteOffset is: {byteOffset}");

                    return;
                } 
            *ptr = 0;
        }
    }
}