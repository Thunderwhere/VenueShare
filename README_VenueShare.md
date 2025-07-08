# VenueShare - FFXIV Dalamud Plugin

A Dalamud plugin for Final Fantasy XIV that integrates with Discord bots to share venue location information and search the FFXIVVenues.com API.

## Features

- **Location Detection**: Automatically detects when you're in housing districts (Mist, Lavender Beds, Goblet, Shirogane, Empyreum)
- **Discord Integration**: Send venue location data to your Discord bot
- **Venue Search**: Search FFXIVVenues.com API for venues at your current location
- **Easy Configuration**: Simple UI to configure Discord bot settings

## Commands

- `/venueshare` - Share your current venue location with the Discord bot
- `/pmycommand` - Open the main plugin window

## Setup

### Plugin Configuration

1. Open the plugin configuration window by clicking "Show Settings" in the main window
2. Configure the following settings:
   - **Discord Bot URL**: The base URL of your Discord bot API (e.g., `https://your-bot.herokuapp.com`)
   - **Discord Channel ID**: The Discord channel ID where venue information should be posted
   - **Bot Auth Token**: (Optional) Authentication token for your bot API
   - **Enable Venue Sharing**: Toggle to enable/disable the venue sharing functionality

### Discord Bot API Endpoints

Your Discord bot should implement the following endpoint:

#### POST `/venue-search`

Receives venue location data and processes it.

**Request Body:**
```json
{
  "location": {
    "server": "Excalibur",
    "district": "Mist",
    "ward": 12,
    "plot": 45,
    "territoryName": "Mist",
    "territoryId": 339,
    "timestamp": "2025-07-08T10:30:00Z"
  },
  "discordChannelId": "123456789012345678",
  "requestedBy": "PlayerName"
}
```

**Expected Response:**
- `200 OK` - Venue search request processed successfully
- `4xx/5xx` - Error occurred (error message in response body)

### FFXIVVenues.com API Integration

The plugin also searches the FFXIVVenues.com API directly using the following parameters:
- `server`: World/Server name
- `district`: Housing district name
- `ward`: Ward number
- `plot`: Plot number

## Development

### Building the Plugin

1. Clone the repository
2. Open `VenueShare.sln` in Visual Studio or your preferred IDE
3. Build the solution
4. The plugin DLL will be output to `VenueShare/bin/x64/Debug/`

### Project Structure

```
VenueShare/
├── Models/
│   └── VenueLocation.cs          # Data models for venue information
├── Services/
│   ├── LocationService.cs        # Housing district and location detection
│   └── DiscordBotService.cs     # HTTP client for Discord bot communication
├── Windows/
│   ├── MainWindow.cs            # Main plugin UI
│   └── ConfigWindow.cs          # Configuration UI
├── Configuration.cs             # Plugin configuration
├── Plugin.cs                   # Main plugin class
└── VenueShare.csproj           # Project file
```

### Dependencies

- **Dalamud.NET.Sdk**: Core Dalamud plugin framework
- **Newtonsoft.Json**: JSON serialization for API communication

### Current Limitations

- Ward and plot detection currently returns placeholder values (1, 1)
- Exact location detection would require memory reading techniques beyond the current implementation
- The plugin detects housing districts but may need refinement for exact positioning

### Future Enhancements

- Implement precise ward/plot detection using memory scanning
- Add caching for venue search results
- Support for multiple Discord channels
- Venue bookmark/favorites system
- Integration with more venue databases

## License

This project is licensed under the AGPL-3.0-or-later license.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.
