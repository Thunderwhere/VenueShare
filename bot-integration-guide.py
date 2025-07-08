# Complete integration example - Add these parts to your existing bot code

# 1. ADD THESE IMPORTS to your existing imports section:
from aiohttp import web
from datetime import datetime
import os

# 2. ADD THESE FUNCTIONS to your bot code (after your existing classes):

async def create_webhook_server():
    """Create the webhook server to handle requests from the FFXIV plugin"""
    app = web.Application()
    
    # Health check endpoint
    async def health_check(request):
        return web.json_response({
            "status": "OK", 
            "timestamp": datetime.utcnow().isoformat(),
            "venues_cached": len(venue_cache)
        })
    
    # Venue search webhook endpoint - this is what the FFXIV plugin calls
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
        if server and venue_world:
            # Flexible matching - check if either contains the other
            if server not in venue_world and venue_world not in server:
                continue
        
        # Check district match
        venue_district = venue_location.get('district', '').lower()
        if district and venue_district:
            if district not in venue_district and venue_district not in district:
                continue
        
        # Check ward match (if provided and not placeholder)
        venue_ward = venue_location.get('ward')
        if ward and venue_ward and ward != 1:  # Ignore placeholder ward 1
            try:
                if int(venue_ward) != int(ward):
                    continue
            except (ValueError, TypeError):
                pass
        
        # Check plot match (if provided and not placeholder)
        venue_plot = venue_location.get('plot')
        if plot and venue_plot and plot != 1:  # Ignore placeholder plot 1
            try:
                if int(venue_plot) != int(plot):
                    continue
            except (ValueError, TypeError):
                pass
        
        matching_venues.append(venue)
    
    return matching_venues

def create_venue_location_embed(location, venues, requested_by):
    """Create a Discord embed for venue location results"""
    server = location.get('server', 'Unknown')
    district = location.get('district', 'Unknown')
    ward = location.get('ward', '?')
    plot = location.get('plot', '?')
    territory_name = location.get('territoryName', district)
    
    # Create embed with location info
    embed = discord.Embed(
        title="🏠 FFXIV Venue Location Search",
        description=f"**Location:** {district} Ward {ward}, Plot {plot}\n**Server:** {server}\n**Territory:** {territory_name}",
        color=discord.Color.blue(),
        timestamp=datetime.utcnow()
    )
    
    embed.set_footer(text=f"Requested by {requested_by} via VenueShare plugin")
    
    if not venues:
        embed.add_field(
            name="❌ No Venues Found",
            value="No venues were found at this location in the FFXIVVenues database.\n\n" +
                  "This could mean:\n" +
                  "• No venues are registered at this exact location\n" +
                  "• The venue may be in a different ward/plot\n" +
                  "• The venue might not be listed on FFXIVVenues.com",
            inline=False
        )
    else:
        embed.add_field(
            name=f"✅ Found {len(venues)} Venue(s)",
            value=f"Here are the venues found at or near this location:",
            inline=False
        )
        
        # Add venues (limit to 3 to avoid embed size limits)
        for i, venue in enumerate(venues[:3]):
            is_sfw = venue.get("sfw", True)
            sfw_label = "✅ SFW" if is_sfw else "🔞 NSFW"
            
            venue_info = f"**{venue['name']}** {sfw_label}\n"
            
            # Add description
            if venue.get('description'):
                description = venue['description']
                if isinstance(description, list):
                    description = description[0] if description else "No description"
                if len(description) > 100:
                    description = description[:97] + "..."
                venue_info += f"{description}\n\n"
            
            # Add location details
            venue_loc = venue.get('location', {})
            if venue_loc:
                datacenter = venue_loc.get('dataCenter', '?')
                world = venue_loc.get('world', '?')
                v_district = venue_loc.get('district', '?')
                v_ward = venue_loc.get('ward', '?')
                v_plot = venue_loc.get('plot', '?')
                venue_info += f"**Location:** {datacenter} / {world} – {v_district}, Ward {v_ward}, Plot {v_plot}\n"
            
            # Add tags
            if venue.get('tags'):
                tags = venue['tags'][:3]  # Limit to 3 tags
                venue_info += f"**Tags:** {', '.join(tags)}\n"
            
            # Add links
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
                name=f"Venue {i+1}: {venue['name']}",
                value=venue_info,
                inline=False
            )
        
        if len(venues) > 3:
            embed.add_field(
                name="Additional Results",
                value=f"... and {len(venues) - 3} more venue(s) found. Use `/searchvenue` command for detailed browsing.",
                inline=False
            )
    
    return embed

# 3. REPLACE your existing on_ready event with this updated version:
@bot.event
async def on_ready():
    if bot.user is not None:
        print(f"Logged in as {bot.user} (ID: {bot.user.id})")
    else:
        print("Logged in, but bot user is None.")
    
    await init_db()
    await fetch_venues()
    
    # Start the webhook server for FFXIV plugin integration
    try:
        app = await create_webhook_server()
        runner = web.AppRunner(app)
        await runner.setup()
        
        # Use port from environment variable or default to 8080
        port = int(os.getenv('WEBHOOK_PORT', 8080))
        
        site = web.TCPSite(runner, '0.0.0.0', port)
        await site.start()
        
        print(f"✅ Webhook server started on port {port}")
        print(f"🌐 Webhook URL: http://localhost:{port}/venue-search")
        print(f"🔍 Health check: http://localhost:{port}/health")
    except Exception as e:
        print(f"❌ Failed to start webhook server: {e}")
        print("The bot will continue running without webhook support.")
    
    print("Commands synced with Server!")
    synced = await tree.sync()
    print(f"Synced {len(synced)} command(s): {[cmd.name for cmd in synced]}")
    print("Commands synced with Discord!")
    print("Bot is ready!")

# 4. OPTIONAL: Add a new slash command to test the venue search functionality
@tree.command(name="searchlocation", description="Search for venues at a specific location")
@app_commands.describe(
    server="Server/World name",
    district="Housing district (Mist, Lavender Beds, Goblet, Shirogane, Empyreum)",
    ward="Ward number (optional)",
    plot="Plot number (optional)"
)
async def searchlocation(interaction: Interaction, server: str, district: str, ward: int = None, plot: int = None):
    """Search for venues by location - similar to what the FFXIV plugin does"""
    
    # Create location object similar to plugin
    location = {
        'server': server,
        'district': district,
        'ward': ward,
        'plot': plot,
        'territoryName': district
    }
    
    # Search for venues
    matching_venues = search_venues_by_location(location)
    
    if not matching_venues:
        await interaction.response.send_message(
            f"No venues found at {district} Ward {ward or '?'}, Plot {plot or '?'} on {server}.", 
            ephemeral=True
        )
        return
    
    # Show results in dropdown like other commands
    view = VenueSelectView(matching_venues, interaction.user.id)
    await interaction.response.send_message(
        f"Found {len(matching_venues)} venue(s) at {district} on {server}. Choose one below:", 
        view=view
    )
    view.message = await interaction.original_response()
