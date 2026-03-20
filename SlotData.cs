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
        Mod.Logger!.WriteLineAsync("=== Slot Data ===");
        Mod.Logger!.WriteLineAsync($"EndGoal: {EndGoal}");
        Mod.Logger!.WriteLineAsync($"Required Horcruxes: {NumberOfRequiredHorcruxes}");
        
        if (EndGoal == -1 || NumberOfRequiredHorcruxes == -1)
        {
            Mod.Logger!.WriteLineAsync("EndGoal or Horcrux value not found or invalid. Please report to the devs.");
        }

        // if (EndGoal == 1)
        // {
        //     Mod.Logger!.WriteLineAsync("CollectiblesRequired:");
        //     Mod.Logger!.WriteLineAsync("=================");
        //     foreach (var kvp in CollectiblesRequired)
        //     {
        //         Mod.Logger!.WriteLineAsync($"  {kvp.Key}: {kvp.Value}");
        //     }
        //     Mod.Logger!.WriteLineAsync("=================");
        // }
    }
}