using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using TicketBot.attributes;

namespace TicketBot.modules
{
	public enum ExampleEnum
	{
		First,
		Second,
		Third,
		Fourth,
		[ChoiceDisplay("Twenty First")]
		TwentyFirst
	}
	// Interation modules must be public and inherit from an IInterationModuleBase
	public class TicketModule : InteractionModuleBase<SocketInteractionContext>
	{
		private InteractionHandler _handler;
		private TicketDb ticketDb;
		private Config config;

		// Constructor injection is also a valid way to access the dependencies
		public TicketModule(InteractionHandler handler, TicketDb ticketDb, Config config)
		{
			_handler = handler;
			this.ticketDb = ticketDb;
			this.config = config;
		}

		[SlashCommand("report", "Creates a ticket")]
		public async Task Report()
		{
			await RespondWithModalAsync<DescriptionModal>("desc_modal");
		}


		[DoAdminCheck]
		[SlashCommand("setuplog", "Sets up the logging channel")]
		public async Task SetupLogChannel([ChannelTypes(ChannelType.Text)]IChannel channel)
		{
			config[Context.Guild.Id].LogChannelId = channel.Id;
			config.Save();
			await ((ITextChannel)channel).SendMessageAsync("Channel set as logging channel");
			await RespondAsync("Configuration updated", ephemeral: true);
		}

		[DoAdminCheck]
		[SlashCommand("setupadminrole", "Sets up the admin role")]
		public async Task SetupAdminRole(IRole role)
		{
			config[Context.Guild.Id].AdminRole = role.Id;
			config.Save();
			await RespondAsync("Configuration updated", ephemeral: true);
		}

		[ModalInteraction("desc_modal")]
		public async Task Description(DescriptionModal modalData)
		{
			var newId = 1;
			var tickets = ticketDb.GetTickets();
			if(tickets.Count > 0)
				newId = tickets.Max(t => t.Id) + 1;//TODO: optimize this
			var ticket = new Ticket()
			{
				DCMessageId = 0,
				Id = newId,
				Channel = "#" + Context.Channel.Name,
				CurrentStatus = Ticket.Status.Reported,
				ShortDescription = modalData.ShortDesc,
				LongDescription = modalData.LongDesc,
				ReportDate = DateTime.Now,
				Reporter = Context.User.Username,
				ReporterId = Context.User.Id,
			};

			if(Context.User is SocketGuildUser user)
			{
				if (user.Roles.Any(r => r.Id == 991025896167211060)) { ticket.Region = Ticket.Regions.CBT; }
				if (user.Roles.Any(r => r.Id == 886995559418826823)) { ticket.Region = Ticket.Regions.EU; }
				if (user.Roles.Any(r => r.Id == 886995111341338705)) { ticket.Region = Ticket.Regions.NA_EL; }
				if (user.Roles.Any(r => r.Id == 886995485875904572)) { ticket.Region = Ticket.Regions.NA_DP; }
				if (user.Roles.Any(r => r.Id == 935837330395267132)) { ticket.Region = Ticket.Regions.SEA_EL; }
				if (user.Roles.Any(r => r.Id == 935837514000896000)) { ticket.Region = Ticket.Regions.SEA_MP; }
				if (user.Roles.Any(r => r.Id == 935837514625871953)) { ticket.Region = Ticket.Regions.SEA_MOF; }
				if (user.Roles.Any(r => r.Id == 978971770604355615)) { ticket.Region = Ticket.Regions.SEA_VG; }
			}

			if (!string.IsNullOrEmpty(modalData.char_id))
			{
				try
				{
					ticket.CharacterId = ulong.Parse(modalData.char_id);
				}
				catch (Exception) { }
			}

			ticketDb.Add(ticket);
			await RespondAsync("Thank you for reporting. A new ticket has been created with ID "+newId+". Please wait for a moderator to put through your ticket. I sent you a DM in case you want to add any pictures to this report", ephemeral : true);


			var eb = ticket.Embed()
				.AddField("Details", ticket.LongDescription);

			var cb = new ComponentBuilder()
				.WithButton("Approve", "TicketApprove:" + ticket.Id, ButtonStyle.Success)
				.WithButton("Duplicate", "TicketDuplicate:" + ticket.Id, ButtonStyle.Danger)
				.WithButton("Deny", "TicketDeny:" + ticket.Id, ButtonStyle.Danger)
				;
			var msg = await config[Context].LogChannel(Context).SendMessageAsync("New ticket received", embed: eb.Build(), components:cb.Build());

			await Context.User.SendMessageAsync("Your ticket has been received. If you would like to add any pictures or videos, just send a picture to this bot in DM, and you will get an option to add this to your report", embed: eb.Build());

			ticket.ResetChange();
			ticket.DCMessageId = msg.Id;
			ticketDb.Update(ticket);
		}


