using System.Runtime.InteropServices;
using Archipelago.MultiClient.Net.Models;
using LHP2_Archi_Mod.Addresses;

namespace LHP2_Archi_Mod;

public class Shops
{
    private const int RedBrickShopCount = 24;
    private const int JokeShopCount = 19;
    private const int SingleSlotCharacterCount = 0x72;
    private const int MultiSlotCharacterCount = 0xD2;
    private const int CharacterPriceGap = 0x8;
    // Gaps are in ints; red bricks are 0x18 bytes apart (0x20 on 64-bit)
    private static int RedBrickShopGap => Off.RedBrickShopStride / sizeof(int);
    // Joke shop entries: 0x2C bytes, text at +0x20 (64-bit: 0x50 bytes, text at +0x38)
    private static int JokeShopGap => (GameVersion.Is64 ? 0x50 : 0x2C) / sizeof(int);
    private static int JokeShopTextPointer => GameVersion.Is64 ? 0x38 : 0x20;

    private static unsafe int* GoldBrickShopBaseAddress => (int*)Addr.GoldBrickShopPrice;
    private static unsafe int* RedBrickShopBaseAddress => (int*)Addr.RedBrickShopPrices;
    private static unsafe int* JokeShopBaseAddress => (int*)Addr.JokeShopTable;
    private static unsafe byte* SingleSlotCharacterBaseAddress => Addr.SingleSlotCharacterPricesPtr == 0 ? null : *(byte**)Addr.SingleSlotCharacterPricesPtr;
    private static unsafe byte* MultiSlotCharacterBaseAddress => Addr.MultiSlotCharacterPricesPtr == 0 ? null : *(byte**)Addr.MultiSlotCharacterPricesPtr;

    // 64-bit: the text bank isn't located, so shop texts go to a mod buffer
    private static nuint _ownShopText;
    private static unsafe nuint ShopTextAddress
    {
        get
        {
            if (!GameVersion.Is64)
                return (nuint)(*(byte**)Addr.HintTextBasePtr + Off.ShopText);
            if (_ownShopText == 0)
            {
                _ownShopText = (nuint)Marshal.AllocHGlobal(256);
                *(byte*)_ownShopText = 0;
            }
            return _ownShopText;
        }
    }

    // Gold brick shop text pointer in the text table (entry 0xE0, 0xEC on 64-bit)
    private static unsafe nint* GoldBrickShopPointerAddress => (nint*)(*(byte**)Addr.TextTablePtr + Off.GoldBrickShopTextSlot);
    private static bool GoldBrickShopTextAvailable => Addr.TextTablePtr != 0;
    private static unsafe byte* CharacterPointerBaseAddress => *(byte**)Addr.CharacterTableBasePtr;
    private static IntPtr[] OriginalJokeShopPointers = new IntPtr[JokeShopCount];
    private static IntPtr OriginalGoldBrickShopPointer;
    private static IntPtr[] RedBrickShopAddresses = new IntPtr[RedBrickShopCount];
    private static IntPtr[] OriginalRedBrickShopPointers = new IntPtr[RedBrickShopCount];
    private static readonly Dictionary<int, IntPtr> OriginalCharacterPointers = new();

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

    private static unsafe nint* JokeShopTextPointerAddress(int i) => (nint*)((byte*)(JokeShopBaseAddress + i * JokeShopGap) + JokeShopTextPointer);

    private static unsafe bool TryGetCharacterShopTextPointer(int characterId, out nint* pointer)
    {
        pointer = null;
        int offset = CharacterHandler.GetCharacterByteOffset(characterId);
        if (offset == -1)
        {
            return false;
        }
        offset += 0x70;

        byte* pointerAddress1 = *(byte**)(CharacterPointerBaseAddress + Off.CharacterTableArray);
        byte* pointerAddress2 = *(byte**)(pointerAddress1 + offset * Off.PtrSize);
        byte* pointerAddress3 = pointerAddress2 + Off.CharacterEntryText;
        pointer = (nint*)pointerAddress3;
        return true;
    }

    // 64-bit: the character prices are handled in SetCharacterPrices64
    private static unsafe bool AreShopBaseAddressesValid()
    {
        bool characterTablesValid = GameVersion.Is64 || (SingleSlotCharacterBaseAddress != null && MultiSlotCharacterBaseAddress != null);
        return GoldBrickShopBaseAddress != null && RedBrickShopBaseAddress != null && characterTablesValid;
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

        if (GameVersion.Is64)
        {
            SetCharacterPrices64(multiplier);
            return;
        }

        int* singleSlotPricePtr = (int*)(SingleSlotCharacterBaseAddress + 0x4);
        int* multiSlotPricePtr = (int*)(MultiSlotCharacterBaseAddress + 0x64);

        Game.PrintToLog($"Single Slot Character Price Address: 0x{(nuint)singleSlotPricePtr:X}");
        Game.PrintToLog($"Multi Slot Character Price Address: 0x{(nuint)multiSlotPricePtr:X}");

        ApplyPriceOperation(singleSlotPricePtr, SingleSlotCharacterCount, CharacterPriceGap, multiplier);
        ApplyPriceOperation(multiSlotPricePtr, MultiSlotCharacterCount, CharacterPriceGap, multiplier);
    }

