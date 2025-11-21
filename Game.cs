using Reloaded.Hooks.Definitions;
using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions.Enums;
using Microsoft.VisualBasic;
using Archipelago.MultiClient.Net.Models;
using LHP_Archi_Mod.Template;
using LHP_Archi_Mod.Configuration;

namespace LHP_Archi_Mod;

public class Game
{
    public int PrevLevelID { get; private set; } = -1;
    public int LevelID { get; private set; } = -1;
    public int PrevMapID { get; private set; } = -1;
    public int MapID { get; private set; } = -1;

    public static void GameLoaded()
    {
        Console.WriteLine("Checking to see if save file is loaded");
        while (!PlayerControllable())
        {
            System.Threading.Thread.Sleep(2000);
            Console.WriteLine("Waiting for save file to load");
        }
        Console.WriteLine("Save File loaded!");
    }

    public static unsafe bool PlayerControllable()
    {
        try
        {
            byte** basePtr = (byte**)(Mod.BaseAddress + 0xC5763C);
            if (basePtr == null || *basePtr == null)
                return false;

            byte* finalPtr = (byte*)(*basePtr + 0x119C);
            if (finalPtr == null)
                return false;

            return *finalPtr == 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in InGame check: {ex.Message}");
            return false;
        }
    }

    public void ManageItem(int index, ItemInfo item)
    {
        var itemName = item.ItemName;
        var newItemID = (int)(item.ItemId - 400000);

        // implement logic for in shop or not controllable
        if (!PlayerControllable())
            return;

        switch(newItemID)
        {
                case < 450:
                    Console.WriteLine($"Unknown item received: {itemName}, {newItemID}");
                    break;
                case < 475:
                    // Todo: Update so that we don't have to subtract 450 every time
                    Level.ConvertIDToLeveData(newItemID - 450);
                    break;
                default:
                    Console.WriteLine($"Unknown item received: {itemName}, {newItemID}");
                    break;
        }
    }

    public void SetCurrentLevelID()
    {
        unsafe
        {
            int* levelIDPtr = (int*)(Mod.BaseAddress + 0xADDB7C);
            if (levelIDPtr == null) return;
            if (*levelIDPtr != LevelID)
            {
                PrevLevelID = LevelID;
                LevelID = *levelIDPtr;
                Console.WriteLine($"Level ID changed to: {LevelID}");
            }
        }
    }

    public void SetCurrentMapID()
    {
        unsafe
        {
            int* MapIDPtr = (int*)(Mod.BaseAddress + 0xC5B374);
            if (MapIDPtr == null) return;
            if (*MapIDPtr != MapID)
            {
                PrevMapID = MapID;
                MapID = *MapIDPtr;
                Console.WriteLine($"Map ID changed to: {MapID}");
            }
        }
    }

    public void GameLoop()
    {
        while (true)
        {
            SetCurrentLevelID();
            SetCurrentMapID();
            Thread.Sleep(500);
        }
    }
}