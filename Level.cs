
namespace LHP2_Archi_Mod;

public class Level
{
    private static unsafe readonly byte* levelBaseAddress = (byte*)(*(int*)(Mod.BaseAddress + 0xC55F2C));

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

    public static readonly LevelData[] LevelUnlockOrder = new LevelData[]
    {
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
    };

    public static LevelData ConvertIDToLeveData(int id)
    {
        LevelData level = LevelUnlockOrder[id];
        return level;
    }

    public static unsafe void UnlockLevel(LevelData level)
    {
        byte* ptr = levelBaseAddress + (ushort)level;
        if (ptr == null || levelBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock Level, null pointer at 0x{(nuint)ptr:X}");
        } 
        *ptr |= (byte)(BitMask.StoryUnlocked | BitMask.FreeplayUnlocked);
    }

    public static unsafe void UnlockGryffindorCrest(LevelData level)
    {
        byte* ptr = levelBaseAddress + (ushort)level;
        if (ptr == null || levelBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock GC, null pointer at 0x{(nuint)ptr:X}");
        } 
        *ptr |= (byte)(BitMask.GryfCrest);
    }

    public static unsafe void UnlockSlytherinCrest(LevelData level)
    {
        byte* ptr = levelBaseAddress + (ushort)level;
        if (ptr == null || levelBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock SC, null pointer at 0x{(nuint)ptr:X}");
        } 
        *ptr |= (byte)(BitMask.SlythCrest);
    }

    public static unsafe void UnlockRavenclawCrest(LevelData level)
    {
        byte* ptr = levelBaseAddress + (ushort)level;
        if (ptr == null || levelBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock RC, null pointer at 0x{(nuint)ptr:X}");
        } 
        *ptr |= (byte)(BitMask.RavenCrest);
    }

    public static unsafe void UnlockHufflepuffCrest(LevelData level)
    {
        byte* ptr = levelBaseAddress + (ushort)level;
        if (ptr == null || levelBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock HC, null pointer at 0x{(nuint)ptr:X}");
        } 
        *ptr |= (byte)(BitMask.HuffleCrest);
    }

    public static unsafe void UnlockStudentInPeril(LevelData level)
    {
        byte* ptr = levelBaseAddress + (ushort)level;
        if (ptr == null || levelBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock SIP, null pointer at 0x{(nuint)ptr:X}");
        } 
        *ptr |= (byte)(BitMask.StudentInPeril);
    }

    public static unsafe void UnlockTrueWizard(LevelData level)
    {
        byte* story = levelBaseAddress + (ushort)level - 6;
        byte* freeplay = story + 1;
        if (story == null || freeplay == null || levelBaseAddress == null) 
        {
            Console.WriteLine($"Can't Unlock TW, null pointer at 0x{(nuint)story:X}");
        } 
        *story = 1;
        *freeplay = 1;
    }
    
    public static unsafe void ResetLevels()
    {
        for(int i = 0; i < LevelUnlockOrder.Length; i++)
        {
            byte* level = levelBaseAddress + (ushort)LevelUnlockOrder[i];
            byte* story = level - 6;
            byte* freeplay = story + 1;
            *level = 0;
            *story = 0;
            *freeplay = 0;
        }
    }

    public static void MakeAllBoardsVisible()
    {
        UnlockLevel(LevelData.DarkTimes); 
        UnlockLevel(LevelData.OutOfRetirement);
        UnlockLevel(LevelData.TheSevenHarrys);
        UnlockLevel(LevelData.TheThiefsDownfall);
    }

    //TODO: Update for shop putchase logic - i.e. receive purchase before completing location
    public static void ImplementMapLogic(int map)
    {
        switch (map)
        {
            // Leaky Cauldron
            case 368:
            case 374:
            case 380:
            case 386:
                MakeAllBoardsVisible();
                break;
            case 402:
                break;
            default:
                break;
        }
    }
}