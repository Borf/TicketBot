using Discord;
using Discord.WebSocket;

namespace TicketBot
{
	public class SheetChecker
	{
		private Timer timer;
		private TicketDb TicketDb;
		private DiscordSocketClient Discord;

		public SheetChecker(TicketDb ticketDb, DiscordSocketClient discord)
		{
			this.TicketDb = ticketDb;
			this.Discord = discord;
			timer = new Timer(DoWorkAsync, null, Timeout.Infinite, 0);
		}

		public async Task StartAsync()
		{
			timer.Change(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5000));
		}

		private async void DoWorkAsync(object? state)
		{
			var tickets = TicketDb.GetChangedTickets();
			Console.WriteLine("Found " + tickets.Count + " changed tickets (" + TicketDb.CachedTickets.Count + " total)");
			foreach(var ticket in tickets)
			{
				if (ticket.CurrentStatus == Ticket.Status.Hidden ||
					ticket.CurrentStatus == Ticket.Status.Reported)
					continue;

				//var channel = Discord.Guilds.First(g => g.Id == 724054882717532171).TextChannels.First(c => c.Id == 1033524417491390525);
				var channel = Discord.GetGuild(885126454545891398).TextChannels.First(c => "#" + c.Name == ticket.Channel);
				await ticket.UpdateMessage(channel, TicketDb);
			}
		}

		public async Task Stop()
		{
			timer.Change(Timeout.Infinite, 0);
		}

	}
}