using System;

namespace VenueShare.Models;

public class VenueLocation
{
    public string Server { get; set; } = "";
    public string District { get; set; } = "";
    public int Ward { get; set; }
    public int Plot { get; set; }
    public string TerritoryName { get; set; } = "";
    public uint TerritoryId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LocationShareRequest
{
    public VenueLocation Location { get; set; } = new();
    public string DiscordChannelId { get; set; } = "";
    public string RequestedBy { get; set; } = "";
}
