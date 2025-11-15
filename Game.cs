using Reloaded.Hooks.Definitions;
using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using Reloaded.Hooks.Definitions.Enums;
using Microsoft.VisualBasic;

namespace LHP_Archi_Mod;

public class Game
{

    public void GameLoaded()
    {
        Console.WriteLine("Testing InGame");
        while (!InGame())
        {
            System.Threading.Thread.Sleep(2000);
            Console.WriteLine("Retesting InGame");
        }
        Console.WriteLine("You are in-game!");
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