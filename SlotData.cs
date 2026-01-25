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

    /// <summary>
    /// Gets the collectibles required as a Dictionary mapping collectible names to required counts.
    /// Returns an empty dictionary if not found.
    /// </summary>
    public Dictionary<string, int> CollectiblesRequired
    {
        get
        {
            if (!SlotDataDictionary.TryGetValue("CollectiblesRequired", out var value))
                return [];

            if (value is Newtonsoft.Json.Linq.JObject jObject)
            {
                return jObject.ToObject<Dictionary<string, int>>() ?? [];
            }

            return [];
        }
    }

    /// <summary>
    /// Prints the slot data to console for debugging/verification.
    /// </summary>
    public void PrintData()
    {
        Console.WriteLine("=== Slot Data ===");
        Console.WriteLine($"EndGoal: {EndGoal}");
        
        if (EndGoal == -1)
        {
            Console.WriteLine("EndGoal not found or invalid. Please report to the devs.");
        }

        if (EndGoal == 1)
        {
            Console.WriteLine("CollectiblesRequired:");
            Console.WriteLine("=================");
            foreach (var kvp in CollectiblesRequired)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            Console.WriteLine("=================");
        }
    }
}