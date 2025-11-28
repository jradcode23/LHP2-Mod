using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using LHP_Archi_Mod.Template;
using LHP_Archi_Mod.Configuration;
#if DEBUG
using System.Diagnostics;
#endif

namespace LHP_Archi_Mod;

public class Mod : ModBase // <= Do not Remove.
{
    private readonly IModLoader _modLoader;
    private static IReloadedHooks? _hooks;
    private readonly ILogger _logger;
    private readonly IMod _owner;
    private Config? Configuration { get; set; }
    private readonly IModConfig _modConfig;

    public static LHP_Archipelago? LHP_Archipelago;
    public static Game? GameInstance;
    public static Level? Levels;
    public static nuint BaseAddress;

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        Configuration = context.Configuration;
        _modConfig = context.ModConfig;

#if DEBUG
        // Attaches debugger in debug mode; ignored in release.
        Debugger.Launch();
#endif

        // For more information about this template, please see
        // https://reloaded-project.github.io/Reloaded-II/ModTemplate/

        // If you want to implement e.g. unload support in your mod,
        // and some other neat features, override the methods in ModBase.

        // TODO: Implement some mod logic
        GameInstance = new Game();
        Levels = new Level();
        BaseAddress = (nuint)Process.GetCurrentProcess().MainModule!.BaseAddress;

        if (Configuration == null)
            return;
        LHP_Archipelago = new LHP_Archipelago(Configuration.ArchipelagoOptions.Server, Configuration.ArchipelagoOptions.Port, Configuration.ArchipelagoOptions.Slot, Configuration.ArchipelagoOptions.Password);
        _logger.WriteLine($"[{_modConfig.ModId}] Mod Initialized with Server: {Configuration.ArchipelagoOptions.Server}, Port: {Configuration.ArchipelagoOptions.Port}, Slot: {Configuration.ArchipelagoOptions.Slot}");

        var thread1 = new Thread(start: () =>
        {
            while (true)
            {
                if (!LHP_Archipelago.IsConnecting && !LHP_Archipelago.IsConnected)
                {
                    LHP_Archipelago.InitConnect();
                }
                Thread.Sleep(2500);
            }
        });
        thread1.Start();


    }

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        Configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    public static void InitOnConnect()
    {
        GameInstance?.ModifyInstructions();
        if (Mod._hooks != null)
            GameInstance?.SetupHooks(Mod._hooks);
    }

    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}