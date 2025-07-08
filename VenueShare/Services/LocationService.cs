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
    
    // Cache the last detected location to avoid spam logging
    private VenueLocation? lastDetectedLocation;

    // Housing districts and their territory IDs
    private readonly Dictionary<uint, string> housingDistricts = new()
    {
        // Limsa Lominsa - Mist
        { 339, "Mist" },      // Mist Ward 1-8
        { 340, "Mist" },      // Mist Ward 9-16  
        { 341, "Mist" },      // Mist Ward 17-24
        
        // Gridania - Lavender Beds
        { 342, "Lavender Beds" },  // Lavender Beds Ward 1-8
        { 343, "Lavender Beds" },  // Lavender Beds Ward 9-16
        { 344, "Lavender Beds" },  // Lavender Beds Ward 17-24
        
        // Ul'dah - Goblet
        { 345, "Goblet" },    // Goblet Ward 1-8
        { 346, "Goblet" },    // Goblet Ward 9-16
        { 347, "Goblet" },    // Goblet Ward 17-24
        
        // Kugane - Shirogane
        { 649, "Shirogane" }, // Shirogane Ward 1-8
        { 650, "Shirogane" }, // Shirogane Ward 9-16
        { 651, "Shirogane" }, // Shirogane Ward 17-24
        
        // Foundation - Empyreum
        { 979, "Empyreum" },  // Empyreum Ward 1-8
        { 980, "Empyreum" },  // Empyreum Ward 9-16
        { 981, "Empyreum" },  // Empyreum Ward 17-24
        
        // Individual Housing Instances (for when inside a house)
        // These are commonly in the 1000+ range and each represents a specific house instance
        // We'll use a broader range to catch individual housing instances
        // Note: These might all map to the same district based on the base territory
    };
    
    // Extended housing detection for individual house instances
    private readonly Dictionary<uint, uint> housingInstanceToDistrict = new()
    {
        // Map individual housing instance territory IDs to their base district territory ID
        // Shirogane individual houses (1200s range) -> Shirogane ward territories
        { 1249, 651 }, // Individual house -> Shirogane Ward 17-24
        { 1251, 651 }, // Individual house -> Shirogane Ward 17-24
        
        // Add more mappings as you discover them:
        // - Mist individual houses would likely map to 339-341
        // - Lavender Beds individual houses would likely map to 342-344  
        // - Goblet individual houses would likely map to 345-347
        // - Empyreum individual houses would likely map to 979-981
        // - Shirogane houses -> map to 649, 650, or 651 (based on ward)
        // 💡 HOW TO ADD MORE INDIVIDUAL HOUSE MAPPINGS:
        // 1. Visit an individual house in any district
        // 2. Run /locationtest to get the Territory ID  
        // 3. Add mapping: { TerritoryID, BaseDistrictID }
        //    - Mist houses -> map to 339, 340, or 341 (based on ward)
        //    - Lavender Beds houses -> map to 342, 343, or 344 (based on ward)  
        //    - Goblet houses -> map to 345, 346, or 347 (based on ward)
        //    - Empyreum houses -> map to 979, 980, or 981 (based on ward)
        //    - Shirogane houses -> map to 649, 650, or 651 (based on ward)
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
                return null;
            }

            var territoryId = clientState.TerritoryType;
            var territorySheet = dataManager.GetExcelSheet<TerritoryType>();
            
            if (!territorySheet.TryGetRow(territoryId, out var territoryRow))
            {
                return null;
            }

            // Check if we're in a housing district
            string? district = null;
            if (housingDistricts.TryGetValue(territoryId, out var knownDistrict))
            {
                district = knownDistrict;
            }
            else if (housingInstanceToDistrict.TryGetValue(territoryId, out var baseDistrictId) && 
                     housingDistricts.TryGetValue(baseDistrictId, out var mappedDistrict))
            {
                district = mappedDistrict;
            }
            else if (IsLikelyHousingInstance(territoryId))
            {
                // Try to determine district from territory ID patterns or HousingManager
                district = DetermineDistrictFromInstance(territoryId);
                if (string.IsNullOrEmpty(district))
                {
                    // Only log if we were previously in a housing district
                    if (lastDetectedLocation != null)
                    {
                        log.Info($"Left housing district (unknown territory ID: {territoryId})");
                        lastDetectedLocation = null;
                    }
                    return null;
                }
            }
            else
            {
                // Not a housing district at all
                if (lastDetectedLocation != null)
                {
                    log.Info("Left housing district");
                    lastDetectedLocation = null;
                }
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

            // Only log when location changes or when first entering a housing district
            if (lastDetectedLocation == null || 
                !LocationEquals(lastDetectedLocation, location))
            {
                log.Info($"Location changed: {location.District} Ward {location.Ward} Plot {location.Plot} on {location.Server}");
                lastDetectedLocation = location;
            }
            
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
                
                if (currentWard >= 0 && currentPlot >= 0)
                {
                    // Game values are 0-indexed, but display is 1-indexed, so add 1
                    var displayWard = currentWard + 1;
                    var displayPlot = currentPlot + 1;
                    log.Debug($"[HousingManager] Ward {displayWard}, Plot {displayPlot}");
                    return (displayWard, displayPlot);
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
                    log.Debug($"[AgentMap] Ward {wardFromMap} from map ID {mapInfo}");
                    // Still need to determine plot, use position-based detection
                    var plotFromPosition = GetPlotFromPosition();
                    return (wardFromMap, plotFromPosition);
                }
            }

            // Method 3: Analyze zone name or other UI elements
            var (ward, plot) = AnalyzeLocationFromUI();
            if (ward > 0)
            {
                log.Debug($"[UI Analysis] Ward {ward}, Plot {plot}");
                return (ward, plot);
            }

            // Method 4: Position-based estimation (less reliable)
            var positionBased = EstimateWardPlotFromPosition();
            if (positionBased.Ward > 0)
            {
                log.Debug($"[Position] Estimated Ward {positionBased.Ward}, Plot {positionBased.Plot}");
                return positionBased;
            }

            log.Debug("Could not determine ward/plot, using defaults");
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
                // Map IDs are 0-indexed, so add 1 for display
                return ((int)(mapId - 7) % 24) + 1;
            case 342: // Lavender Beds  
            case 343:
            case 344:
                // Map IDs are 0-indexed, so add 1 for display
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
            // Ensure we return 1-indexed values for display
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
            // Start with 1-indexed values for display
            var estimatedWard = 1;
            var estimatedPlot = 1;
            
            // Very rough calculation based on coordinate ranges
            // Each ward typically spans a certain coordinate range
            var x = position.X;
            var z = position.Z;
            
            // Estimate ward based on general coordinate ranges (needs refinement)
            // Ensure result is 1-indexed
            if (Math.Abs(x) > 100)
            {
                estimatedWard = (int)((Math.Abs(x) / 100) % 24) + 1;
            }
            
            // Estimate plot based on local coordinates within ward
            // Ensure result is 1-indexed
            if (Math.Abs(z) > 50)
            {
                estimatedPlot = (int)((Math.Abs(z) / 20) % 60) + 1;
            }
            
            // Only log at Debug level to avoid spam
            // log.Debug($"Position-based estimate: Ward {estimatedWard}, Plot {estimatedPlot} (at {x:F1}, {z:F1})");
            
            return (estimatedWard, estimatedPlot);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error in position-based estimation");
            return (1, 1);
        }
    }

    private bool LocationEquals(VenueLocation? loc1, VenueLocation? loc2)
    {
        if (loc1 == null || loc2 == null) return false;
        
        return loc1.Server == loc2.Server &&
               loc1.District == loc2.District &&
               loc1.Ward == loc2.Ward &&
               loc1.Plot == loc2.Plot;
    }

    public bool IsInHousingDistrict()
    {
        var territoryId = clientState.TerritoryType;
        
        // Check known housing districts
        if (housingDistricts.ContainsKey(territoryId))
            return true;
            
        // Check mapped individual housing instances
        if (housingInstanceToDistrict.ContainsKey(territoryId))
            return true;
            
        // Check if it's likely a housing instance based on ID patterns
        if (IsLikelyHousingInstance(territoryId))
            return true;
            
        return false;
    }

    private bool IsLikelyHousingInstance(uint territoryId)
    {
        // Individual housing instances are typically in higher ID ranges
        // Common ranges include 1000+, 1200+, etc.
        // This is a heuristic to catch individual house instances
        return territoryId >= 1000 && territoryId < 2000;
    }

    private string DetermineDistrictFromInstance(uint territoryId)
    {
        try
        {
            // Try to use HousingManager to determine which district we're in
            unsafe
            {
                var housingManager = HousingManager.Instance();
                if (housingManager != null)
                {
                    // Try to get the ward info which might help identify the district
                    var currentWard = housingManager->GetCurrentWard();
                    if (currentWard >= 0)
                    {
                        // Log this for debugging - you can use this info to build the mapping
                        log.Info($"🏠 Detected individual housing instance - Territory ID: {territoryId}, Ward: {currentWard + 1}");
                        
                        // Try to guess based on territory ID ranges and ward info
                        // Shirogane individual houses seem to be in 1200s range
                        if (territoryId >= 1240 && territoryId < 1260)
                        {
                            // Based on your data, these appear to be Shirogane houses
                            log.Info($"💡 Territory {territoryId} appears to be a Shirogane individual house - consider adding to housingInstanceToDistrict mapping");
                            return "Shirogane";
                        }
                        
                        // Other district ranges can be added as you discover them
                        // Mist might be in a different 1000s range
                        // Lavender Beds might be in another range, etc.
                    }
                }
            }
            
            // Fallback: log the unknown territory for manual mapping
            log.Info($"🔍 Unknown housing territory ID {territoryId} - please add to housingInstanceToDistrict mapping");
            return "";
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Error determining district for territory {territoryId}");
            return "";
        }
    }
}
