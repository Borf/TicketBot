using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace TicketBot.attributes
{
	internal class DoAdminCheck : PreconditionAttribute
	{
		public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
		{
			var config = services.GetRequiredService<Config>()[context.Guild.Id];
			if (context.User is SocketGuildUser guildUser)
			{
				if (config.AdminRole == 0 || guildUser.Roles.Any(gr => gr.Id == config.AdminRole))
					return Task.FromResult(PreconditionResult.FromSuccess());
				else
					return Task.FromResult(PreconditionResult.FromError("You're not an admin"));
			}
			return Task.FromResult(PreconditionResult.FromError("You're not an admin"));
		}
	}
}