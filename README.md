# VenueShare - FFXIV Plugin

A Dalamud plugin that integrates with Discord bots to share venue location information and search the FFXIVVenues.com database for nearby venues.

## Features

- **Automatic Location Detection**: Detects your current housing district, ward, and plot
- **Discord Integration**: Sends venue location data to Discord via webhook
- **Simple Configuration**: Easy setup through in-game settings window

## Commands

- `/venuesettings` - Open VenueShare configuration window
- `/venueshare` - Share current venue location to Discord
- `/locationtest` - Test location detection (shows current position info)

## Setup

1. **Install Plugin**: Place the plugin DLL in your Dalamud plugins folder
2. **Configure Settings**: Use `/venuesettings` to open settings and configure:
   - Discord Bot URL (e.g., `http://localhost:8080`)
   - - Currently only available on request
   - Discord Channel ID where venue info should be posted
   - Enable Venue Sharing checkbox

## How It Works

1. **Location Detection**: The plugin monitors your in-game position and territory to detect when you're in a housing district
2. **Ward/Plot Detection**: Uses multiple methods to determine the specific ward and plot number
3. **Discord Communication**: Sends HTTP requests to your Discord bot with location data
4. **Venue Lookup**: The Discord bot searches FFXIVVenues.com for venues at the detected location
5. **Discord Posting**: Results are posted to your configured Discord channel

## Technical Details

### Core Components

- **LocationService**: Handles detection of server, district, ward, and plot
- **DiscordBotService**: Manages HTTP communication with Discord bot
- **VenueLocation Model**: Data structure for location information
- **Configuration**: Persistent settings storage

### Location Detection Methods

- Memory scanning for ward/plot information
- Map territory analysis
- Position-based detection as fallback
- Server name detection from game state

## License

AGPL-3.0-or-later

* XIVLauncher, FINAL FANTASY XIV, and Dalamud have all been installed and the game has been run with Dalamud at least once.
* XIVLauncher is installed to its default directories and configurations.
  * If a custom path is required for Dalamud's dev directory, it must be set with the `DALAMUD_HOME` environment variable.
* A .NET Core 8 SDK has been installed and configured, or is otherwise available. (In most cases, the IDE will take care of this.)
