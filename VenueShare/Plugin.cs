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

    private const string CommandName = "/pmycommand";
    private const string VenueShareCommand = "/venueshare";

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

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        CommandManager.AddHandler(VenueShareCommand, new CommandInfo(OnVenueShareCommand)
        {
            HelpMessage = "Share current venue location with Discord bot"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [VenueShare] ===A cool log message from Sample Plugin===
        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        DiscordBotService.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(VenueShareCommand);
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
            var location = LocationService.GetCurrentLocation();
            if (location == null)
            {
                Log.Info("Not currently in a housing district or unable to detect location");
                return;
            }

            var playerName = ClientState.LocalPlayer?.Name.TextValue ?? "Unknown Player";
            var success = await DiscordBotService.SendVenueSearchRequestAsync(location, playerName);
            
            if (success)
            {
                Log.Info($"Venue search request sent for {location.District} Ward {location.Ward} Plot {location.Plot} on {location.Server}");
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

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
