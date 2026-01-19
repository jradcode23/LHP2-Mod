using System.Numerics;

namespace LHP2_Archi_Mod;

public class CharacterHandler
{
    private static unsafe readonly byte* characterBaseAddress = *(byte**)(Mod.BaseAddress + 0xC546E4);
    private static readonly byte TokenOffset = 0xE;
    private static readonly byte unlockOffset = 0xE4;

    private static readonly Dictionary<int, int> characterMap = new()
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
        // {33, 0xD7}, // Harry Potter
        // {34, 0xD8}, // Hermione Granger
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
        // {51, 0xFC}, // Custom A
        // {52, 0xFD}, // Custom B
        // {53, 0xFE}, // Custom C
        // {54, 0xFF}, // Custom D
        // {55, 0x100}, // Custom E
        // {56, 0x101}, // Custom G
        // {57, 0x102}, // Custom H
        // {58, 0x103}, // Custom I
        // {59, 0x104}, // Custom J
        {60, 0x10A}, // Harry (Pyjamas)
        {61, 0x10F}, // Moaning Myrtle
        {62, 0x11C}, // James Potter (Ghost)
        {63, 0x120}, // Mad-Eye Moody
        {64, 0x121}, // Hannah Abbot
        // {65, 0x122}, // Custom F
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
        {84, 0x139}, // Grindelwald (Old)
        {85, 0x13B}, // Gregorovitch
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
        {98, 0x150}, // Molly (Apron)
        {99, 0x151}, // Crabbe (Jumper)
        {100, 0x152}, // Draco (Sweater)
        {101, 0x158}, // Goyle (Jumper)
        {102, 0x162}, // Milk Man
        {103, 0x165}, // SLytherin Twin #2
        {104, 0x167}, // Hermione (Mafalda)
        {105, 0x168}, // Ministry Guard
        {106, 0x169}, // Harry (Winter)
        {107, 0x16B}, // Arthur (Torn Suit)
        {108, 0x16C}, // Fred (Winter)
        {109, 0x16D}, // Cho (Winter)
        {110, 0x16E}, // George (Winter)
        {111, 0x16F}, // Hermione (Scarf)
        {112, 0x173}, // Luna (Blue Jumper)
        {113, 0x174}, // Fred (Owls)
        {114, 0x176}, // Fred (Pyjamas)
        {115, 0x177}, // George (Owls)
        {116, 0x178}, // George (Pyjamas)
        {117, 0x17A}, // Hermione (Jumper)
        {118, 0x17C}, // Fudge (Wizengamot)
        {119, 0x17D}, // Dudley Dursley
        {120, 0x17E}, // Dudley's Gang Member
        {121, 0x183}, // Harry (Albert Runcorn)
        {122, 0x184}, // Harry (Brown Jacket)
        {123, 0x186}, // Dumbledore (Cursed)
        {124, 0x187}, // Lucius (Death Eater)
        {125, 0x189}, // Luna Lovegood
        {126, 0x18A}, // Umbridge (Wizengamot)
        {127, 0x18B}, // Dolohov (Workman)
        {128, 0x193}, // Michael Corner
        {129, 0x194}, // Dean (Winter)
        {130, 0x196}, // Arthur (Cardigan)
        {131, 0x198}, // Luna (Pink Dress)
        {132, 0x19A}, // Marietta Edgecombe
        {133, 0x19B}, // Dumbledore (Young)
        {134, 0x19C}, // Slughorn (Young)
        {135, 0x19D}, // Slughorn (Pajamas)
        {136, 0x19E}, // Lily (Young Casual)
        {137, 0x19F}, // Ginny (Dress)
        {138, 0x1A0}, // Ginny (Pyjamas)
        {139, 0x1A3}, // Blaise (Black Shirt)
        {140, 0x1A5}, // Cormac (Suit)
        {141, 0x1A6}, // Muggle Orphan
        {142, 0x1A7}, // Luna (Overalls)
        {143, 0x1A8}, // Molly Weasley
        {144, 0x1AC}, // Hermione (Cardigan)
        {145, 0x1AE}, // Luna (Yellow Dress)
        {146, 0x1B0}, // Dudley (Shirt)
        {147, 0x1B3}, // Bill Weasley (Wedding)
        {148, 0x1B4}, // Fleur Delacour
        {149, 0x1B5}, // Hermione (Red Dress)
        {150, 0x1BD}, // Mrs Figg
        {151, 0x1C0}, // Harry (Locket)
        {152, 0x1C2}, // Slytherin Twin #1
        {153, 0x1C7}, // Mrs Cole
        {154, 0x1CA}, // Hermione (Ministry)
        {155, 0x1E6}, // Arthur (Suit)
        {156, 0x1E8}, // Harry (Christmas)
        {157, 0x1F1}, // Ernie Prang
        {158, 0x1FC}, // Professor Snape
        {159, 0x1FE}, // Neville Longbottom
        // {160, 0x1FF}, // Ron Weasley
        {161, 0x201}, // Ron (Quidditch)
        {162, 0x207}, // Vernon Dursley
        {163, 0x20B}, // Tom Riddle
        {164, 0x20C}, // Sirius Black
        {165, 0x20D}, // Remus Lupin
        {166, 0x20E}, // Wormtail
        {167, 0x20F}, // Rita Skeeter
        {168, 0x212}, // Padma Patil
        {169, 0x214}, // Station Guard
        {170, 0x215}, // Professor Binns
        {171, 0x223}, // Penelope Clearwater
        {172, 0x224}, // Susan Bones
        {173, 0x226}, // Nymphadora Tonks
        {174, 0x227}, // Pius Thicknesse
        {175, 0x229}, // Reg Cattermole
        {176, 0x22A}, // Regulus Black
        {177, 0x22C}, // Rufus Scrimgeour
        {178, 0x22E}, // Scabior
        {179, 0x232}, // Xenophilius Lovegood
        {180, 0x233}, // Yaxley
        {181, 0x234}, // Zacharias Smith
        {182, 0x239}, // Waitress (Treats)
        {183, 0x23B}, // Lord Voldemort
        {184, 0x23D}, // Ron (Blue Pyjamas)
        {185, 0x23E}, // Neville (Cardigan)
        {186, 0x23F}, // Neville (Pyjamas)
        {187, 0x240}, // Percy Weasley
        {188, 0x245}, // Sirius (Azkaban)
        {189, 0x246}, // Mrs. Black
        {190, 0x247}, // Xenophilius (Luna)
        {191, 0x248}, // Ron (Reg Cattermole)
        {192, 0x24E}, // Tonks (Pink Coat)
        {193, 0x24F}, // Ron (Winter Clothes)
        {194, 0x251}, // Snape (Underwear)
        {195, 0x254}, // Thorfinn Rowle
        {196, 0x255}, // Petunia Dursley
        {197, 0x25B}, // Neville (Tank Top)
        {198, 0x25E}, // Neville (Winter)
        {199, 0x260}, // Parvati Patil
        {200, 0x261}, // Ron (Red Sweater)
        {201, 0x262}, // Olivander
        {202, 0x263}, // Seamus (Winter)
        {203, 0x266}, // Ron (Underwear)
        {204, 0x267}, // Ron (Wedding)
        {205, 0x268}, // Waitress (Luchino)
        {206, 0x269}, // Petunia (Green Coat)
        {207, 0x26A}, // Seamus Finnigan
        {208, 0x26D}, // Snatcher
        {209, 0x272}, // Ron (Green Shirt)
        {210, 0x275}, // Neville (Waiter)
        {211, 0x276}, // Xenophilius (Wedding)
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

