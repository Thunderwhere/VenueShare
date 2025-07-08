# Additional imports needed for the webhook server
from aiohttp import web, ClientSession
import json
import asyncio
from datetime import datetime

# Add this to your existing bot code - Webhook server setup
async def create_webhook_server():
    """Create the webhook server to handle requests from the FFXIV plugin"""
    app = web.Application()
    
    # Health check endpoint
    async def health_check(request):
        return web.json_response({
            "status": "OK", 
            "timestamp": datetime.utcnow().isoformat()
        })
    
    # Venue search webhook endpoint
    async def venue_search_webhook(request):
        try:
            data = await request.json()
            
            # Validate required fields
            if not data.get('location') or not data.get('discordChannelId'):
                return web.json_response(
                    {"error": "Missing required fields: location and discordChannelId"}, 
                    status=400
                )
            
            location = data['location']
            channel_id = data['discordChannelId']
            requested_by = data.get('requestedBy', 'Unknown Player')
            
            # Get Discord channel
            try:
                channel = bot.get_channel(int(channel_id))
                if not channel:
                    return web.json_response(
                        {"error": "Discord channel not found"}, 
                        status=404
                    )
            except ValueError:
                return web.json_response(
                    {"error": "Invalid Discord channel ID"}, 
                    status=400
                )
            
            # Search for venues at the location
            matching_venues = search_venues_by_location(location)
            
            # Create and send Discord embed
            embed = create_venue_location_embed(location, matching_venues, requested_by)
            
            # Send to Discord channel
            await channel.send(embed=embed)
            
            return web.json_response({
                "success": True,
                "venuesFound": len(matching_venues),
                "message": "Venue information posted to Discord"
            })
            
        except json.JSONDecodeError:
            return web.json_response(
                {"error": "Invalid JSON in request body"}, 
                status=400
            )
        except Exception as e:
            print(f"Error processing venue search webhook: {e}")
            return web.json_response(
                {"error": "Internal server error"}, 
                status=500
            )
    
    # Add routes
    app.router.add_get('/health', health_check)
    app.router.add_post('/venue-search', venue_search_webhook)
    
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
        
        # Check server/world match
        venue_world = venue_location.get('world', '').lower()
        if server and venue_world and server not in venue_world and venue_world not in server:
            continue
        
        # Check district match
        venue_district = venue_location.get('district', '').lower()
        if district and venue_district and district not in venue_district and venue_district not in district:
            continue
        
        # Check ward match (if provided)
        venue_ward = venue_location.get('ward')
        if ward and venue_ward and int(venue_ward) != int(ward):
            continue
        
        # Check plot match (if provided)
        venue_plot = venue_location.get('plot')
        if plot and venue_plot and int(venue_plot) != int(plot):
            continue
        
        matching_venues.append(venue)
    
    return matching_venues

def create_venue_location_embed(location, venues, requested_by):
    """Create a Discord embed for venue location results"""
    server = location.get('server', 'Unknown')
    district = location.get('district', 'Unknown')
    ward = location.get('ward', '?')
    plot = location.get('plot', '?')
    territory_name = location.get('territoryName', district)
    
    embed = discord.Embed(
        title="🏠 Venue Location Search",
        description=f"**Location:** {district} Ward {ward}, Plot {plot}\n**Server:** {server}\n**Territory:** {territory_name}",
        color=discord.Color.blue(),
        timestamp=datetime.utcnow()
    )
    
    embed.set_footer(text=f"Requested by {requested_by}")
    
    if not venues:
        embed.add_field(
            name="❌ No Venues Found",
            value="No venues were found at this location in the FFXIVVenues database.",
            inline=False
        )
    else:
        embed.add_field(
            name=f"✅ Found {len(venues)} Venue(s)",
            value=f"Here are the venues at this location:",
            inline=False
        )
        
        # Add up to 5 venues to avoid embed limits
        for i, venue in enumerate(venues[:5]):
            is_sfw = venue.get("sfw", True)
            sfw_label = "✅ SFW" if is_sfw else "🔞 NSFW"
            
            venue_info = f"**{venue['name']}** {sfw_label}\n"
            
            if venue.get('description'):
                description = venue['description']
                if isinstance(description, list):
                    description = description[0] if description else "No description"
                # Truncate description to fit embed limits
                if len(description) > 100:
                    description = description[:97] + "..."
                venue_info += f"{description}\n"
            
            if venue.get('tags'):
                venue_info += f"**Tags:** {', '.join(venue['tags'][:3])}\n"  # Limit tags
            
            links = []
            if venue.get('website'):
                links.append(f"[Website]({venue['website']})")
            if venue.get('discord'):
                links.append(f"[Discord]({venue['discord']})")
            
            if links:
                venue_info += f"**Links:** {' • '.join(links)}\n"
            
            # Mare Synchronos info
            if venue.get('mareCode') and venue.get('marePassword'):
                venue_info += f"**SynchShell:** `{venue['mareCode']}` / `{venue['marePassword']}`"
            
            embed.add_field(
                name=f"Venue {i+1}",
                value=venue_info,
                inline=True if len(venues) > 2 else False
            )
        
        if len(venues) > 5:
            embed.add_field(
                name="Additional Venues",
                value=f"... and {len(venues) - 5} more venues found at this location.",
                inline=False
            )
    
    return embed

# Modify your existing on_ready event
@bot.event
async def on_ready():
    if bot.user is not None:
        print(f"Logged in as {bot.user} (ID: {bot.user.id})")
    else:
        print("Logged in, but bot user is None.")
    
    await init_db()
    await fetch_venues()
    
    # Start the webhook server
    app = await create_webhook_server()
    runner = web.AppRunner(app)
    await runner.setup()
    
    # Use port from environment variable or default to 8080
    import os
    port = int(os.getenv('WEBHOOK_PORT', 8080))
    
    site = web.TCPSite(runner, '0.0.0.0', port)
    await site.start()
    
    print(f"Webhook server started on port {port}")
    print(f"Webhook URL: http://localhost:{port}/venue-search")
    
    print("Commands synced with Server!")
    synced = await tree.sync()
    print(f"Synced {len(synced)} command(s): {[cmd.name for cmd in synced]}")
    print("Commands synced with Discord!")
    print("Bot is ready!")
