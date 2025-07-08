# VenueShare Plugin + Discord Bot Integration Setup Guide

## 🎯 Quick Setup Steps

### 1. Update Your Discord Bot

Add these imports to the top of your existing bot file:
```python
from aiohttp import web
from datetime import datetime
import os
```

### 2. Add Webhook Functions

Copy these functions into your bot code (after your existing classes):

```python
async def create_webhook_server():
    """Create the webhook server to handle requests from the FFXIV plugin"""
    app = web.Application()
    
    async def health_check(request):
        return web.json_response({
            "status": "OK", 
            "timestamp": datetime.utcnow().isoformat(),
            "venues_cached": len(venue_cache)
        })
    
    async def venue_search_webhook(request):
        try:
            data = await request.json()
            
            if not data.get('location') or not data.get('discordChannelId'):
                return web.json_response(
                    {"error": "Missing required fields"}, status=400
                )
            
            location = data['location']
            channel_id = data['discordChannelId']
            requested_by = data.get('requestedBy', 'Unknown Player')
            
            # Get Discord channel
            channel = bot.get_channel(int(channel_id))
            if not channel:
                return web.json_response(
                    {"error": "Discord channel not found"}, status=404
                )
            
            # Search venues
            matching_venues = search_venues_by_location(location)
            
            # Create embed and send
            embed = create_venue_location_embed(location, matching_venues, requested_by)
            await channel.send(embed=embed)
            
            return web.json_response({
                "success": True,
                "venuesFound": len(matching_venues),
                "message": "Venue information posted to Discord"
            })
            
        except Exception as e:
            print(f"Webhook error: {e}")
            return web.json_response({"error": "Internal server error"}, status=500)
    
    async def venue_search_direct(request):
        """Direct venue search endpoint for plugin to get venue data"""
        try:
            # Get query parameters
            server = request.query.get('server', '')
            district = request.query.get('district', '')
            ward = request.query.get('ward', 0)
            plot = request.query.get('plot', 0)
            
            # Build location object
            location = {
                'server': server,
                'district': district,
                'ward': int(ward) if ward else 0,
                'plot': int(plot) if plot else 0
            }
            
            # Search venues
            matching_venues = search_venues_by_location(location)
            
            return web.json_response({
                "venues": matching_venues
            })
            
        except Exception as e:
            print(f"Direct search error: {e}")
            return web.json_response({"error": "Internal server error"}, status=500)

    app.router.add_get('/health', health_check)
    app.router.add_post('/venue-search', venue_search_webhook)
    app.router.add_get('/search-venues', venue_search_direct)
    return app

def search_venues_by_location(location):
    """Search for venues matching the given location"""
    server = location.get('server', '').lower()
    district = location.get('district', '').lower()
    ward = location.get('ward')
    plot = location.get('plot')
    
    matching_venues = []
    
    for venue in venue_cache.values():
        venue_location = venue.get('location', {})
        
        # Server match
        venue_world = venue_location.get('world', '').lower()
        if server and venue_world and server not in venue_world and venue_world not in server:
            continue
        
        # District match
        venue_district = venue_location.get('district', '').lower()
        if district and venue_district and district not in venue_district and venue_district not in district:
            continue
        
        # Ward match (ignore placeholder value 1)
        venue_ward = venue_location.get('ward')
        if ward and venue_ward and ward != 1:
            try:
                if int(venue_ward) != int(ward):
                    continue
            except (ValueError, TypeError):
                pass
        
        # Plot match (ignore placeholder value 1)
        venue_plot = venue_location.get('plot')
        if plot and venue_plot and plot != 1:
            try:
                if int(venue_plot) != int(plot):
                    continue
            except (ValueError, TypeError):
                pass
        
        matching_venues.append(venue)
    
    return matching_venues

def create_venue_location_embed(location, venues, requested_by):
    """Create Discord embed for venue search results"""
    server = location.get('server', 'Unknown')
    district = location.get('district', 'Unknown')
    ward = location.get('ward', '?')
    plot = location.get('plot', '?')
    
    embed = discord.Embed(
        title="🏠 FFXIV Venue Location Search",
        description=f"**Location:** {district} Ward {ward}, Plot {plot}\\n**Server:** {server}",
        color=discord.Color.blue(),
        timestamp=datetime.utcnow()
    )
    
    embed.set_footer(text=f"Requested by {requested_by} via VenueShare plugin")
    
    if not venues:
        embed.add_field(
            name="❌ No Venues Found",
            value="No venues found at this location in the FFXIVVenues database.",
            inline=False
        )
    else:
        embed.add_field(
            name=f"✅ Found {len(venues)} Venue(s)",
            value="Here are the venues found:",
            inline=False
        )
        
        # Add up to 3 venues
        for i, venue in enumerate(venues[:3]):
            is_sfw = venue.get("sfw", True)
            sfw_label = "✅ SFW" if is_sfw else "🔞 NSFW"
            
            venue_info = f"**{venue['name']}** {sfw_label}\\n"
            
            if venue.get('description'):
                desc = venue['description']
                if isinstance(desc, list):
                    desc = desc[0] if desc else "No description"
                if len(desc) > 100:
                    desc = desc[:97] + "..."
                venue_info += f"{desc}\\n\\n"
            
            # Location
            venue_loc = venue.get('location', {})
            if venue_loc:
                datacenter = venue_loc.get('dataCenter', '?')
                world = venue_loc.get('world', '?')
                v_district = venue_loc.get('district', '?')
                v_ward = venue_loc.get('ward', '?')
                v_plot = venue_loc.get('plot', '?')
                venue_info += f"**Location:** {datacenter} / {world} – {v_district}, Ward {v_ward}, Plot {v_plot}\\n"
            
            # Links
            links = []
            if venue.get('website'):
                links.append(f"[Website]({venue['website']})")
            if venue.get('discord'):
                links.append(f"[Discord]({venue['discord']})")
            if links:
                venue_info += f"**Links:** {' • '.join(links)}"
            
            embed.add_field(
                name=f"Venue {i+1}",
                value=venue_info,
                inline=False
            )
        
        if len(venues) > 3:
            embed.add_field(
                name="More Results",
                value=f"... and {len(venues) - 3} more venue(s) found.",
                inline=False
            )
    
    return embed
```

