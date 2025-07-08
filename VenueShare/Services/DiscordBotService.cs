using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VenueShare.Models;

namespace VenueShare.Services;

public class DiscordBotService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IPluginLog _log;
    private readonly Configuration _configuration;

    public DiscordBotService(Configuration configuration, IPluginLog log)
    {
        _configuration = configuration;
        _log = log;
        _httpClient = new HttpClient();
        
        // Set up default headers if auth token is provided
        if (!string.IsNullOrEmpty(_configuration.BotAuthToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration.BotAuthToken}");
        }
    }

    public async Task<bool> ShareLocationAsync(VenueLocation location, string requestedBy)
    {
        if (!_configuration.EnableVenueSharing || string.IsNullOrEmpty(_configuration.DiscordBotUrl))
        {
            _log.Warning("Venue sharing is disabled or Discord bot URL is not configured");
            return false;
        }

        try
        {
            var request = new LocationShareRequest
            {
                Location = location,
                DiscordChannelId = _configuration.DiscordChannelId,
                RequestedBy = requestedBy
            };

            var json = JsonConvert.SerializeObject(request, Formatting.Indented);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_configuration.DiscordBotUrl}/venue-search", content);
            
            if (response.IsSuccessStatusCode)
            {
                _log.Info($"Successfully shared location with Discord bot");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _log.Error($"Failed to share location. Status: {response.StatusCode}, Content: {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error sharing location with Discord bot");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
