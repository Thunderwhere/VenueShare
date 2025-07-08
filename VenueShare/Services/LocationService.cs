using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using VenueShare.Models;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace VenueShare.Services;

public class LocationService
{
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    // Housing districts and their territory IDs
    private readonly Dictionary<uint, string> housingDistricts = new()
    {
        // Limsa Lominsa - Mist
        { 339, "Mist" },
        { 340, "Mist" },
        { 341, "Mist" },
        
        // Gridania - Lavender Beds
        { 342, "Lavender Beds" },
        { 343, "Lavender Beds" },
        { 344, "Lavender Beds" },
        
        // Ul'dah - Goblet
        { 345, "Goblet" },
        { 346, "Goblet" },
        { 347, "Goblet" },
        
        // Kugane - Shirogane
        { 649, "Shirogane" },
        { 650, "Shirogane" },
        { 651, "Shirogane" },
        
        // Foundation - Empyreum
        { 979, "Empyreum" },
        { 980, "Empyreum" },
        { 981, "Empyreum" }
    };

    public LocationService(IClientState clientState, IDataManager dataManager, IPluginLog log)
    {
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.log = log;
    }

    public VenueLocation? GetCurrentLocation()
    {
        try
        {
            var localPlayer = clientState.LocalPlayer;
            if (localPlayer == null)
            {
                log.Debug("Local player not available");
                return null;
            }

            var territoryId = clientState.TerritoryType;
            var territorySheet = dataManager.GetExcelSheet<TerritoryType>();
            
            if (!territorySheet.TryGetRow(territoryId, out var territoryRow))
            {
                log.Debug($"Territory {territoryId} not found in data");
                return null;
            }

            // Check if we're in a housing district
            if (!housingDistricts.TryGetValue(territoryId, out var district))
            {
                log.Debug($"Territory {territoryId} is not a housing district");
                return null;
            }

            var wardInfo = GetCurrentWardAndPlot();

            var location = new VenueLocation
            {
                Server = clientState.LocalPlayer?.CurrentWorld.Value.Name.ExtractText() ?? "",
                District = district,
                TerritoryName = territoryRow.PlaceName.Value.Name.ExtractText() ?? "",
                TerritoryId = territoryId,
                Ward = wardInfo.Ward,
                Plot = wardInfo.Plot
            };

            log.Info($"Detected location: {location.District} Ward {location.Ward} Plot {location.Plot} on {location.Server}");
            return location;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error getting current location");
            return null;
        }
    }

    private unsafe (int Ward, int Plot) GetCurrentWardAndPlot()
    {
        try
        {
            // Method 1: Try to get ward info from HousingManager
            var housingManager = HousingManager.Instance();
            if (housingManager != null)
            {
                var currentWard = housingManager->GetCurrentWard();
                var currentPlot = housingManager->GetCurrentPlot();
                
                if (currentWard > 0 && currentPlot > 0)
                {
                    log.Info($"[HousingManager] Detected Ward {currentWard}, Plot {currentPlot}");
                    return (currentWard, currentPlot);
                }
            }

            // Method 2: Try to get info from AgentMap
            var agentMap = AgentMap.Instance();
            if (agentMap != null)
            {
                var mapInfo = agentMap->CurrentMapId;
                var wardFromMap = ExtractWardFromMapId(mapInfo);
                if (wardFromMap > 0)
                {
                    log.Info($"[AgentMap] Detected Ward {wardFromMap} from map ID {mapInfo}");
                    // Still need to determine plot, use position-based detection
                    var plotFromPosition = GetPlotFromPosition();
                    return (wardFromMap, plotFromPosition);
                }
            }

            // Method 3: Analyze zone name or other UI elements
            var (ward, plot) = AnalyzeLocationFromUI();
            if (ward > 0)
            {
                log.Info($"[UI Analysis] Detected Ward {ward}, Plot {plot}");
                return (ward, plot);
            }

            // Method 4: Position-based estimation (less reliable)
            var positionBased = EstimateWardPlotFromPosition();
            if (positionBased.Ward > 0)
            {
                log.Info($"[Position] Estimated Ward {positionBased.Ward}, Plot {positionBased.Plot}");
                return positionBased;
            }

            log.Warning("Could not determine ward/plot, using defaults");
            return (1, 1); // Fallback
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error detecting ward/plot");
            return (1, 1);
        }
    }

    private int ExtractWardFromMapId(uint mapId)
    {
        // Housing district map IDs follow patterns - this is district-specific logic
        var territoryId = clientState.TerritoryType;
        
        // This is a simplified approach - actual map ID analysis would be more complex
        // Each housing district has different map ID ranges for different wards
        switch (territoryId)
        {
            case 339: // Mist
            case 340:
            case 341:
                return ((int)(mapId - 7) % 24) + 1; // Rough estimation
            case 342: // Lavender Beds  
            case 343:
            case 344:
                return ((int)(mapId - 7) % 24) + 1;
            default:
                return 0;
        }
    }

    private int GetPlotFromPosition()
    {
        try
        {
            var player = clientState.LocalPlayer;
            if (player == null) return 1;

            var position = player.Position;
            
            // This is a very rough position-based plot estimation
            // Real implementation would need detailed coordinate mapping for each district
            // For now, return a reasonable estimate based on position
            var x = (int)position.X;
            var z = (int)position.Z;
            
            // Simple grid-based estimation (this would need refinement)
            var plotEstimate = ((Math.Abs(x) / 50) + (Math.Abs(z) / 50)) % 60 + 1;
            return Math.Max(1, Math.Min(60, plotEstimate));
        }
        catch
        {
            return 1;
        }
    }

    private (int Ward, int Plot) AnalyzeLocationFromUI()
    {
        // This method would analyze UI elements, chat messages, or other game state
        // to determine current ward/plot. This is a placeholder for more advanced techniques.
        
        // Check if we can get info from the map or minimap title
        // Check recent system messages for housing district info
        // Analyze nearby housing placards or signs
        
        return (0, 0); // Not implemented yet
    }

    private (int Ward, int Plot) EstimateWardPlotFromPosition()
    {
        try
        {
            var player = clientState.LocalPlayer;
            if (player == null) return (1, 1);

            var position = player.Position;
            var territoryId = clientState.TerritoryType;
            
            // Basic position-based estimation
            // This is a rough approximation and would need fine-tuning for each district
            
            // Use position to estimate ward (housing districts are laid out in a grid)
            var estimatedWard = 1;
            var estimatedPlot = 1;
            
            // Very rough calculation based on coordinate ranges
            // Each ward typically spans a certain coordinate range
            var x = position.X;
            var z = position.Z;
            
            // Estimate ward based on general coordinate ranges (needs refinement)
            if (Math.Abs(x) > 100)
            {
                estimatedWard = (int)((Math.Abs(x) / 100) % 24) + 1;
            }
            
            // Estimate plot based on local coordinates within ward
            if (Math.Abs(z) > 50)
            {
                estimatedPlot = (int)((Math.Abs(z) / 20) % 60) + 1;
            }
            
            log.Debug($"Position-based estimate: Ward {estimatedWard}, Plot {estimatedPlot} (at {x:F1}, {z:F1})");
            
            return (estimatedWard, estimatedPlot);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error in position-based estimation");
            return (1, 1);
        }
    }

    public bool IsInHousingDistrict()
    {
        var territoryId = clientState.TerritoryType;
        return housingDistricts.ContainsKey(territoryId);
    }
}