    public static unsafe int GetHubTokenItemID(IntPtr address, int offset)
    {
        int bitIndex = BitOperations.TrailingZeroCount(offset);
        long byteIndex = address - (IntPtr)(characterBaseAddress + TokenOffset); 
        if (byteIndex < 0) 
        {
            return -1;
        }

        int value = (int)(byteIndex * 8 + bitIndex );
        Console.WriteLine($"Hub Token Value is: {value:X}");
        var kvp = characterMap.FirstOrDefault(k => k.Value == value);
        return kvp.Equals(default(KeyValuePair<int,int>)) ? -1 : kvp.Key;
    }

    public static int GetLevelTokenItemID(int ID)
    {
        int bx = ID & 0xFFFF;
        bx -= 0x70; // There is a base 0x70 offset
        Console.WriteLine($"Level Token bx is: {bx}");
        var kvp = characterMap.FirstOrDefault(k => k.Value == bx);
        return kvp.Equals(default(KeyValuePair<int,int>)) ? -1 : kvp.Key;
    }

    public static unsafe int GetPurchaseCharacterID(IntPtr address, int offset)
    {
        long byteIndex = address + offset + 0x74 - (IntPtr)(characterBaseAddress + unlockOffset); 
        if (byteIndex < 0) 
        {
            return -1;
        }

        int value = (int)byteIndex;
        var kvp = characterMap.FirstOrDefault(k => k.Value == value);
        return kvp.Equals(default(KeyValuePair<int,int>)) ? -1 : kvp.Key;
    }
}