using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace TicketBot
{
	public class InteractionHandler
	{
		private readonly DiscordSocketClient client;
		private readonly InteractionService handler;
		private readonly IServiceProvider services;
		private readonly IConfiguration configuration;
		private readonly TicketDb ticketDb;

		public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services, IConfiguration config, TicketDb ticketDb)
		{
			this.client = client;
			this.handler = handler;
			this.services = services;
			this.configuration = config;
			this.ticketDb = ticketDb;
		}

		public async Task InitializeAsync()
		{
			// Process when the client is ready, so we can register our commands.
			client.Ready += ReadyAsync;
			handler.Log += LogAsync;

			// Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
			await handler.AddModulesAsync(Assembly.GetEntryAssembly(), services);

			// Process the InteractionCreated payloads to execute Interactions commands
			client.InteractionCreated += HandleInteraction;
			client.MessageReceived += HandleMesage;
		}

		private async Task HandleMesage(SocketMessage msg)
		{
			if (msg.Author.IsBot)
				return;
			if (msg.Channel.GetChannelType() != ChannelType.DM)
				return;

			var dm = (SocketDMChannel)msg.Channel;

			if (msg.Attachments.Count == 0)
			{
				await msg.Channel.SendMessageAsync("Please send me images to attach to tickets", messageReference: new MessageReference(msg.Id));
				return;
			}

			var tickets = ticketDb.GetTickets();
			tickets = tickets.Where(t => t.ReporterId == msg.Author.Id && 
			t.CurrentStatus != Ticket.Status.Hidden && 
			t.CurrentStatus != Ticket.Status.Fixed).ToList();

			if(tickets.Count == 0)
			{
				await msg.Channel.SendMessageAsync("You currently do not have any open tickets", messageReference: new MessageReference(msg.Id));
				return;
			}

			var sb = new SelectMenuBuilder()
				.WithMinValues(1)
				.WithMaxValues(1)
				.WithCustomId("BugAttachImage")
				;
			foreach (var ticket in tickets)
				sb.AddOption(new SelectMenuOptionBuilder().WithValue(ticket.Id + "").WithLabel(ticket.ShortDescription));

			var cb = new ComponentBuilder()
				.WithSelectMenu(sb);

			await msg.Channel.SendMessageAsync("What ticket does this image go with?", components: cb.Build(), messageReference: new MessageReference(msg.Id));
		}
		private async Task LogAsync(LogMessage log)
			=> Console.WriteLine(log);

		private async Task ReadyAsync()
		{
			//await _handler.RegisterCommandsToGuildAsync(724054882717532171, true);
			await handler.RegisterCommandsToGuildAsync(885126454545891398, true);
//				await _handler.RegisterCommandsGloballyAsync(true);
		}

		private async Task HandleInteraction(SocketInteraction interaction)
		{
			try
			{
				Console.WriteLine("Got interaction from user " + interaction.User.Username);
				// Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
				var context = new SocketInteractionContext(client, interaction);

				// Execute the incoming command.
				var result = await handler.ExecuteCommandAsync(context, services);

				if (!result.IsSuccess)
				{
					Console.WriteLine("Error running interaction");
					Console.WriteLine(result.ToString());
					Console.WriteLine(result.Error.ToString());
					switch (result.Error)
					{
						case InteractionCommandError.UnmetPrecondition:
							// implement
							break;
						default:
							break;
					}
				}
			}
			catch
			{
				// If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
				// response, or at least let the user know that something went wrong during the command execution.
				if (interaction.Type is InteractionType.ApplicationCommand)
					await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
			}
		}
	}
}