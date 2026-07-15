using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using LHP2_Archi_Mod.Template;
using LHP2_Archi_Mod.Configuration;
using System.Diagnostics;
#if DEBUG
// using System.Diagnostics;
#endif

namespace LHP2_Archi_Mod;

public class Mod : ModBase // <= Do not Remove.
{
    // private readonly IModLoader _modLoader;
    private static IReloadedHooks? _hooks;
    public static ILogger? Logger;
    // private readonly IMod _owner;
    public static Config? Configuration { get; set; }
    // private readonly IModConfig _modConfig;
    public static ArchipelagoHandler? LHP2_Archipelago;
    public static Game? GameInstance;
    public static nuint BaseAddress;

    public Mod(ModContext context)
    {
        // _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        Logger = context.Logger;
        // _owner = context.Owner;
        Configuration = context.Configuration;
        // _modConfig = context.ModConfig;

#if DEBUG
        // Attaches debugger in debug mode; ignored in release.
        // Debugger.Launch();
#endif

        GameInstance = new Game();
        BaseAddress = (nuint)Process.GetCurrentProcess().MainModule!.BaseAddress;
        bool is32Bit = IntPtr.Size == 4;

        if (Configuration == null)
        {
            Logger.WriteLineAsync("[LHP2.archipelago.mod] Configuration is null. Terminating Mod.");
            return;
        }
        SetUpAP(Configuration.ArchipelagoOptions.Server, Configuration.ArchipelagoOptions.Port, Configuration.ArchipelagoOptions.Slot, Configuration.ArchipelagoOptions.Password);
        Logger.WriteLineAsync("[LHP2.archipelago.mod] Mod Version: LHP2.archipelago.mod 1.1.2");

        while (true)
        {
            if (is32Bit)
            {
                break;
            }
            else
            {
                Logger.WriteLine("[LHP2.archipelago.mod] The game is not 32 bit. This may not be the standalone game. Please report to the dev if you are playing the standalone game.");
                Thread.Sleep(10000);
            }
        }

        var thread1 = new Thread(start: () =>
        {
            while (true)
            {
                if (!ArchipelagoHandler.IsConnecting && !ArchipelagoHandler.IsConnected)
                {
                    LHP2_Archipelago!.InitConnect();
                }
                Thread.Sleep(2500);
            }
        });
        thread1.Start();

    }

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        if (Configuration == null)
        {
            Logger!.WriteLineAsync("Configuration is null, cannot update.");
            return;
        }
        if (Configuration.ArchipelagoOptions.Port != configuration.ArchipelagoOptions.Port || Configuration.ArchipelagoOptions.Slot != configuration.ArchipelagoOptions.Slot)
        {
            Configuration = configuration;
            Logger!.WriteLine($"[LHP2.archipelago.mod] Config Updated: Applying");
            LHP2_Archipelago!.Disconnect();
            SetUpAP(Configuration.ArchipelagoOptions.Server, Configuration.ArchipelagoOptions.Port, Configuration.ArchipelagoOptions.Slot, Configuration.ArchipelagoOptions.Password);
        }
    }

    public static void SetUpAP(string server, int port, string slot, string password)
    {
        LHP2_Archipelago = new ArchipelagoHandler(server, port, slot, password);
        Logger!.WriteLineAsync($"[LHP2.archipelago.mod] Mod Initialized with Server: {server}, Port: {port}, Slot: {slot}");
    }

    public static bool InitOnMenu()
    {
        if (_hooks == null)
        {
            Game.PrintToLog("Hooks are Null. Please do not proceed and report this to the Dev.");
            return false;
        }
        int hookCount = Game._asmHooks.Count;
        if (hookCount > 0)
        {
            Game.PrintToLog($"Hooks already set up. Count: {hookCount}, skipping setup.");
            return true;
        }
        Game.ModifyInstructions();
        if (_hooks != null)
        {
            Game.PrintToLog("Menu loaded, setting up hooks. Please wait for hook setup before loading a save file.");
            GameInstance!.SetupHooks(Mod._hooks!);
            Game.PrintToLog("Hooks set up complete. You may now load a save file.");
            return true;
        }
        return false;
    }

    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}