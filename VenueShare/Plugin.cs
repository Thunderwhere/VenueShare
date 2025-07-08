using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using VenueShare.Windows;
using VenueShare.Services;
using System;

namespace VenueShare;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/venuesettings";
    private const string VenueShareCommand = "/venueshare";
    private const string LocationTestCommand = "/locationtest";

    public Configuration Configuration { get; init; }
    public LocationService LocationService { get; init; }
    public DiscordBotService DiscordBotService { get; init; }

    public readonly WindowSystem WindowSystem = new("VenueShare");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Initialize services
        LocationService = new LocationService(ClientState, DataManager, Log);
        DiscordBotService = new DiscordBotService(Configuration, Log);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open VenueShare configuration window"
        });

        CommandManager.AddHandler(VenueShareCommand, new CommandInfo(OnVenueShareCommand)
        {
            HelpMessage = "Share current venue location with Discord bot"
        });

        CommandManager.AddHandler(LocationTestCommand, new CommandInfo(OnLocationTestCommand)
        {
            HelpMessage = "Test current location detection (shows ward/plot in chat)"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add initialization message to the log
        // Use /xllog to open the log window in-game
        Log.Information($"VenueShare plugin initialized - Discord integration ready");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        DiscordBotService.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(VenueShareCommand);
        CommandManager.RemoveHandler(LocationTestCommand);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private async void OnVenueShareCommand(string command, string args)
    {
        try
        {
            var currentLocation = LocationService.GetCurrentLocation();
            if (currentLocation == null)
            {
                Log.Info("Not currently in a housing district or unable to detect location");
                return;
            }

            var playerName = ClientState.LocalPlayer?.Name.TextValue ?? "Unknown Player";
            var success = await DiscordBotService.SendVenueSearchRequestAsync(currentLocation, playerName);
            
            if (success)
            {
                Log.Info($"Venue search request sent for {currentLocation.District} Ward {currentLocation.Ward} Plot {currentLocation.Plot} on {currentLocation.Server}");
            }
            else
            {
                Log.Warning("Failed to send venue search request");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing venue share command");
        }
    }

    private void OnLocationTestCommand(string command, string args)
    {
        try
        {
            var currentLocation = LocationService.GetCurrentLocation();
            if (currentLocation == null)
            {
                Log.Info("❌ Not currently in a housing district or unable to detect location");
                return;
            }

            Log.Info($"📍 Location Test Results:");
            Log.Info($"   Server: {currentLocation.Server}");
            Log.Info($"   District: {currentLocation.District}");
            Log.Info($"   Ward: {currentLocation.Ward}");
            Log.Info($"   Plot: {currentLocation.Plot}");
            Log.Info($"   Territory: {currentLocation.TerritoryName} (ID: {currentLocation.TerritoryId})");

            // Also show player position for debugging
            var player = ClientState.LocalPlayer;
            if (player != null)
            {
                var pos = player.Position;
                Log.Info($"   Player Position: X={pos.X:F1}, Y={pos.Y:F1}, Z={pos.Z:F1}");
            }
            
            Log.Info("📝 Use this info to verify ward/plot detection accuracy!");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error testing location detection");
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
