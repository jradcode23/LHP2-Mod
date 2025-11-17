
namespace LHP_Archi_Mod;

public class Level
{
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

    public static void ConvertIDToLeveData(int id)
    {
        if (id < 0 || id >= LevelUnlockOrder.Length)
        {
            Console.WriteLine($"Invalid level ID: {id}");
            return;
        }
        LevelData level = LevelUnlockOrder[id];
        UnlockLevel(level);
    }

    public static unsafe void UnlockLevel(LevelData level)
    {
        int levelBaseAddress = *(int*)(Mod.BaseAddress + 0xC55F2C);
        byte* ptr = (byte*)((nint)levelBaseAddress + (int)level);
        *ptr |= (byte)(BitMask.StoryUnlocked | BitMask.FreeplayUnlocked);
    }
}