		[MessageCommand("Add to ticket")]
		public async Task AddToTicket(IMessage message)
		{
			var userMessage = message as SocketUserMessage;
			if (userMessage == null)
			{
				await RespondAsync(text: ":x: You can't add system messages to a ticket!", ephemeral: true);
				return;
			}
			if(userMessage.Attachments.Count == 0)
			{
				await RespondAsync(text: ":x: You can only add attachments to tickets through this", ephemeral: true);
				return;
			}

			if (userMessage.Channel.GetChannelType() == ChannelType.PublicThread)
			{
				var channel = (SocketThreadChannel)userMessage.Channel;
				ulong messageId = channel.Id;
				var ticket = ticketDb.GetTickets().First(t => t.DCMessageId == messageId);
				foreach(var attachment in userMessage.Attachments)
				{
					ticket.Images += "\n" + attachment.Url;
				}
				ticket.Images = ticket.Images.ToString().Trim();
				ticketDb.Update(ticket);
				Console.WriteLine(ticket);
				await RespondAsync(text: "Added!", ephemeral: true);

			}
			else
				await DeferAsync();
		}

		[DoAdminCheck]
		[ComponentInteraction("TicketApprove:*")]
		public async Task TicketApprove(int ticketId)
		{
			await RespondWithModalAsync<ClarificationModal>("TicketApproveReason:" + ticketId);
		}

		[DoAdminCheck]
		[ModalInteraction("TicketApproveReason:*")]
		public async Task TicketApproveReason(int ticketId, ClarificationModal modalData)
		{
			await DeferAsync();
			var ticket = ticketDb.GetTicket(ticketId);
			await Context.Channel.DeleteMessageAsync(ticket.DCMessageId);
			ticket.SetStatus(Ticket.Status.Registered, Context.User.Username, "");
			ticket.DCMessageId = 0;
			ticketDb.Update(ticket);
			var channel = Context.Guild.TextChannels.First(tc => "#" + tc.Name == ticket.Channel.Value);

			var eb = ticket.Embed();
			var cb = new ComponentBuilder()
				.WithButton("Details", "TicketDetails:"+ ticket.Id)
			;
			var msg = await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
			ticket.DCMessageId = msg.Id;
			ticketDb.Update(ticket);

			await channel.CreateThreadAsync(ticket.ShortDescription, ThreadType.PublicThread, ThreadArchiveDuration.OneWeek, msg);

			if (!string.IsNullOrEmpty(modalData.Reason.Trim()))
				await Context.Guild.Users.First(u => u.Id == ticket.ReporterId).SendMessageAsync("Your report of '" + ticket.ShortDescription + "' has been set through.\nWe ask you to update your report with the following information.\n\n" + modalData.Reason);
			else
				await Context.Guild.Users.First(u => u.Id == ticket.ReporterId).SendMessageAsync("Your report of '" + ticket.ShortDescription + "' has been set through.\nPlease be patient as we follow up on this report");
		}

		[DoAdminCheck]
		[ComponentInteraction("TicketDuplicate:*")]
		public async Task TicketDuplicate(int ticketId)
		{//TODO: autocomplete for bugs instead of reason
			await RespondWithModalAsync<ReasonModal>("TicketDuplicateReason:" + ticketId);
		}

