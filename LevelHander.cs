namespace LHP2_Archi_Mod;

public class LevelHandler
{
    public static unsafe byte* LevelBaseAddress => *(byte**)(Mod.BaseAddress + 0xC55F2C);

    // Bitmask for how the level unlock byte is structed.
    [Flags]
    public enum BitMask
    {
        None = 0,
        GryfCrest = 1 << 0,
        SlythCrest = 1 << 1,
        RavenCrest = 1 << 2,
        HuffleCrest = 1 << 3,
        StoryUnlocked = 1 << 4,
        FreeplayUnlocked = 1 << 5,
        // 6 is empty
        StudentInPeril = 1 << 7,
    }

    // Enum representing each level and their offset from the base pointer
    public enum LevelData : ushort
    {
        // Y5
        DarkTimes = 0xA,
        DumbeldoresArmy = 0x6E,
        Focus = 0x82,
        KreacherDiscomforts = 0x96,
        AGiantVirtuoso = 0xAA,
        AVeiledThreat = 0xBE,

        // Y6
        OutOfRetirement = 0xD2,
        JustDesserts = 0xE6,
        ANotSoMerryChristmas = 0xFA,
        LoveHurts = 0x10E,
        FelixFelicis = 0x122,
        TheHorcruxAndTheHand = 0x136,

        // Y7
        TheSevenHarrys = 0x14A,
        MagicIsMight = 0x15E,
        InGraveDanger = 0x172,
        SwordAndLocket = 0x186,
        LovegoodsLunacy = 0x19A,
        Dobby = 0x1AE,

        // Y8
        TheThiefsDownfall = 0x1C2,
        BackToSchool = 0x1D6,
        BurningBridges = 0x1EA,
        FiendfyreFrenzy = 0x1FE,
        SnapesTears = 0x212,
        TheFlawInThePlan = 0x226,
    }

    // Used to fetch the level number, since in level activity is stored in the same addresses, we use this to make sure the right check is sent
    public static readonly LevelData[] LevelUnlockOrder =
    [
        LevelData.DarkTimes,
        LevelData.DumbeldoresArmy,
        LevelData.Focus,
        LevelData.KreacherDiscomforts,
        LevelData.AGiantVirtuoso,
        LevelData.AVeiledThreat,
        LevelData.OutOfRetirement,
        LevelData.JustDesserts,
        LevelData.ANotSoMerryChristmas,
        LevelData.LoveHurts,
        LevelData.FelixFelicis,
        LevelData.TheHorcruxAndTheHand,
        LevelData.TheSevenHarrys,
        LevelData.MagicIsMight,
        LevelData.InGraveDanger,
        LevelData.SwordAndLocket,
        LevelData.LovegoodsLunacy,
        LevelData.Dobby,
        LevelData.TheThiefsDownfall,
        LevelData.BackToSchool,
        LevelData.BurningBridges,
        LevelData.FiendfyreFrenzy,
        LevelData.SnapesTears,
        LevelData.TheFlawInThePlan,
    ];

    // Helper function to look up the applicable level based on the Archi ID
    public static LevelData ConvertIDToLeveData(int id)
    {
        LevelData level = LevelUnlockOrder[id];
        return level;
    }

    // Helper function to unlock a level
    public static unsafe void UnlockLevel(LevelData level)
    {
        byte* ptr = LevelBaseAddress + (ushort)level;
        if (ptr == null || LevelBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock Level, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)(BitMask.StoryUnlocked | BitMask.FreeplayUnlocked);
    }

