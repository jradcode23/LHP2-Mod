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
        {5, 0xA7}, // Dobby Token

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