		[DoAdminCheck]
		[ModalInteraction("TicketDuplicateReason:*")]
		public async Task TicketDuplicateReason(int ticketId, ReasonModal modalData)
		{
			await DeferAsync();
			var interaction = (SocketModal)Context.Interaction;
			var ticket = ticketDb.GetTicket(ticketId);
			ticket.SetStatus(Ticket.Status.Hidden, interaction.User.Username, "Marked as duplicate of " + modalData.Reason);
			await interaction.Channel.DeleteMessageAsync(ticket.DCMessageId);
			ticket.DCMessageId = 0;
			ticketDb.Update(ticket);
			await Context.Guild.Users.First(u => u.Id == ticket.ReporterId).SendMessageAsync("Your report of '" + ticket.ShortDescription + "' has been marked as duplicate. " + modalData.Reason);
		}

		[DoAdminCheck]
		[ComponentInteraction("TicketDeny:*")]
		public async Task TicketDeny(int ticketId)
		{//TODO: autocomplete for bugs instead of reason
			await RespondWithModalAsync<ReasonModal>("TicketDenyReason:" + ticketId);
		}

		[DoAdminCheck]
		[ModalInteraction("TicketDenyReason:*")]
		public async Task TicketTicketDenyReason(int ticketId, ReasonModal modalData)
		{
			await DeferAsync();
			var interaction = (SocketModal)Context.Interaction;
			var ticket = ticketDb.GetTicket(ticketId);
			ticket.SetStatus(Ticket.Status.Hidden, interaction.User.Username, "Deny: " + modalData.Reason);
			await interaction.Channel.DeleteMessageAsync(ticket.DCMessageId);
			ticket.DCMessageId = 0;
			ticketDb.Update(ticket);
			await Context.Guild.Users.First(u => u.Id == ticket.ReporterId).SendMessageAsync("Your report of '" + ticket.ShortDescription + "' has not been approved as a bugreport. " + modalData.Reason);
		}



		[ComponentInteraction("TicketDetails:*")]
		public async Task MoreDetails(int ticketId)
		{
			var Interaction = Context.Interaction as SocketMessageComponent;
			if (Interaction == null)
				return;
			var ticket = ticketDb.GetTicket(ticketId);

			string statusLog = "";
			foreach (var entry in ticket.StatusHistory.Value)
				statusLog += entry.DateTime.ToString("dd-MM-yyyy") + " -> " + entry.NewStatus + "\n";

			var eb = new EmbedBuilder()
				.WithTitle(ticket.ShortDescription.ToString())
				.AddField("Description", ticket.LongDescription.ToString()+" ")
				.AddField("Date Reported", ticket.ReportDate.Value.ToString("dd-MM-yyyy") + " ")
				.AddField("Reporter", ticket.Reporter.ToString() + " ")
				;
			if (!string.IsNullOrEmpty(ticket.Images))
			{
				eb.AddField(new EmbedFieldBuilder()
					.WithName("Images")
					.WithValue(ticket.Images.Value));
			}

			if (statusLog.Trim() != "")
				eb.AddField("State history", statusLog);


			var cb = new ComponentBuilder();
			bool isAdmin = false;
			if (Context.User is SocketGuildUser guildUser)
				if (guildUser.Roles.Any(r => r.Id == config[Context].AdminRole))
				{
					isAdmin = true;
					cb.WithButton("Edit", "EditDetails:" + ticket.Id);
					cb.WithButton("Hide", "TicketHide:" + ticket.Id, ButtonStyle.Danger);
					eb.AddField("Character ID", ticket.CharacterId.ToString());
					if (ticket.CurrentStatus == Ticket.Status.Registered) // ticket has just been approved by admin
					{
						cb.WithButton("Asked devs", "TicketAskedDev:" + ticket.Id);
					}
					if (ticket.CurrentStatus == Ticket.Status.Requested) // ticket has been sent to devs
					{
						cb.WithButton("Fixed Bug", "TicketFixed:" + ticket.Id, ButtonStyle.Success);
//						cb.WithButton("Accepted by devs", "TicketAccepted:" + ticket.Id, ButtonStyle.Success);
//						cb.WithButton("Denied by devs", "TicketDenied:" + ticket.Id, ButtonStyle.Danger);
					}
				}
			if (!isAdmin && ticket.Reporter == Context.User.Username && (
				ticket.CurrentStatus == Ticket.Status.Reported ||
				ticket.CurrentStatus == Ticket.Status.Registered ||
				ticket.CurrentStatus == Ticket.Status.Requested
				)) //TODO: security leak
			{
				cb.WithButton("Edit", "EditDetails:" + ticket.Id);
			}
			await RespondAsync(embed : eb.Build(), components : cb.Build(), ephemeral: true);
			var response = await GetOriginalResponseAsync();
		}

