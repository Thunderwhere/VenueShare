// Example Discord Bot Implementation (Node.js/Express)
// This is a sample implementation showing how your Discord bot should handle the VenueShare plugin requests

const express = require('express');
const axios = require('axios');
const { Client, GatewayIntentBits, EmbedBuilder } = require('discord.js');

const app = express();
app.use(express.json());

// Discord bot setup
const client = new Client({ 
    intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMessages] 
});

// Route to handle venue search requests from the FFXIV plugin
app.post('/venue-search', async (req, res) => {
    try {
        const { location, discordChannelId, requestedBy } = req.body;
        
        // Validate request
        if (!location || !discordChannelId) {
            return res.status(400).json({ error: 'Missing required fields' });
        }

        // Search FFXIVVenues.com API
        const venues = await searchFFXIVVenues(location);
        
        // Get Discord channel
        const channel = await client.channels.fetch(discordChannelId);
        if (!channel) {
            return res.status(404).json({ error: 'Discord channel not found' });
        }

        // Create and send Discord embed
        const embed = createVenueEmbed(location, venues, requestedBy);
        await channel.send({ embeds: [embed] });

        res.status(200).json({ 
            success: true, 
            venuesFound: venues.length,
            message: 'Venue information posted to Discord'
        });

    } catch (error) {
        console.error('Error processing venue search:', error);
        res.status(500).json({ error: 'Internal server error' });
    }
});

// Function to search FFXIVVenues.com API
async function searchFFXIVVenues(location) {
    try {
        const params = new URLSearchParams({
            server: location.server,
            district: location.district,
            ward: location.ward.toString(),
            plot: location.plot.toString()
        });

        const response = await axios.get(`https://ffxivvenues.com/api/venues?${params}`);
        return response.data.venues || [];
    } catch (error) {
        console.error('Error searching FFXIV venues:', error);
        return [];
    }
}

// Function to create Discord embed
function createVenueEmbed(location, venues, requestedBy) {
    const embed = new EmbedBuilder()
        .setTitle(`🏠 Venue Search Results`)
        .setDescription(`**Location:** ${location.district} Ward ${location.ward}, Plot ${location.plot}\\n**Server:** ${location.server}`)
        .setColor(0x5865F2)
        .setFooter({ text: `Requested by ${requestedBy}` })
        .setTimestamp();

    if (venues.length === 0) {
        embed.addFields({ 
            name: 'No Venues Found', 
            value: 'No venues were found at this location in the FFXIVVenues database.' 
        });
    } else {
        venues.slice(0, 5).forEach((venue, index) => { // Limit to 5 venues to avoid embed limits
            embed.addFields({
                name: `${venue.name}`,
                value: `${venue.description ? venue.description.substring(0, 100) + '...' : 'No description available'}\\n` +
                       `${venue.website ? `[Website](${venue.website})` : ''}` +
                       `${venue.discord ? ` • [Discord](${venue.discord})` : ''}` +
                       `${venue.tags && venue.tags.length > 0 ? `\\n**Tags:** ${venue.tags.join(', ')}` : ''}`,
                inline: false
            });
        });

        if (venues.length > 5) {
            embed.addFields({
                name: 'Additional Venues',
                value: `... and ${venues.length - 5} more venues found.`,
                inline: false
            });
        }
    }

    return embed;
}

// Discord bot event handlers
client.once('ready', () => {
    console.log(`Discord bot logged in as ${client.user.tag}!`);
});

client.on('error', console.error);

// Start server
const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
    console.log(`Venue search API server running on port ${PORT}`);
});

// Login to Discord
client.login(process.env.DISCORD_BOT_TOKEN);

// Additional webhook endpoint for testing
app.get('/health', (req, res) => {
    res.json({ status: 'OK', timestamp: new Date().toISOString() });
});

module.exports = app;
