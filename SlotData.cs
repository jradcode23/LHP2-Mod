namespace LHP2_Archi_Mod;

public class SlotData(Dictionary<string, object> slotData)
{
    public Dictionary<string, object> SlotDataDictionary { get; set; } = slotData ?? new();

    /// <summary>
    /// Gets the end goal requirement as an integer. Returns -1 if not found or unable to convert.
    /// </summary>
    public int EndGoal
    {
        get
        {
            if (SlotDataDictionary?.TryGetValue("EndGoal", out var value) != true || value == null)
                return -1;

            if (!int.TryParse(value.ToString(), out int result))
            {
                Game.PrintToLog($"[SlotData] Failed to parse EndGoal value '{value}'");
                return -1;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the Horcrux requirement as an integer. Returns -1 if not found or unable to convert.
    /// </summary>
    public int NumberOfRequiredHorcruxes
    {
        get
        {
            if (SlotDataDictionary?.TryGetValue("NumHorcruxRequired", out var value) != true || value == null)
                return -1;

            if (!int.TryParse(value.ToString(), out int result))
            {
                Game.PrintToLog($"[SlotData] Failed to parse NumberOfRequiredHorcruxes value '{value}'");
                return -1;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets whether Joke shop spells are shuffled (0 or 1). Returns -1 if not found or unable to convert.
    /// </summary>
    public int ShuffleJokeSpells
    {
        get
        {
            if (SlotDataDictionary?.TryGetValue("ShuffleJokeSpells", out var value) != true || value == null)
                return -1;

            if (!int.TryParse(value.ToString(), out int result))
            {
                Game.PrintToLog($"[SlotData] Failed to parse ShuffleJokeSpells value '{value}'");
                return -1;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets whether Gold Brick Purchases are shuffled (0 or 1). Returns -1 if not found or unable to convert.
    /// </summary>
    public int ShuffleGoldBrickPurchases
    {
        get
        {
            if (SlotDataDictionary?.TryGetValue("ShuffleGoldBrickPurchases", out var value) != true || value == null)
                return -1;

            if (!int.TryParse(value.ToString(), out int result))
            {
                Game.PrintToLog($"[SlotData] Failed to parse ShuffleGoldBrickPurchases value '{value}'");
                return -1;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the Cheaper shops multiplier (1-10). Returns -1 if not found or unable to convert.
    /// </summary>
    public int CheaperShops
    {
        get
        {
            if (SlotDataDictionary?.TryGetValue("CheaperShops", out var value) != true || value == null)
                return -1;

            if (!int.TryParse(value.ToString(), out int result))
            {
                Game.PrintToLog($"[SlotData] Failed to parse CheaperShops value '{value}'");
                return -1;
            }

            if (result < 1 || result > 10)
            {
                Game.PrintToLog($"[SlotData] CheaperShops value {result} is out of valid range (1-10)");
                return -1;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the Cheaper shops multiplier (1-10). Returns -1 if not found or unable to convert.
    /// </summary>
    public int FasterDuels
    {
        get
        {
            if (SlotDataDictionary?.TryGetValue("FasterDuels", out var value) != true || value == null)
                return -1;

            if (!int.TryParse(value.ToString(), out int result))
            {
                Game.PrintToLog($"[SlotData] Failed to parse Faster Duels value '{value}'");
                return -1;
            }

            return result;
        }
    }

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


    // Helper Function that prints the slot data to console for debugging/verification.
    public void PrintData()
    {
        Game.PrintToLog("=== Slot Data ===");
        Game.PrintToLog($"EndGoal: {EndGoal}");
        Game.PrintToLog($"Required Horcruxes: {NumberOfRequiredHorcruxes}");
        Game.PrintToLog($"Shuffle Joke Spells: {ShuffleJokeSpells}");
        Game.PrintToLog($"Shuffle Gold Brick Purchases: {ShuffleGoldBrickPurchases}");
        Game.PrintToLog($"Cheaper Shops Multiplier: {CheaperShops}");
        Game.PrintToLog($"Faster Duels: {FasterDuels}");


        if (EndGoal == -1 || NumberOfRequiredHorcruxes == -1 || ShuffleJokeSpells == -1 || ShuffleGoldBrickPurchases == -1 || CheaperShops == -1 || FasterDuels == -1)
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