    // 64-bit: one merged price table (0x20-byte entries), held twice: divide both copies
    private static unsafe void SetCharacterPrices64(int multiplier)
    {
        byte* divided = null;
        foreach (nuint collection in new[] { Addr.CharacterPricesA, Addr.CharacterPricesB })
        {
            byte* entries = collection == 0 ? null : *(byte**)collection;
            if (entries == null)
            {
                Game.PrintToLog("[ShopPrices] Character price table unavailable, character prices unchanged");
                continue;
            }
            // Two holders of the same array would divide its prices twice
            if (entries == divided)
                continue;
            divided = entries;
            int count = *(ushort*)(collection + 0xA);
            if (CharacterHandler.DLCCharactersEnabled)
                SetDLCCharacterPrices(entries, count);
            ApplyPriceOperation((int*)(entries + 4), count, 0x20 / sizeof(int), multiplier);
            Game.PrintToLog($"[ShopPrices] {count} character prices divided at 0x{(nuint)entries:X}");
        }

        // Madam Malkin's grid: DLC entries keep their own price (0) instead of pointing into A/B
        nuint grid = Addr.CharacterPricesC;
        byte* gridEntries = grid == 0 ? null : *(byte**)grid;
        if (CharacterHandler.DLCCharactersEnabled && gridEntries != null)
            SetDLCCharacterPrices(gridEntries, *(ushort*)(grid + 0xA), multiplier);
    }

    // Entries start with the character's game id (low word), the price is at +4
    private static unsafe void SetDLCCharacterPrices(byte* entries, int count, int divisor = 1)
    {
        foreach (var (gameId, price) in CharacterHandler.DLCCharacterPrices)
        {
            for (int i = 0; i < count; i++)
            {
                if (*(ushort*)(entries + i * 0x20) == gameId)
                {
                    *(int*)(entries + i * 0x20 + 4) = price / divisor;
                    break;
                }
            }
        }
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
            RedBrickShopAddresses[i] = (IntPtr)Addr.RedBrickTextPointer(i);
        }
    }

    public static unsafe void SetShopPointers()
    {
        if (JokeShopBaseAddress != null)
        {
            for (int i = 0; i < JokeShopCount; i++)
            {
                OriginalJokeShopPointers[i] = *JokeShopTextPointerAddress(i);
            }
        }
        else
        {
            Game.PrintToLog("[Shops] Joke shop table unavailable, joke shop text unchanged");
        }

        if (GoldBrickShopTextAvailable)
            OriginalGoldBrickShopPointer = *GoldBrickShopPointerAddress;
        else
            Game.PrintToLog("[Shops] Gold brick shop text pointer unavailable, gold brick shop text unchanged");

        CreateRedBrickArray();
        for (int i = 0; i < RedBrickShopCount; i++)
        {
            if (RedBrickShopAddresses[i] != IntPtr.Zero)
                OriginalRedBrickShopPointers[i] = *(nint*)RedBrickShopAddresses[i].ToPointer();
        }

        foreach (int i in CharacterHandler.characterMap.Keys)
        {
            if (!TryGetCharacterShopTextPointer(i, out nint* pointer))
            {
                continue;
            }

            OriginalCharacterPointers[i] = *pointer;
        }
    }

    public static unsafe void UpdateJokeShopPointers()
    {
        if (JokeShopBaseAddress == null)
        {
            return;
        }

        for (int i = 0; i < JokeShopCount; i++)
        {
            *JokeShopTextPointerAddress(i) = (nint)ShopTextAddress;
        }
    }

    public static unsafe void ResetJokeShopPointers()
    {
        if (JokeShopBaseAddress == null)
        {
            return;
        }

        for (int i = 0; i < JokeShopCount; i++)
        {
            *JokeShopTextPointerAddress(i) = OriginalJokeShopPointers[i];
        }
    }

    public static unsafe void UpdateGoldBrickPointer()
    {
        if (!GoldBrickShopTextAvailable)
            return;
        *GoldBrickShopPointerAddress = (nint)ShopTextAddress;
    }

    public static unsafe void ResetGoldBrickPointer()
    {
        if (!GoldBrickShopTextAvailable)
            return;
        *GoldBrickShopPointerAddress = OriginalGoldBrickShopPointer;
    }

    public static unsafe void UpdateRedBrickPointers()
    {
        for (int i = 0; i < RedBrickShopCount; i++)
        {
            if (RedBrickShopAddresses[i] != IntPtr.Zero)
                *(nint*)RedBrickShopAddresses[i].ToPointer() = (nint)ShopTextAddress;
        }
    }

    public static unsafe void ResetRedBrickPointers()
    {
        for (int i = 0; i < RedBrickShopCount; i++)
        {
            if (RedBrickShopAddresses[i] != IntPtr.Zero)
                *(nint*)RedBrickShopAddresses[i].ToPointer() = OriginalRedBrickShopPointers[i];
        }
    }

    public static unsafe void UpdateCharacterPointers()
    {
        foreach (int i in CharacterHandler.characterMap.Keys)
        {
            if (!TryGetCharacterShopTextPointer(i, out nint* pointer))
            {
                continue;
            }

            *pointer = (nint)ShopTextAddress;
        }
    }

    public static unsafe void ResetCharacterPointers()
    {
        foreach (var kvp in OriginalCharacterPointers)
        {
            if (!TryGetCharacterShopTextPointer(kvp.Key, out nint* pointer))
            {
                continue;
            }

            *pointer = kvp.Value;
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
            // Counted in UTF-8 bytes: the slot holds bytes, and an accent takes two
            while (System.Text.Encoding.UTF8.GetByteCount(message) > 60)
            {
                message = message[..^1]; // Truncate message if it exceeds selected max shop character length (seems to be 64 characters, but we leave a little buffer cause of the Gold Brick Shop adding numbers)
            }
            HintSystem.WriteTextToMemory(message, ShopTextAddress);
        }
        catch (Exception ex)
        {
            Game.PrintToLog($"Error retrieving scouted location for ID {itemSelected}: {ex.Message}");
        }
    }

}