		[ComponentInteraction("EditDetails:*")]
		public async Task EditDetails(int ticketId)
		{
			var interaction = Context.Interaction as SocketMessageComponent;
			if (interaction == null)
				return;
			var ticket = ticketDb.GetTickets().First(t => t.Id == ticketId);

			var mb = new ModalBuilder()
				.WithCustomId("EditDetailsSave:" + ticket.Id)
				.WithTitle("Edit Ticket")
				.AddTextInput("Short Description", "short_desc", TextInputStyle.Short, "", value: ticket.ShortDescription)
				.AddTextInput("Long Description", "long_desc", TextInputStyle.Paragraph, "", value: ticket.LongDescription)
				;
			await RespondWithModalAsync(mb.Build());
			await interaction.Channel.DeleteMessageAsync(interaction.Message.Id);
		}

		[ModalInteraction("EditDetailsSave:*")]
		public async Task EditDetailsSave(int ticketId, DescriptionModal modalData)
		{
			//TODO: check if user is allowed to edit
			var interaction = Context.Interaction as SocketModal;
			if (interaction == null)
				return;
			await DeferAsync();
			var ticket = ticketDb.GetTickets().First(t => t.Id == ticketId);
			ticket.ShortDescription = modalData.ShortDesc;
			ticket.LongDescription = modalData.LongDesc;
			ticketDb.Update(ticket);
			await ticket.UpdateMessage((ITextChannel)interaction.Channel, ticketDb);
		}


		[ComponentInteraction("TicketHide:*")]
		public async Task TicketHide(int ticketId)
		{
			var interaction = Context.Interaction as SocketMessageComponent;
			await RespondWithModalAsync<ReasonModal>("TicketHideReason:" + ticketId);
		}
		
		[ComponentInteraction("BugAttachImage")]
		public async Task BugAttachImage(int[] selections)
		{
			if (selections.Length != 1)
			{
				await DeferAsync();
				return;
			}
			var interaction = (SocketMessageComponent)Context.Interaction;
			var msg = await interaction.Message.Channel.GetMessageAsync(interaction.Message.Reference.MessageId.Value);

			var ticket = ticketDb.GetTicket(selections[0]);
			foreach (var attachment in msg.Attachments)
			{
				ticket.Images += "\n" + attachment.Url;
			}
			ticket.Images = ticket.Images.ToString().Trim();
			ticketDb.Update(ticket);
			await ReplyAsync("Attachments have been added to this report", messageReference: new MessageReference(msg.Id));

			var eb = ticket.Embed()
				.AddField("Details", ticket.LongDescription)
				.AddField("Images: ", ticket.Images);

			var guild = Context.Guild; // this is null.....
			//TODO: get the guild-config where the sheetID matches the ticket's sheetID???
			guild = Context.Client.GetGuild(885126454545891398);

			if (ticket.CurrentStatus == Ticket.Status.Reported)
			{
				await guild.GetTextChannel(config[guild.Id].LogChannelId).ModifyMessageAsync(ticket.DCMessageId, m =>
				{
					m.Embed = eb.Build();
				});
			}
			else
			{
				await guild.TextChannels.First(c => "#" + c.Name == ticket.Channel).ModifyMessageAsync(ticket.DCMessageId, m =>
				{
					m.Embed = eb.Build();
				});
			}

		}

