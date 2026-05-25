using Archipelago.MultiClient.Net.Models;

namespace LHP2_Archi_Mod;

public class Shops
{
    private static unsafe int* GoldBrickShopBaseAddress => (int*)(Mod.BaseAddress + 0x94B13C);
    private static unsafe int* RedBrickShopBaseAddress => (int*)(Mod.BaseAddress + 0x94CEE4);
    private static unsafe int* JokeShopBaseAddress => (int*)(Mod.BaseAddress + 0x94E514);
    private static unsafe byte* SingleSlotCharacterBaseAddress => *(byte**)(Mod.BaseAddress + 0xADBF90);
    private static unsafe byte* MultiSlotCharacterBaseAddress => *(byte**)(Mod.BaseAddress + 0xAE157C);
    private static unsafe uint ShopTextAddress => (uint)(HintSystem.HintTextBaseAddress + 0x274);
    private static IntPtr[] OriginalJokeShopPointers = new IntPtr[19];

    // This helper function is used to reduce the shop prices based on what is stated in the Slot Data
    public static unsafe void SetShopPrices(int multiplier)
    {
        // Failsafe in case the value doesn't get read properly or if we can't read slot data
        if (multiplier < 1 || multiplier > 10)
        {
            Game.PrintToLog($"Multiplier Amount is: {multiplier} which is invalid. Changing amount to 1.");
            multiplier = 1;
        }

        // Validate base addresses before dereferencing
        if (GoldBrickShopBaseAddress == null || RedBrickShopBaseAddress == null ||
            SingleSlotCharacterBaseAddress == null || MultiSlotCharacterBaseAddress == null)
        {
            Game.PrintToLog("[ShopPrices] One or more shop base addresses are null, cannot set shop prices");
            return;
        }

        // Set Gold Brick Shop prices
        *GoldBrickShopBaseAddress /= multiplier;

        // Set Red Brick Shop prices
        for (int i = 0; i < 24; i++)
        {
            *(RedBrickShopBaseAddress + i * 0x6) /= multiplier;
        }

        // Character Prices have 2 different address arrays depending if 1 or more characters are included in the same shop slot
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

    // Joke shop prices are set upon opening the shop. As such, we have to run this separately from the other shop price function.
    public static unsafe void SetJokeShopPrices(int multiplier)
    {
        // Failsafe in case we can't read the slot data
        if (multiplier < 1 || multiplier > 10)
        {
            Game.PrintToLog($"Multiplier Amount is: {multiplier} which is invalid. Changing amount to 1.");
            multiplier = 1;
        }

        // Validate base address before dereferencing
        if (JokeShopBaseAddress == null)
        {
            Game.PrintToLog("[ShopPrices] Joke shop base address is null, cannot set prices");
            return;
        }

        // Set Joke Shop prices
        for (int i = 0; i < 19; i++)
        {
            *(JokeShopBaseAddress + i * 0xB) /= multiplier;
        }
    }

    // To make sure the multiplier doesn't compound, we reverse the Joke shop effects when leaving the shop
    public static unsafe void ReverseJokeShopPriceChanges(int multiplier)
    {
        // Failsafe
        if (multiplier < 1 || multiplier > 10)
        {
            Game.PrintToLog($"Multiplier Amount is: {multiplier} which is invalid. Changing amount to 1.");
            multiplier = 1;
        }

        // Validate base address before dereferencing
        if (JokeShopBaseAddress == null)
        {
            Game.PrintToLog("[ShopPrices] Joke shop base address is null, cannot reverse prices");
            return;
        }

        // Reverse Joke Shop prices
        for (int i = 0; i < 19; i++)
        {
            *(JokeShopBaseAddress + i * 0xB) *= multiplier;
        }
    }

    public static unsafe void SetJokeShopPointers()
    {
        int* firstPTR = JokeShopBaseAddress + 0x8;
        for (int i = 0; i < 19; i++)
        {
            OriginalJokeShopPointers[i] = new IntPtr(*(firstPTR + i * 0xB));
        }
        for (int i = 0; i < 19; i++)
        {
            Game.PrintToLog($"Original Joke Shop Pointer {i}: 0x{(nuint)OriginalJokeShopPointers[i]:X}");
        }
    }

    public static unsafe void UpdateJokeShopPointers()
    {
        int* firstPTR = JokeShopBaseAddress + 0x8;
        for (int i = 0; i < 19; i++)
        {
            *(firstPTR + i * 0xB) = (int)ShopTextAddress;
        }
    }

    public static unsafe void ResetJokeShopPointers()
    {
        int* firstPTR = JokeShopBaseAddress + 0x8;
        for (int i = 0; i < 19; i++)
        {
            *(firstPTR + i * 0xB) = (int)OriginalJokeShopPointers[i];
        }
    }

    public static void HandleShopText(int itemSelected)
    {
        string message = "Unknown item";
        if (Mod.LHP2_Archipelago == null || Mod.LHP2_Archipelago.ScoutedLocations == null)
        {
            return;
        }

        ScoutedItemInfo item;

        try
        {
            item = Mod.LHP2_Archipelago!.ScoutedLocations[ArchipelagoHandler.gameOffset + itemSelected];
            message = item.Player + "'s " + item.ItemDisplayName;
            if (message.Length > 75)
            {
                message = message[..75]; // Truncate message if it exceeds selected max hint length
            }
            HintSystem.SetMessageText(message, ShopTextAddress);
        }

        catch (Exception ex)
        {
            Game.PrintToLog($"Error retrieving scouted location for ID {itemSelected}: {ex.Message}");
            return;
        }
    }

}