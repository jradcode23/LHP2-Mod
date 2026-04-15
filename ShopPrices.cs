namespace LHP2_Archi_Mod;

public class ShopPrices
{
    private static unsafe int* GoldBrickShopBaseAddress => (int*)(Mod.BaseAddress + 0x94B13C);
    private static unsafe int* RedBrickShopBaseAddress => (int*)(Mod.BaseAddress + 0x94CEE4);
    private static unsafe int* JokeShopBaseAddress => (int*)(Mod.BaseAddress + 0x94E514);
    private static unsafe byte* SingleSlotCharacterBaseAddress => *(byte**)(Mod.BaseAddress + 0xADBF90);
    private static unsafe byte* MultiSlotCharacterBaseAddress => *(byte**)(Mod.BaseAddress + 0xAE157C);

    public static unsafe void SetShopPrices(int multiplier)
    {
        if (multiplier < 0 || multiplier > 10)
        {
            Game.PrintToLog($"Multiplier Amount is: {multiplier} which is invalid. Changing amount to 1.");
            multiplier = 1;
        }

        // Set Gold Brick Shop prices
        *GoldBrickShopBaseAddress /= multiplier;

        // Set Red Brick Shop prices
        for (int i = 0; i < 24; i++)
        {
            *(RedBrickShopBaseAddress + i * 0x6) /= multiplier;
        }

        int* singleSlotPricePtr = (int*)(SingleSlotCharacterBaseAddress + 0x4);
        int* multiSlotPricePtr = (int*)(MultiSlotCharacterBaseAddress + 0x64);

        Game.PrintToLog($"Single Slot Character Price Address: 0x{(nuint)singleSlotPricePtr:X}");
        Game.PrintToLog($"Multi Slot Character Price Address: 0x{(nuint)multiSlotPricePtr:X}");

        // Set Single Slot Character prices
        for (int i = 0; i < 0x72; i++)
        {
            *(singleSlotPricePtr + i * 0x8) /= multiplier;
        }

        // Set Multi Slot Character prices
        for (int i = 0; i < 0xD2; i++)
        {
            *(multiSlotPricePtr + i * 0x8) /= multiplier;
        }
    }

    public static unsafe void SetJokeShopPrices(int multiplier)
    {
        if (multiplier < 0 || multiplier > 10)
        {
            Game.PrintToLog($"Multiplier Amount is: {multiplier} which is invalid. Changing amount to 1.");
            multiplier = 1;
        }
        // Set Joke Shop prices
        for (int i = 0; i < 19; i++)
        {
            *(JokeShopBaseAddress + i * 0xB) /= multiplier;
        }
    }

    public static unsafe void ReverseJokeShopPriceChanges(int multiplier)
    {
        if (multiplier < 0 || multiplier > 10)
        {
            Game.PrintToLog($"Multiplier Amount is: {multiplier} which is invalid. Changing amount to 1.");
            multiplier = 1;
        }
        // Reverse Joke Shop prices
        for (int i = 0; i < 19; i++)
        {
            *(JokeShopBaseAddress + i * 0xB) *= multiplier;
        }
    }
}