    // Helper function to unlock a Gryffindor Crest
    public static unsafe void UnlockGryffindorCrest(LevelData level)
    {
        byte* ptr = LevelBaseAddress + (ushort)level;
        if (ptr == null || LevelBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock GC, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.GryfCrest;
    }

    // Helper function to unlock a Slytherin Crest
    public static unsafe void UnlockSlytherinCrest(LevelData level)
    {
        byte* ptr = LevelBaseAddress + (ushort)level;
        if (ptr == null || LevelBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock SC, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.SlythCrest;
    }

    // Helper function to unlock a Ravenclaw Crest
    public static unsafe void UnlockRavenclawCrest(LevelData level)
    {
        byte* ptr = LevelBaseAddress + (ushort)level;
        if (ptr == null || LevelBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock RC, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.RavenCrest;
    }

    // Helper function to unlock a Hufflepuff Crest
    public static unsafe void UnlockHufflepuffCrest(LevelData level)
    {
        byte* ptr = LevelBaseAddress + (ushort)level;
        if (ptr == null || LevelBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock HC, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.HuffleCrest;
    }

    // Helper function to unlock an In Level SIP
    public static unsafe void UnlockStudentInPeril(LevelData level)
    {
        byte* ptr = LevelBaseAddress + (ushort)level;
        if (ptr == null || LevelBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock SIP, null pointer at 0x{(nuint)ptr:X}");
        }
        *ptr |= (byte)BitMask.StudentInPeril;
    }

    // Helper function to unlock a True Wizard
    public static unsafe void UnlockTrueWizard(LevelData level)
    {
        // Adjusting the address since TW address is before the rest of the in level activity
        byte* story = LevelBaseAddress + (ushort)level - 6;
        byte* freeplay = story + 1;
        if (story == null || freeplay == null || LevelBaseAddress == null)
        {
            Game.PrintToLog($"Can't Unlock TW, null pointer at 0x{(nuint)story:X}");
        }
        *story = 1;
        *freeplay = 1;
    }

    // Helper function to reset all In Level Unlocks
    public static unsafe void ResetLevels()
    {
        for (int i = 0; i < LevelUnlockOrder.Length; i++)
        {
            byte* level = LevelBaseAddress + (ushort)LevelUnlockOrder[i];
            byte* story = level - 6;
            byte* freeplay = story + 1;
            *level = 0;
            *story = 0;
            *freeplay = 0;
        }
    }

    // If you unlock a level in a later year before having 1 level in all years before, you can't select the any levels on the later level board. This makes sure to temporarily show a level in all years so all level boards are selectable.
    public static void MakeAllBoardsVisible()
    {
        UnlockLevel(LevelData.DarkTimes);
        UnlockLevel(LevelData.OutOfRetirement);
        UnlockLevel(LevelData.TheSevenHarrys);
        UnlockLevel(LevelData.TheThiefsDownfall);
    }

    // Some maps require special logic, this function takes the map ID and applies any special logic.
    public static unsafe void ImplementMapLogic(int map)
    {
        byte* y5GhostPtr2 = HubHandler.GhostPathBaseAddress + 0x21;
        // Game doesn't open WW Courtyard if these two bits are completed so plan is to handle them once map changes after the fact
        bool locationChecked = Mod.LHP2_Archipelago!.IsLocationChecked(1014);
        bool bitSet1 = (*y5GhostPtr2 & (1 << 4)) != 0;
        bool bitSet2 = (*y5GhostPtr2 & (1 << 5)) == 0;

        // Marks the two levels complete after the next map change (assuming they aren't in great hall lobby)
        if (locationChecked && bitSet1 && bitSet2 && Mod.GameInstance!.MapID != 293)
        {
            *y5GhostPtr2 |= 1 << 5; // Mark A Giant Viruoso Story Complete
            *y5GhostPtr2 |= 1 << 6; // Mark A Veiled Threat Story Complete
        }

        switch (map)
        {
            // Diagon Alley
            case 370:
            case 376:
            case 384:
            case 388:
                HubHandler.ClearReturnToHogwartsLocation();
                break;
            // Leaky Cauldron
            case 368:
            case 380:
            case 386:
                MakeAllBoardsVisible();
                break;
            // Leaky Cauldron in Y7 - to handle the special case of first visit (to clear the 7 Harry's Loading Zone on the first visit)
            case 374:
                MakeAllBoardsVisible();
                if (!HubHandler.CheckIfLeaky7Entered())
                {
                    Game.PrintToLog("Player has not entered Leaky2London Y7, clearing boards and setting PTR for Leaky2London Y7.");
                    new Thread(HubHandler.CheckLeaky2LondonY7PTR).Start();
                }
                break;
            // Menu & MM
            case 366:
            case 372:
            case 378:
            case 382:
            case 402:
                HubHandler.VerifyCharCustMaps();
                break;
            default:
                break;
        }
    }
}
