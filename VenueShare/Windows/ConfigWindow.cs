using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace VenueShare.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("VenueShare Configuration###VenueShareConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        ImGui.Text("Discord Bot Configuration");
        ImGui.Separator();

        // Enable venue sharing checkbox
        var enableSharing = Configuration.EnableVenueSharing;
        if (ImGui.Checkbox("Enable Venue Sharing", ref enableSharing))
        {
            Configuration.EnableVenueSharing = enableSharing;
            Configuration.Save();
        }

        ImGui.Spacing();

        // Discord Bot URL
        var botUrl = Configuration.DiscordBotUrl;
        ImGui.Text("Discord Bot URL:");
        if (ImGui.InputText("##BotUrl", ref botUrl, 256))
        {
            Configuration.DiscordBotUrl = botUrl;
            Configuration.Save();
        }

        ImGui.Spacing();

        // Discord Channel ID
        var channelId = Configuration.DiscordChannelId;
        ImGui.Text("Discord Channel ID:");
        if (ImGui.InputText("##ChannelId", ref channelId, 64))
        {
            Configuration.DiscordChannelId = channelId;
            Configuration.Save();
        }

        ImGui.Spacing();

        // Bot Auth Token
        var authToken = Configuration.BotAuthToken;
        ImGui.Text("Bot Auth Token (Optional):");
        if (ImGui.InputText("##AuthToken", ref authToken, 256, ImGuiInputTextFlags.Password))
        {
            Configuration.BotAuthToken = authToken;
            Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Legacy settings
        var configValue = Configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("Random Config Bool", ref configValue))
        {
            Configuration.SomePropertyToBeSavedAndWithADefault = configValue;
            Configuration.Save();
        }

        var movable = Configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            Configuration.IsConfigWindowMovable = movable;
            Configuration.Save();
        }
    }
}
