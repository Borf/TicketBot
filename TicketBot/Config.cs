using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TicketBot
{
	public class Config
	{
		public class GuildConfig
		{
			public ulong NewTicketChannelId { get; set; }
			public ulong LogChannelId { get; set; }
			public string SheetId { get; set; } = String.Empty; //TODO: make this per-channel
			public ulong AdminRole { get; set; }

			public ITextChannel LogChannel(SocketInteractionContext context) => context.Guild.GetTextChannel(LogChannelId);
		}

		public Dictionary<ulong, GuildConfig> GuildConfigs { get; set; } = new Dictionary<ulong, GuildConfig>();
		public GuildConfig this[ulong guildId]
		{
			get {
				if (!GuildConfigs.ContainsKey(guildId))
					GuildConfigs[guildId] = new GuildConfig();
				return GuildConfigs[guildId]; 
			}
			set { 
				if (!GuildConfigs.ContainsKey(guildId))
					GuildConfigs[guildId] = new GuildConfig();
				GuildConfigs[guildId] = value; 
			}
		}
		public GuildConfig this[SocketInteractionContext context]
		{
			get
			{
				if (!GuildConfigs.ContainsKey(context.Guild.Id))
					GuildConfigs[context.Guild.Id] = new GuildConfig();
				return GuildConfigs[context.Guild.Id];
			}
			set
			{
				if (!GuildConfigs.ContainsKey(context.Guild.Id))
					GuildConfigs[context.Guild.Id] = new GuildConfig();
				GuildConfigs[context.Guild.Id] = value;
			}
		}


		public void Save()
		{
			File.WriteAllText("config.json", JsonSerializer.Serialize(this));
		}
	}
}
