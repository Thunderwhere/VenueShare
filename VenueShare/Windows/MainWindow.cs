using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using VenueShare.Models;

namespace VenueShare.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin plugin;
    private VenueLocation? currentLocation;
    private VenueData[] foundVenues = Array.Empty<VenueData>();

    // We give this window a hidden ID using ##
    // So that the user will see "VenueShare" as window title,
    // but for ImGui the ID is "VenueShare##MainWindow"
    public MainWindow(Plugin plugin)
        : base("VenueShare##MainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Venue Sharing Section
        ImGui.Text("Venue Location Sharing");
        ImGui.Separator();

        // Get current location
        currentLocation = plugin.LocationService.GetCurrentLocation();
        
        if (currentLocation != null)
        {
            ImGui.Text($"Current Location: {currentLocation.District} on {currentLocation.Server}");
            ImGui.Text($"Ward: {currentLocation.Ward}, Plot: {currentLocation.Plot}");
            ImGui.Text($"Territory: {currentLocation.TerritoryName}");
            
            if (ImGui.Button("Share Current Venue"))
            {
                ShareCurrentVenue();
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Search Local Venues"))
            {
                SearchLocalVenues();
            }
        }
        else
        {
            ImGui.Text("No location detected. Make sure you're in a residential district.");
        }

        // Debug/Test Section
        ImGui.Spacing();
        ImGui.Text("Debug Tools");
        ImGui.Separator();
        
        if (ImGui.Button("Test Ward/Plot Detection"))
        {
            var location = plugin.LocationService.GetCurrentLocation();
            if (location != null)
            {
                Plugin.Log.Info($"Detected: {location.District} Ward {location.Ward}, Plot {location.Plot}");
            }
        }

        // Display found venues
        if (foundVenues.Length > 0)
        {
            ImGui.Spacing();
            ImGui.Text($"Found {foundVenues.Length} venues:");
            ImGui.Separator();
            
            foreach (var venue in foundVenues)
            {
                ImGui.Text($"• {venue.Name}");
                if (!string.IsNullOrEmpty(venue.Description))
                {
                    ImGui.Text($"  {venue.Description}");
                }
                ImGui.Spacing();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Plugin Settings
        if (ImGui.Button("Show Settings"))
        {
            plugin.ToggleConfigUI();
        }
    }

    private async void ShareCurrentVenue()
    {
        if (currentLocation == null) return;

        try
        {
            var playerName = Plugin.ClientState.LocalPlayer?.Name.TextValue ?? "Unknown Player";
            var success = await plugin.DiscordBotService.SendVenueSearchRequestAsync(currentLocation, playerName);
            
            if (success)
            {
                Plugin.Log.Info("Venue location shared successfully!");
            }
            else
            {
                Plugin.Log.Warning("Failed to share venue location.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error sharing venue location");
        }
    }

    private async void SearchLocalVenues()
    {
        if (currentLocation == null) return;

        try
        {
            foundVenues = await plugin.DiscordBotService.SearchFFXIVVenuesAsync(currentLocation);
            Plugin.Log.Info($"Found {foundVenues.Length} venues at current location");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error searching for venues");
            foundVenues = Array.Empty<VenueData>();
        }
    }
}