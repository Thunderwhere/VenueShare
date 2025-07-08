using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.Sheets;
using VenueShare.Models;

namespace VenueShare.Windows;

public class MainWindow : Window, IDisposable
{
    private string goatImagePath;
    private Plugin plugin;
    private VenueLocation? currentLocation;
    private VenueData[] foundVenues = Array.Empty<VenueData>();

    // We give this window a hidden ID using ##
    // So that the user will see "VenueShare" as window title,
    // but for ImGui the ID is "VenueShare##MainWindow"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("VenueShare##MainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
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
            
            ImGui.Spacing();
            
            if (ImGui.Button("Share Venue Location"))
            {
                ShareCurrentVenue();
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Search Local Venues"))
            {
                SearchLocalVenues();
            }
        }
        else if (plugin.LocationService.IsInHousingDistrict())
        {
            ImGui.TextUnformatted("In housing district but unable to detect exact location.");
        }
        else
        {
            ImGui.TextUnformatted("Not currently in a housing district.");
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Display found venues
        if (foundVenues.Length > 0)
        {
            ImGui.Text($"Found {foundVenues.Length} venue(s):");
            
            using (var child = ImRaii.Child("VenuesList", new Vector2(0, 200), true))
            {
                if (child.Success)
                {
                    foreach (var venue in foundVenues)
                    {
                        ImGui.Text($"• {venue.Name}");
                        if (!string.IsNullOrEmpty(venue.Description))
                        {
                            using (ImRaii.PushIndent(20f))
                            {
                                ImGui.TextWrapped(venue.Description);
                            }
                        }
                        ImGui.Spacing();
                    }
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Configuration and legacy content
        ImGui.TextUnformatted($"Configuration setting: {plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");

        if (ImGui.Button("Show Settings"))
        {
            plugin.ToggleConfigUI();
        }

        ImGui.Spacing();

        // Legacy goat image section
        using (var child = ImRaii.Child("LegacyContent", Vector2.Zero, true))
        {
            if (child.Success)
            {
                ImGui.TextUnformatted("Have a goat:");
                var goatImage = Plugin.TextureProvider.GetFromFile(goatImagePath).GetWrapOrDefault();
                if (goatImage != null)
                {
                    using (ImRaii.PushIndent(55f))
                    {
                        ImGui.Image(goatImage.ImGuiHandle, new Vector2(goatImage.Width, goatImage.Height));
                    }
                }
                else
                {
                    ImGui.TextUnformatted("Image not found.");
                }

                ImGuiHelpers.ScaledDummy(20.0f);

                // Player information
                var localPlayer = Plugin.ClientState.LocalPlayer;
                if (localPlayer == null)
                {
                    ImGui.TextUnformatted("Our local player is currently not loaded.");
                    return;
                }

                if (!localPlayer.ClassJob.IsValid)
                {
                    ImGui.TextUnformatted("Our current job is currently not valid.");
                    return;
                }

                ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation.ExtractText()}\"");

                var territoryId = Plugin.ClientState.TerritoryType;
                if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
                {
                    ImGui.TextUnformatted($"We are currently in ({territoryId}) \"{territoryRow.PlaceName.Value.Name.ExtractText()}\"");
                }
                else
                {
                    ImGui.TextUnformatted("Invalid territory.");
                }
            }
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
