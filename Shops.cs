using Archipelago.MultiClient.Net.Models;

namespace LHP2_Archi_Mod;

public class Shops
{
    private const int RedBrickShopCount = 24;
    private const int JokeShopCount = 19;
    private const int CharacterShopCount = 213;
    private const int SingleSlotCharacterCount = 0x72;
    private const int MultiSlotCharacterCount = 0xD2;
    private const int RedBrickShopGap = 0x6;
    private const int CharacterPriceGap = 0x8;
    private const int JokeShopGap = 0xB;

    private static readonly nuint[] RedBrickShopOffsets =
    [
        0xAD9DB4, 0xAD9DA4, 0xAD9984, 0xAD9D44, 0xAD9D94, 0xAD9A74, 0xAD9D24, 0xAD9AC4,
        0xAD9BB4, 0xAD9BF4, 0xAD9C24, 0xAD9C54, 0xAD9C84, 0xAD9AA4, 0xAD9C04, 0xAD9CF4,
        0xAD9BA4, 0xAD9A34, 0xAD9A44, 0xAD9A54, 0xAD9D64, 0xAD9D34, 0xAD9B74, 0xAD9C94,
    ];

    private static unsafe int* GoldBrickShopBaseAddress => (int*)(Mod.BaseAddress + 0x94B13C);
    private static unsafe int* RedBrickShopBaseAddress => (int*)(Mod.BaseAddress + 0x94CEE4);
    private static unsafe int* JokeShopBaseAddress => (int*)(Mod.BaseAddress + 0x94E514);
    private static unsafe byte* SingleSlotCharacterBaseAddress => *(byte**)(Mod.BaseAddress + 0xADBF90);
    private static unsafe byte* MultiSlotCharacterBaseAddress => *(byte**)(Mod.BaseAddress + 0xAE157C);
    private static unsafe uint ShopTextAddress => (uint)(HintSystem.HintTextBaseAddress + 0x274);
    private static unsafe int* GoldBrickShopPointerAddress => *(int**)(Mod.BaseAddress + 0xAE6E58) + 0xE0;
    private static unsafe byte* CharacterPointerBaseAddress => *(byte**)(Mod.BaseAddress + 0xB06ED0);
    private static IntPtr[] OriginalJokeShopPointers = new IntPtr[JokeShopCount];
    private static IntPtr OriginalGoldBrickShopPointer;
    private static IntPtr[] RedBrickShopAddresses = new IntPtr[RedBrickShopCount];
    private static IntPtr[] OriginalRedBrickShopPointers = new IntPtr[RedBrickShopCount];
    private static IntPtr[] OriginalCharacterPointers = new IntPtr[CharacterShopCount];

    private static int ValidateMultiplier(int multiplier)
    {
        if (multiplier < 1 || multiplier > 10)
        {
            Game.PrintToLog($"Multiplier Amount is: {multiplier} which is invalid. Changing amount to 1.");
            return 1;
        }

        return multiplier;
    }

    private static unsafe void ApplyPriceOperation(int* baseAddress, int count, int Gap, int multiplier, bool reverse = false)
    {
        if (baseAddress == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int* current = baseAddress + i * Gap;
            *current = reverse ? (*current * multiplier) : (*current / multiplier);
        }
    }

    private static unsafe bool TryGetCharacterShopTextPointer(int characterId, out int* pointer)
    {
        pointer = null;
        int offset = CharacterHandler.GetCharacterByteOffset(characterId);
        if (offset == -1)
        {
            return false;
        }
        offset += 0x70;

        byte* pointerAddress1 = *(byte**)(CharacterPointerBaseAddress + 0x64);
        byte* pointerAddress2 = *(byte**)(pointerAddress1 + offset * 0x4);
        byte* pointerAddress3 = pointerAddress2 + 0x0C;
        pointer = (int*)pointerAddress3;
        return true;
    }

    private static unsafe bool AreShopBaseAddressesValid()
    {
        return GoldBrickShopBaseAddress != null && RedBrickShopBaseAddress != null && SingleSlotCharacterBaseAddress != null && MultiSlotCharacterBaseAddress != null;
    }

    // This helper function is used to reduce the shop prices based on what is stated in the Slot Data
    public static unsafe void SetShopPrices(int multiplier)
    {
        multiplier = ValidateMultiplier(multiplier);

        if (!AreShopBaseAddressesValid())
        {
            Game.PrintToLog("[ShopPrices] One or more shop base addresses are null, cannot set shop prices");
            return;
        }

        ApplyPriceOperation(GoldBrickShopBaseAddress, 1, 1, multiplier);
        ApplyPriceOperation(RedBrickShopBaseAddress, RedBrickShopCount, RedBrickShopGap, multiplier);

        int* singleSlotPricePtr = (int*)(SingleSlotCharacterBaseAddress + 0x4);
        int* multiSlotPricePtr = (int*)(MultiSlotCharacterBaseAddress + 0x64);

        Game.PrintToLog($"Single Slot Character Price Address: 0x{(nuint)singleSlotPricePtr:X}");
        Game.PrintToLog($"Multi Slot Character Price Address: 0x{(nuint)multiSlotPricePtr:X}");

        ApplyPriceOperation(singleSlotPricePtr, SingleSlotCharacterCount, CharacterPriceGap, multiplier);
        ApplyPriceOperation(multiSlotPricePtr, MultiSlotCharacterCount, CharacterPriceGap, multiplier);
    }

