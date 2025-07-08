using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using VenueShare.Models;

namespace VenueShare.Services;

public class LocationService
{
    private readonly IClientState _clientState;
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _log;

    // Housing districts and their territory IDs
    private readonly Dictionary<uint, string> _housingDistricts = new()
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
        _clientState = clientState;
        _dataManager = dataManager;
        _log = log;
    }

    public VenueLocation? GetCurrentLocation()
    {
        try
        {
            var localPlayer = _clientState.LocalPlayer;
            if (localPlayer == null)
            {
                _log.Debug("Local player not available");
                return null;
            }

            var territoryId = _clientState.TerritoryType;
            var territorySheet = _dataManager.GetExcelSheet<TerritoryType>();
            
            if (!territorySheet.TryGetRow(territoryId, out var territoryRow))
            {
                _log.Debug($"Territory {territoryId} not found in data");
                return null;
            }

            // Check if we're in a housing district
            if (!_housingDistricts.TryGetValue(territoryId, out var district))
            {
                _log.Debug($"Territory {territoryId} is not a housing district");
                return null;
            }

            var location = new VenueLocation
            {
                Server = _clientState.LocalPlayer?.CurrentWorld.Value.Name.ExtractText() ?? "",
                District = district,
                TerritoryName = territoryRow.PlaceName.Value.Name.ExtractText() ?? "",
                TerritoryId = territoryId,
                Ward = GetCurrentWard(),
                Plot = GetCurrentPlot()
            };

            return location;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error getting current location");
            return null;
        }
    }

    private int GetCurrentWard()
    {
        // This is a simplified approach - in reality, you might need to use memory reading
        // or other techniques to get the exact ward number
        // For now, we'll return 1 as a placeholder
        return 1;
    }

    private int GetCurrentPlot()
    {
        // Similar to ward, this would require more advanced techniques to detect
        // the exact plot number. For now, return 1 as placeholder
        return 1;
    }

    public bool IsInHousingDistrict()
    {
        var territoryId = _clientState.TerritoryType;
        return _housingDistricts.ContainsKey(territoryId);
    }
}