### 3. Update Your on_ready Event

Replace your existing `@bot.event async def on_ready():` with this:

```python
@bot.event
async def on_ready():
    if bot.user is not None:
        print(f"Logged in as {bot.user} (ID: {bot.user.id})")
    else:
        print("Logged in, but bot user is None.")
    
    await init_db()
    await fetch_venues()
    
    # Start webhook server for FFXIV plugin
    try:
        app = await create_webhook_server()
        runner = web.AppRunner(app)
        await runner.setup()
        
        port = int(os.getenv('WEBHOOK_PORT', 8080))
        site = web.TCPSite(runner, '0.0.0.0', port)
        await site.start()
        
        print(f"✅ Webhook server started on port {port}")
        print(f"🌐 Webhook URL: http://localhost:{port}/venue-search")
    except Exception as e:
        print(f"❌ Failed to start webhook server: {e}")
    
    synced = await tree.sync()
    print(f"Synced {len(synced)} command(s)")
    print("Bot is ready!")
```

### 4. Install aiohttp

Run this command in your bot directory:
```bash
pip install aiohttp
```

### 5. Configure the FFXIV Plugin

In the VenueShare plugin configuration:
- **Discord Bot URL**: `http://localhost:8080` (or your server URL)
- **Discord Channel ID**: Your Discord channel ID where you want venue info posted
- **Enable Venue Sharing**: ✅ Checked

### 6. Test the Integration

1. Start your Discord bot (it should show webhook server starting)
2. Build and install the VenueShare plugin in FFXIV
3. Go to a housing district in-game
4. Use `/venueshare` command or click "Share Venue Location" in the plugin window
5. Check your Discord channel for the venue search results!

## 🚀 Usage

**In FFXIV:**
- `/venuesettings` - Open plugin configuration window
- `/venueshare` - Share current location with Discord
- `/locationtest` - Test location detection
- Open plugin window for manual venue search

**In Discord:**
- Bot will automatically post venue search results
- Results include venue details, links, and SynchShell info
- Shows "No venues found" if location doesn't match any venues

## 🔧 Troubleshooting

**Plugin can't connect to bot:**
- Check Discord Bot URL in plugin config
- Make sure bot is running and webhook server started
- Check firewall/port access

**No venues found:**
- Plugin currently uses placeholder ward/plot values (1, 1)
- Results are filtered by server and district
- Venue database might not have venues at that exact location

**Discord channel not found:**
- Verify Discord Channel ID is correct
- Make sure bot has permissions to post in that channel