    // Joke shop prices are set upon opening the shop. As such, we have to run this separately from the other shop price function.
    public static unsafe void SetJokeShopPrices(int multiplier)
    {
        multiplier = ValidateMultiplier(multiplier);

        if (JokeShopBaseAddress == null)
        {
            Game.PrintToLog("[ShopPrices] Joke shop base address is null, cannot set prices");
            return;
        }

        ApplyPriceOperation(JokeShopBaseAddress, JokeShopCount, JokeShopGap, multiplier);
    }

    // To make sure the multiplier doesn't compound, we reverse the Joke shop effects when leaving the shop
    public static unsafe void ReverseJokeShopPriceChanges(int multiplier)
    {
        multiplier = ValidateMultiplier(multiplier);

        if (JokeShopBaseAddress == null)
        {
            Game.PrintToLog("[ShopPrices] Joke shop base address is null, cannot reverse prices");
            return;
        }

        ApplyPriceOperation(JokeShopBaseAddress, JokeShopCount, JokeShopGap, multiplier, reverse: true);
    }

    // Set up the Red Brick shop address array since they aren't in order in memory like the other shops
    private static void CreateRedBrickArray()
    {
        for (int i = 0; i < RedBrickShopCount; i++)
        {
            RedBrickShopAddresses[i] = new IntPtr(unchecked((nint)(Mod.BaseAddress + RedBrickShopOffsets[i])));
        }
    }

    public static unsafe void SetShopPointers()
    {
        int* firstJokePTR = JokeShopBaseAddress + 0x8;
        for (int i = 0; i < JokeShopCount; i++)
        {
            OriginalJokeShopPointers[i] = new IntPtr(*(firstJokePTR + i * JokeShopGap));
        }

        OriginalGoldBrickShopPointer = new IntPtr(*GoldBrickShopPointerAddress);
        CreateRedBrickArray();
        for (int i = 0; i < RedBrickShopCount; i++)
        {
            OriginalRedBrickShopPointers[i] = new IntPtr(*(int*)RedBrickShopAddresses[i].ToPointer());
        }

        for (int i = 0; i < CharacterShopCount; i++)
        {
            if (!TryGetCharacterShopTextPointer(i, out int* pointer))
            {
                continue;
            }

            OriginalCharacterPointers[i] = new IntPtr(*pointer);
        }
    }

    public static unsafe void UpdateJokeShopPointers()
    {
        int* firstPTR = JokeShopBaseAddress + 0x8;
        if (firstPTR == null)
        {
            return;
        }

        for (int i = 0; i < JokeShopCount; i++)
        {
            *(firstPTR + i * JokeShopGap) = (int)ShopTextAddress;
        }
    }

    public static unsafe void ResetJokeShopPointers()
    {
        int* firstPTR = JokeShopBaseAddress + 0x8;
        for (int i = 0; i < JokeShopCount; i++)
        {
            *(firstPTR + i * JokeShopGap) = (int)OriginalJokeShopPointers[i];
        }
    }

    public static unsafe void UpdateGoldBrickPointer()
    {
        *GoldBrickShopPointerAddress = (int)ShopTextAddress;
    }

    public static unsafe void ResetGoldBrickPointer()
    {
        *GoldBrickShopPointerAddress = (int)OriginalGoldBrickShopPointer;
    }

    public static unsafe void UpdateRedBrickPointers()
    {
        for (int i = 0; i < RedBrickShopCount; i++)
        {
            *(int*)RedBrickShopAddresses[i].ToPointer() = (int)ShopTextAddress;
        }
    }

    public static unsafe void ResetRedBrickPointers()
    {
        for (int i = 0; i < RedBrickShopCount; i++)
        {
            *(int*)RedBrickShopAddresses[i].ToPointer() = (int)OriginalRedBrickShopPointers[i];
        }
    }

    public static unsafe void UpdateCharacterPointers()
    {
        for (int i = 0; i < CharacterShopCount; i++)
        {
            if (!TryGetCharacterShopTextPointer(i, out int* pointer))
            {
                continue;
            }

            *pointer = (int)ShopTextAddress;
        }
    }

    public static unsafe void ResetCharacterPointers()
    {
        for (int i = 0; i < CharacterShopCount; i++)
        {
            if (!TryGetCharacterShopTextPointer(i, out int* pointer))
            {
                continue;
            }

            *pointer = (int)OriginalCharacterPointers[i];
        }
    }

    public static void HandleShopText(int itemSelected)
    {
        if (Mod.LHP2_Archipelago == null || Mod.LHP2_Archipelago.ScoutedLocations == null)
        {
            return;
        }

        long index = ArchipelagoHandler.gameOffset + itemSelected;
        if (!Mod.LHP2_Archipelago.ScoutedLocations.TryGetValue(index, out ScoutedItemInfo? item) || item == null)
        {
            Game.PrintToLog($"Error retrieving scouted location for ID {itemSelected}: index not found");
            return;
        }

        try
        {
            string message = item.Player + "'s " + item.ItemDisplayName;
            if (message.Length > 60)
            {
                message = message[..60]; // Truncate message if it exceeds selected max hint length
            }
            HintSystem.SetMessageText(message, ShopTextAddress);
        }
        catch (Exception ex)
        {
            Game.PrintToLog($"Error retrieving scouted location for ID {itemSelected}: {ex.Message}");
        }
    }

}