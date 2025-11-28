using System.ComponentModel;
using LHP2_Archi_Mod.Template.Configuration;

namespace LHP2_Archi_Mod.Configuration;

public class Config : Configurable<Config>
{
    /*
        User Properties:
            - Please put all of your configurable properties here.
    
        By default, configuration saves as "Config.json" in mod user config folder.    
        Need more config files/classes? See Configuration.cs
    
        Available Attributes:
        - Category
        - DisplayName
        - Description
        - DefaultValue

        // Technically Supported but not Useful
        - Browsable
        - Localizable

        The `DefaultValue` attribute is used as part of the `Reset` button in Reloaded-Launcher.
    */

    [DisplayName("AP Connection Options")]
    [Description("AP Connection Options")]
    public ArchipelagoOptions ArchipelagoOptions { get; set; } = new ArchipelagoOptions();
}
    public class ArchipelagoOptions
    {
        [DisplayName("Host IP")]
        [Description("Host address of the Archipelago server")]
        [DefaultValue("Archipelago.gg")]
        public string Server { get; set; } = "Archipelago.gg";

        [DisplayName("Port")]
        [Description("Port open for the Archipelago server")]
        [DefaultValue("55555")]
        public int Port { get; set; } = 55555;

        [DisplayName("Slot")]
        [Description("Slot user name used to connect to the Archipelago server")]
        [DefaultValue("Player1")]
        public string Slot { get; set; } = "Player1";

        [DisplayName("Password")]
        [Description("Password for the Archipelago server")]
        [DefaultValue("")]
        public string Password { get; set; } = "";
    }

    /// <summary>
    /// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
    /// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
    /// </summary>
    public class ConfiguratorMixin : ConfiguratorMixinBase
    { 
    //
    }
    