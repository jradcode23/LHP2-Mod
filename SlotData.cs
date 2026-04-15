namespace LHP2_Archi_Mod;

public class SlotData
{
    public SlotData(Dictionary<string, object> slotData)
    {
        SlotDataDictionary = slotData;
    }

    public Dictionary<string, object> SlotDataDictionary { get; set; }

    /// <summary>
    /// Gets the end goal requirement as an integer.
    /// Returns -1 if not found or unable to convert.
    /// </summary>
    public int EndGoal =>
        SlotDataDictionary.TryGetValue("EndGoal", out var value)
            ? Convert.ToInt32(value)
            : -1;

    public int NumberOfRequiredHorcruxes =>
        SlotDataDictionary.TryGetValue("NumHorcruxRequired", out var value)
            ? Convert.ToInt32(value)
            : -1;

    public int ShuffleJokeSpells =>
        SlotDataDictionary.TryGetValue("ShuffleJokeSpells", out var value)
            ? Convert.ToInt32(value)
            : -1;

    public int ShuffleGoldBrickPurchases =>
        SlotDataDictionary.TryGetValue("ShuffleGoldBrickPurchases", out var value)
            ? Convert.ToInt32(value)
            : -1;

    public int CheaperShops =>
        SlotDataDictionary.TryGetValue("CheaperShops", out var value)
            ? Convert.ToInt32(value)
            : -1;

    /// <summary>
    /// Gets the collectibles required as a Dictionary mapping collectible names to required counts.
    /// Returns an empty dictionary if not found.
    /// </summary>
    // public Dictionary<string, int> CollectiblesRequired
    // {
    //     get
    //     {
    //         if (!SlotDataDictionary.TryGetValue("CollectiblesRequired", out var value))
    //             return [];

    //         if (value is Newtonsoft.Json.Linq.JObject jObject)
    //         {
    //             return jObject.ToObject<Dictionary<string, int>>() ?? [];
    //         }

    //         return [];
    //     }
    // }

    /// <summary>
    /// Prints the slot data to console for debugging/verification.
    /// </summary>
    public void PrintData()
    {
        Game.PrintToLog("=== Slot Data ===");
        Game.PrintToLog($"EndGoal: {EndGoal}");
        Game.PrintToLog($"Required Horcruxes: {NumberOfRequiredHorcruxes}");
        Game.PrintToLog($"Shuffle Joke Spells: {ShuffleJokeSpells}");
        Game.PrintToLog($"Shuffle Gold Brick Purchases: {ShuffleGoldBrickPurchases}");
        Game.PrintToLog($"Cheaper Shops Multiplier: {CheaperShops}");

        if (EndGoal == -1 || NumberOfRequiredHorcruxes == -1 || ShuffleJokeSpells == -1 || ShuffleGoldBrickPurchases == -1 || CheaperShops == -1)
        {
            Game.PrintToLog("One or more values not found or invalid. Please report to the devs.");
        }

        // if (EndGoal == 1)
        // {
        //     Game.PrintToLog("CollectiblesRequired:");
        //     Game.PrintToLog("=================");
        //     foreach (var kvp in CollectiblesRequired)
        //     {
        //         Game.PrintToLog($"  {kvp.Key}: {kvp.Value}");
        //     }
        //     Game.PrintToLog("=================");
        // }
    }
}