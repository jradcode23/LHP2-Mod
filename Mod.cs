using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using LHP2_Archi_Mod.Template;
using LHP2_Archi_Mod.Configuration;
#if DEBUG
using System.Diagnostics;
#endif

namespace LHP2_Archi_Mod;

public class Mod : ModBase // <= Do not Remove.
{
    private readonly IModLoader _modLoader;
    private static IReloadedHooks? _hooks;
    public static ILogger? Logger;
    private readonly IMod _owner;
    private Config? Configuration { get; set; }
    private readonly IModConfig _modConfig;
    public static ArchipelagoHandler? LHP2_Archipelago;
    public static Game? GameInstance;
    public static nuint BaseAddress;

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        Logger = context.Logger;
        _owner = context.Owner;
        Configuration = context.Configuration;
        _modConfig = context.ModConfig;

#if DEBUG
        // Attaches debugger in debug mode; ignored in release.
        Debugger.Launch();
#endif

        GameInstance = new Game();
        BaseAddress = (nuint)Process.GetCurrentProcess().MainModule!.BaseAddress;

        if (Configuration == null)
            return;
        LHP2_Archipelago = new ArchipelagoHandler(Configuration.ArchipelagoOptions.Server, Configuration.ArchipelagoOptions.Port, Configuration.ArchipelagoOptions.Slot, Configuration.ArchipelagoOptions.Password);
        Logger.WriteLine($"[{_modConfig.ModId}] Mod Initialized with Server: {Configuration.ArchipelagoOptions.Server}, Port: {Configuration.ArchipelagoOptions.Port}, Slot: {Configuration.ArchipelagoOptions.Slot}");

        var thread1 = new Thread(start: () =>
        {
            while (true)
            {
                if (!ArchipelagoHandler.IsConnecting && !ArchipelagoHandler.IsConnected)
                {
                    LHP2_Archipelago.InitConnect();
                }
                Thread.Sleep(2500);
            }
        });
        thread1.Start();

    }

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        Configuration = configuration;
        Logger?.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    public static void InitOnMenu()
    {
        int hookCount = Game._asmHooks.Count;
        if (hookCount > 0)
        {
            Logger?.WriteLine($"Hooks already set up. Count: {hookCount}, skipping setup.");
            return;
        }
        Game.ModifyInstructions();
        if (Mod._hooks != null)
        {
            Logger?.WriteLine("Menu loaded, setting up hooks. Please wait for hook setup before loading a save file.");
            GameInstance!.SetupHooks(Mod._hooks!);
            Logger?.WriteLine("Hooks set up complete. You may now load a save file.");
        }
    }

    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}