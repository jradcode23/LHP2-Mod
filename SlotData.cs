namespace LHP2_Archi_Mod;

public class SlotData(Dictionary<string, object> slotData)
{
    private Dictionary<string, object> SlotDataDictionary { get; set; } = slotData ?? [];
    public int EndGoal => GetIntSlotValue("EndGoal");
    public int NumberOfRequiredHorcruxes => GetIntSlotValue("NumHorcruxRequired");
    public int NumberOfRequiredLevels => GetIntSlotValue("NumLevelsRequired");
    public int ShuffleCharacterTokens => GetIntSlotValue("ShuffleCharacterTokens");
    public int ShuffleRedBricks => GetIntSlotValue("ShuffleRedBricks");
    public int ShuffleJokeSpells => GetIntSlotValue("ShuffleJokeSpells");
    public int ShuffleGoldBrickPurchases => GetIntSlotValue("ShuffleGoldBrickPurchases");
    public int CheaperShops => GetRangedIntSlotValue("CheaperShops", 1, 10);
    public int FasterDuels => GetIntSlotValue("FasterDuels");

    private int GetIntSlotValue(string key)
    {
        if (SlotDataDictionary?.TryGetValue(key, out var value) != true || value == null)
            return -1;

        if (!int.TryParse(value.ToString(), out int result))
        {
            Game.PrintToLog($"[SlotData] Failed to parse {key} value '{value}'");
            return -1;
        }

        return result;
    }

    private int GetRangedIntSlotValue(string key, int min, int max)
    {
        int result = GetIntSlotValue(key);
        if (result == -1)
            return -1;

        if (result < min || result > max)
        {
            Game.PrintToLog($"[SlotData] {key} value {result} is out of valid range ({min}-{max})");
            return -1;
        }

        return result;
    }

    // Helper Function that prints the slot data to console for debugging/verification.
    public void PrintData()
    {
        Game.PrintToLog("=== Slot Data ===");
        Game.PrintToLog($"EndGoal: {EndGoal}");
        Game.PrintToLog($"Required Horcruxes: {NumberOfRequiredHorcruxes}");
        Game.PrintToLog($"Required Levels: {NumberOfRequiredLevels}");
        Game.PrintToLog($"Shuffle Character Tokens: {ShuffleCharacterTokens}");
        Game.PrintToLog($"Shuffle Red Bricks: {ShuffleRedBricks}");
        Game.PrintToLog($"Shuffle Joke Spells: {ShuffleJokeSpells}");
        Game.PrintToLog($"Shuffle Gold Brick Purchases: {ShuffleGoldBrickPurchases}");
        Game.PrintToLog($"Cheaper Shops Multiplier: {CheaperShops}");
        Game.PrintToLog($"Faster Duels: {FasterDuels}");


        if (EndGoal == -1 || NumberOfRequiredHorcruxes == -1 || NumberOfRequiredLevels == -1 || ShuffleJokeSpells == -1 || ShuffleGoldBrickPurchases == -1 || CheaperShops == -1 || FasterDuels == -1)
        {
            Game.PrintToLog("One or more values not found or invalid. Please report to the devs.");
        }
    }
}