		[DoAdminCheck]
		[ModalInteraction("TicketHideReason:*")]
		public async Task TicketHideReason(int ticketId, ReasonModal modalData)
		{
			var interaction = (SocketModal)Context.Interaction;
			await DeferAsync();
			var ticket = ticketDb.GetTickets().First(t => t.Id == ticketId);
			await ((SocketTextChannel)interaction.Channel).DeleteMessageAsync(ticket.DCMessageId);
			ticket.DCMessageId = 0;
			ticket.SetStatus(Ticket.Status.Hidden, interaction.User.Username, modalData.Reason);
			ticketDb.Update(ticket);
		}

		[DoAdminCheck]
		[ComponentInteraction("TicketAskedDev:*")]
		public async Task TicketAskedDev(int ticketId)
		{
			var interaction = (SocketMessageComponent)Context.Interaction;
			await DeferAsync();
			var ticket = ticketDb.GetTicket(ticketId);
			if (ticket.CurrentStatus != Ticket.Status.Requested)
			{
				ticket.SetStatus(Ticket.Status.Requested, Context.User.Username, "");
				ticketDb.Update(ticket);
			}
			await ticket.UpdateMessage((ITextChannel)Context.Channel, ticketDb, true);
		}	
		
		[DoAdminCheck]
		[ComponentInteraction("TicketFixed:*")]
		public async Task TicketFixed(int ticketId)
		{
			var interaction = (SocketMessageComponent)Context.Interaction;
			var ticket = ticketDb.GetTicket(ticketId);

			var mb = new ModalBuilder()
				.WithCustomId("TicketFixedReason:" + ticketId)
				.WithTitle("What was the dev response?")
				.AddTextInput("response", "response", TextInputStyle.Paragraph, "", value: ticket.Response)
				;
			await RespondWithModalAsync(mb.Build());
		}

		[DoAdminCheck]
		[ModalInteraction("TicketFixedReason:*")]
		public async Task TicketFixedReason(int ticketId, DevResponseModal modalData)
		{
			await DeferAsync();
			var ticket = ticketDb.GetTicket(ticketId);
			if (ticket.CurrentStatus != Ticket.Status.Fixed)
			{
				ticket.SetStatus(Ticket.Status.Fixed, Context.User.Username, "");
				ticket.Response = modalData.Response;
				ticketDb.Update(ticket);
			}
			await ticket.UpdateMessage((ITextChannel)Context.Channel, ticketDb, true);
		}


		public class DescriptionModal : IModal
		{
			public string Title => "Ticket Descriptions";
			// Strings with the ModalTextInput attribute will automatically become components.
			[InputLabel("Short Description")]
			[ModalTextInput("short_desc", placeholder: "Enter a short description", maxLength: 60)]
			public string ShortDesc { get; set; } = string.Empty;

			// Additional paremeters can be specified to further customize the input.
			[InputLabel("Longer Description")]
			[ModalTextInput("long_desc", TextInputStyle.Paragraph, "Please enter a longer description")]
			public string LongDesc { get; set; } = string.Empty;

			// Additional paremeters can be specified to further customize the input.
			[InputLabel("Your character ID")]
			[RequiredInput(false)]
			[ModalTextInput("char_id", TextInputStyle.Short, "Please enter your character id (optional)")]
			public string char_id { get; set; } = String.Empty;
		}

		public class ReasonModal : IModal
		{
			public string Title => "Please give a reason";
			[InputLabel("Reason")]
			[ModalTextInput("reason", placeholder: "Enter a reason", maxLength: 100)]
			public string Reason { get; set; } = string.Empty;
		}
		public class ClarificationModal : IModal
		{
			public string Title => "Is any more information required?";
			[InputLabel("What more information?(sent to the reporter)")]
			[ModalTextInput("reason", placeholder: "Can you write more about .....", initValue:" ", minLength:0, maxLength: 100)]
			public string Reason { get; set; } = string.Empty;
		}

		public class DevResponseModal : IModal
		{
			public string Title => "What was the dev response?";
			[InputLabel("Response")]
			[ModalTextInput("response", placeholder: "Dev response", style:TextInputStyle.Paragraph)]
			public string Response { get; set; } = string.Empty;
		}

	}
}