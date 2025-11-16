using Reloaded.Hooks.Definitions;
using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions.Enums;
using Microsoft.VisualBasic;
using LHP_Archi_Mod.Template;
using LHP_Archi_Mod.Configuration;

namespace LHP_Archi_Mod;

public class Game
{

    public void GameLoaded()
    {
        Console.WriteLine("Checking to see if save file is loaded");
        while (!InGame())
        {
            System.Threading.Thread.Sleep(2000);
            Console.WriteLine("Checking again to see if save file is loaded");
        }
        Console.WriteLine("Save File loaded!");
    }


    public unsafe bool InGame()
    {
        try
        {
            byte** basePtr = (byte**)(Mod.BaseAddress + 0xC53934);
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
}