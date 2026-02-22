namespace LHP2_Archi_Mod;

public class SpellHandler
{
    private static unsafe readonly byte* spellBaseAddress = (byte*)(Mod.BaseAddress + 0xB06AB0);
    private static unsafe readonly byte* spellVisibilityBaseAddress = (byte*)(Mod.BaseAddress + 0xB067C4);

    public static void UnlockSpell(int id)
    {

        int byteOffset = id / 8;
        int bitOffset = id % 8;

        UnlockPassiveSpell(byteOffset, bitOffset);
        UnlockActiveSpell(byteOffset, bitOffset);

        bool isVisible = id == 23 || id == 27 || id == 28 || id == 29 || id == 30;
        if (isVisible)
        {
            MakeSpellVisible(id);
        }
    }

    public static unsafe void UnlockPassiveSpell(int byteoffset, int bitOffset)
    {
        byte* ptr = spellBaseAddress + byteoffset;

        if (ptr == null)
        {
            Console.WriteLine("SpellBaseAddress: null pointer");
            return;
        }
        *ptr |= (byte)(1 << bitOffset);
    }

    public static unsafe void MakeSpellVisible(int id)
    {
        if (spellVisibilityBaseAddress == null)
        {
            Console.WriteLine("SpellVisibilityBaseAddress: null pointer");
            return;
        }

        int offset = 8;
        if (id != 23)
        {
            id -= 23;
        } else
        {
            id -= 21;
        }
        byte* ptr = spellVisibilityBaseAddress + offset * id;
        Console.WriteLine($"Making Spell ID {id} visible at offset {offset * id}");
        *ptr = 1;
    }

    public static unsafe void UnlockActiveSpell(int byteoffset, int bitOffset)
    {
        byte* activeSpellBaseAddress = *(byte**)(Mod.BaseAddress + 0x00C53930);

        if (activeSpellBaseAddress == null)
        {
            // Console.WriteLine("ActiveSpellBaseAddress: null pointer");
            return;
        }

        byte* activeFirstPointer = *(byte**)(activeSpellBaseAddress + 0x1C);
        byte* activeSecondPointer = *(byte**)(activeFirstPointer + 0xBF4);
        activeSecondPointer += 0x58;

        byte* ptr = activeSecondPointer + byteoffset;
        *ptr |= (byte)(1 << bitOffset);
    }

    public static unsafe void ResetSpells()
    {

        // Reset Passive Spells
        for(int i = 0; i < 7; i++)
        {
            if (i ==4)
            {
                continue; // Skip Unknown Spells
            }

            byte* passivePTR = spellBaseAddress + i;
            *passivePTR = 0;
        }

        ResetActiveSpells();
        MakeSpellsInvisible();

        // Set the Default Spells
        UnlockSpell(0); // Wingardium Leviosa
        UnlockSpell(20); // Pets
        UnlockSpell(21); // Invisibility Cloak
        UnlockSpell(22); // Avada
        // UnlockSpell(23); // Diffindo
        UnlockSpell(24); // Lumos Part 1
        UnlockSpell(25); // Lumos Part 2
        // UnlockSpell(26); // Deluminator & Polyjuice
        // UnlockSpell(27); // Aguamenti
        // UnlockSpell(28); // Focus
        // UnlockSpell(29); // Expecto Patronum
        // UnlockSpell(30); // Reducto
        UnlockSpell(31); // Unknown Spell
        UnlockSpell(40); // Unknown Spell
        UnlockSpell(41); // Unknown Spell
        UnlockSpell(42); // Draught of Living Death
        UnlockSpell(43); // Thestral Spell
        UnlockSpell(44); // Dueling
        UnlockSpell(46); // DADA
        UnlockSpell(47); // Grawp Befriended
        UnlockSpell(48); // Slughorn Vial
        UnlockSpell(52); // Unknown Spell
        UnlockSpell(53); // Unknown Spell
        UnlockSpell(54); // Unknown Spell
        UnlockSpell(55); // Unknown Spell

        byte* darkMagic = HubHandler.hubBaseAddress + 0x19B * 4 + 2;
        *darkMagic |= 1 << 0;;
    }

    public static unsafe void ResetActiveSpells()
    {
        byte* activeSpellBaseAddress = *(byte**)(Mod.BaseAddress + 0x00C53930);

        if (activeSpellBaseAddress == null)
        {
            // Console.WriteLine("ActiveSpellBaseAddress: null pointer");
            return;
        }

        byte* activeFirstPointer = *(byte**)(activeSpellBaseAddress + 0x1C);
        byte* activeSecondPointer = *(byte**)(activeFirstPointer + 0xBF4);
        activeSecondPointer += 0x58;

        for(int i = 0; i < 7; i++)
        {
            if (i ==4)
            {
                continue; // Skip Unknown Spells
            }

            byte* activePTR = activeSecondPointer + i;
            *activePTR = 0;
        }
    }

    public static unsafe void MakeSpellsInvisible()
    {

        if (spellVisibilityBaseAddress == null)
        {
            Console.WriteLine("SpellVisibilityBaseAddress: null pointer");
            return;
        }

        byte* ptr = spellVisibilityBaseAddress + 8 * 2;
        Console.WriteLine($"Making Spells invisible starting at {(nuint)ptr:X}");
        *ptr = 3; // Make Diffindo invisible

        ptr += 16; // Move to Aguamenti

        for (int i = 0; i < 4; i++)
        {
            *ptr = 3;
            ptr += 8;
        }
    }
}