using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace VenueShare;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    
    // Discord Bot Configuration
    public string DiscordBotUrl { get; set; } = "";
    public string DiscordChannelId { get; set; } = "";
    public string BotAuthToken { get; set; } = "";
    public bool EnableVenueSharing { get; set; } = true;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}