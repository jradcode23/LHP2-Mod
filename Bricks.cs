namespace LHP2_Archi_Mod;

public class Bricks
{
    private static unsafe readonly byte* goldBrickBaseAddress = (byte*)(*(int*)(Mod.BaseAddress + 0xC54554));
    private static unsafe readonly byte* RedBrickBaseAddress = (byte*)(*(int*)(Mod.BaseAddress + 0xC575F4));
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
            Console.WriteLine($"Can't Unlock Character Purchase, null pointer at 0x{(nuint)ptr:X}");
            return;
        } 
        *ptr = goldBrickCount;
    }
    
    public static unsafe void ResetGoldBrickCount()
    {
        goldBrickCount = 0;
    }
}