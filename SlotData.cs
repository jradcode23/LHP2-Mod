using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LHP2_Archi_Mod;

public class SlotData(Dictionary<string, object> slotData)
{
    public sealed class HorcruxLocation
    {
        [JsonProperty("player", Required = Required.Always)]
        public int Player { get; init; }

        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; init; } = "";

        [JsonProperty("id", Required = Required.Always)]
        public long ID { get; init; }
    }

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
    // 0 = Legacy, 1 = Collection; absent from older APWorlds (-1, same as Legacy)
    public int SeedGameVersion => GetIntSlotValue("GameVersion");
    // Determines what the APWorld version is in Slot Data. We multiply by 100 since Slot Data uses ints
    public int APWorldVersion => GetIntSlotValue("APWorldVersion");
    public Dictionary<string, HorcruxLocation> HorcruxLocations => GetDictionarySlotData("HorcruxLocations");

    public bool TryGetHorcruxLocation(string name, out HorcruxLocation? location)
    {
        if (HorcruxLocations.TryGetValue(name, out var value)
            && value is not null
            && value.Player > 0
            && value.ID > 0
            && !string.IsNullOrWhiteSpace(value.Name))
        {
            location = value;
            return true;
        }

        location = null;
        return false;
    }

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

    private Dictionary<string, HorcruxLocation> GetDictionarySlotData(string key)
    {
        if (!SlotDataDictionary.TryGetValue(key, out var value) || value is null)
            return [];

        try
        {
            return value switch
            {
                JObject jobject =>
                    jobject.ToObject<Dictionary<string, HorcruxLocation>>() ?? [],

                Dictionary<string, object> dictionary =>
                    JObject.FromObject(dictionary).ToObject<Dictionary<string, HorcruxLocation>>() ?? [],

                Dictionary<string, HorcruxLocation> dictionary =>
                    dictionary,

                _ => []
            };
        }
        catch (JsonException ex)
        {
            Game.PrintToLog($"[SlotData] Failed to parse dictionary slot value '{key}': {ex.Message}");
            return [];
        }
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
        Game.PrintToLog($"Game Version: {SeedGameVersion}");
        Game.PrintToLog($"APWorld Version: {APWorldVersion}");
        // PrintDictionary();


        if (EndGoal == -1 || NumberOfRequiredHorcruxes == -1 || NumberOfRequiredLevels == -1 || ShuffleJokeSpells == -1 || ShuffleGoldBrickPurchases == -1 || CheaperShops == -1 || FasterDuels == -1)
        {
            Game.PrintToLog("One or more values not found or invalid. Please report to the devs.");
        }
    }

    public void PrintDictionary()
    {
        Game.PrintToLog("Horcrux Locations:");
        Game.PrintToLog("=================");
        foreach (var kvp in HorcruxLocations)
        {
            Game.PrintToLog($"  {kvp.Key} => {kvp.Value.Name} (Player {kvp.Value.Player}) - location ID: {kvp.Value.ID}");
        }
        Game.PrintToLog("=================");

    }
}