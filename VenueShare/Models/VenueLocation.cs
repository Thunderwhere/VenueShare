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

public class VenueSearchRequest
{
    public VenueLocation Location { get; set; } = new();
    public string DiscordChannelId { get; set; } = "";
    public string RequestedBy { get; set; } = "";
}

public class FFXIVVenuesApiResponse
{
    public VenueData[] Venues { get; set; } = Array.Empty<VenueData>();
}

public class VenueData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public VenueLocation Location { get; set; } = new();
    public string Website { get; set; } = "";
    public string Discord { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
}
