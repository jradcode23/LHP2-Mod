namespace LHP2_Archi_Mod;

public class Bricks
{
    private static unsafe readonly byte* goldBrickBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC54554);
    private static unsafe readonly byte* RedBrickBaseAddress = (byte*)*(int*)(Mod.BaseAddress + 0xC575F4);
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

    public static unsafe void ReceivedRedBrickUnlock(int id)
    {
        int byteOffset = id / 8;
        int bitOffset = id % 8;

        byte* ptr = RedBrickBaseAddress + byteOffset;
        if (ptr == null || RedBrickBaseAddress == null) 
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
            byte* ptr = RedBrickBaseAddress + i;
            *ptr = 0;
        }
    }
}