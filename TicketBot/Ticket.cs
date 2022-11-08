using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TicketBot
{
	public class ChangeLogged<T>
	{
		public T Value { get; set; }
		public bool Changed { get; private set; } = false;
		public ChangeLogged(T val)
		{
			Value = val;
		}

		public void ResetChanged() { Changed = false; }
		public void SetChanged() { Changed = true; }
		public static implicit operator ChangeLogged<T>(T someValue)
		{
			return new ChangeLogged<T>(someValue) { Changed = true };
		}

		public static implicit operator T(ChangeLogged<T> myClassInstance)
		{
			return myClassInstance.Value;
		}

		public override string ToString()
		{
			return Value?.ToString() ?? "";
		}

	}

	public class Ticket
	{
		public enum Status
		{
			Reported,		//player just reported this
			Registered,		//moderator confirmed this
			Requested,		//sent to devs
			Approved,		//devs confirmed this will be fixed
			Fixed,			//fixed
			NotApproved,	//devs said won't be added
			Hidden,
		}
		/*
@startuml
 (*) -->[User reports a ticket] "Reported"
 "Reported" --> [Mods mark as duplicate\n<b>Message sent to user</b>] "Hidden"
 "Reported" --> [Mods deny bugreport\n<b>Message sent to user</b>] "Hidden"
 "Reported" --> [Mods approve\n<b>Message sent to user</b>] "Registered"
 "Registered" --> [Missy denies bugreport\n<b>Message sent to user</b>] "Hidden"
 "Registered" --> [Missy picks it up with the devs] "Requested"
 "Requested" -->[Devs fixed it] "Fixed"
 "Fixed" -->[Bug is not relevant anymore] "Hidden"
@enduml
		*/
		public class StatusHistoryEntry
		{
			public DateTime DateTime { get; set; } = DateTime.Now;
			public string User { get; set; } = "";
			public Status OldStatus { get; set; }
			public Status NewStatus { get; set; }
			public string Message { get; set; } = "";
		}

		public enum Regions
		{
			None,
			EU,
			CBT,
			NA_EL,
			NA_DP,
			SEA_EL,
			SEA_MP,
			SEA_MOF,
			SEA_VG
		}
		public int Row { get; set; }
		public ChangeLogged<ulong> DCMessageId { get; set; } = 0;
		public int Id { get; set; }
		public ChangeLogged<Status> CurrentStatus { get; set; } = Status.Reported;
		public ChangeLogged<List<StatusHistoryEntry>> StatusHistory { get; set; } = new List<StatusHistoryEntry>();
		public ChangeLogged<DateTime> ReportDate { get; set; } = DateTime.Now;
		public ChangeLogged<string> Reporter { get; set; } = "";
		public ChangeLogged<Regions> Region { get; set; } = Regions.None;
		public ChangeLogged<string> ShortDescription { get; set; } = ""; // this is the message shown on discord
		public ChangeLogged<string> LongDescription { get; set; } = "";
		public ChangeLogged<string> Channel { get; set; } = "";
		public ChangeLogged<string> Response { get; set; } = "";
		public ChangeLogged<string> Images { get; set; } = "";
		public ChangeLogged<string> Confirmation { get; set; } = "";
		public ChangeLogged<string> Server { get; set; } = "";
		public ChangeLogged<ulong> CharacterId { get; set; } = 0;
		public ChangeLogged<ulong> ReporterId { get; set; } = 0;
		public ChangeLogged<DateTimeOffset> DCLastEdit { get; set; } = DateTimeOffset.MinValue;

		public void ResetChange()
		{
			DCMessageId.ResetChanged();
			CurrentStatus.ResetChanged();
			ShortDescription.ResetChanged();
			LongDescription.ResetChanged();
			Channel.ResetChanged();
			Response.ResetChanged();
			Images.ResetChanged();
			DCLastEdit.ResetChanged();
		}


		public EmbedBuilder Embed()
		{
			var eb = new EmbedBuilder()
									.WithDescription("Ticket #" + Id.ToString() + " " + ShortDescription.ToString())
									.WithAuthor(Reporter.ToString())
									.WithTimestamp(ReportDate.Value)
									.WithFooter(CurrentStatus.ToString())
									;
			if (!string.IsNullOrEmpty(Images))
			{
				var images = Images.Value.Split("\n").Select(url => url.Trim()).ToList();
				eb.WithThumbnailUrl(images[0]);
			}


			if (Response.ToString().Length > 0)
				eb.AddField("Response", Response.ToString());
			return eb;
		}

		public override string ToString()
		{
			return ReportDate.Value.ToString("dd-MM-yyyy") + " - " + Reporter + " " + ShortDescription;
		}

		public async Task UpdateMessage(ITextChannel channel, TicketDb ticketDb, bool force = false)
		{
			bool newTicket = true;
			var embed = Embed().Build();
			var cb = new ComponentBuilder()
				.WithButton("Details", "TicketDetails:" + Id);
			var components = cb.Build();
			if (DCMessageId != 0)
			{
				try
				{
					//TODO: don't change if not needed
					var msg = await channel.GetMessageAsync(DCMessageId);
					bool update = force;
					if(msg.EditedTimestamp.HasValue)
						update |= msg.EditedTimestamp.Value.AddMilliseconds(-msg.EditedTimestamp.Value.Millisecond) != this.DCLastEdit;
					else
						update |= msg.Timestamp.AddMilliseconds(-msg.Timestamp.Millisecond) != this.DCLastEdit;

					if (update)
					{
						var newMsg = await channel.ModifyMessageAsync(DCMessageId, m =>
						{
							m.Embed = embed;
							m.Components = components;
						});
						DCLastEdit = newMsg.EditedTimestamp.Value;
						ticketDb.Update(this);
					}

					newTicket = false;
				}
				catch (Exception)
				{
					newTicket = true;
				}
			}

			if (newTicket)
			{
				var msg = await channel.SendMessageAsync(null, false, embed, components: components);
				DCMessageId = msg.Id;
				ticketDb.Update(this);

				await channel.CreateThreadAsync(this.ShortDescription, ThreadType.PublicThread, ThreadArchiveDuration.OneWeek, msg);
			}
		}

		public void SetStatus(Status newStatus, string author, string reason)
		{
			StatusHistory.Value.Add(new StatusHistoryEntry()
			{
				DateTime = DateTime.Now,
				User = author,
				OldStatus = CurrentStatus,
				NewStatus = newStatus,
				Message = reason
			});
			StatusHistory.SetChanged();
			CurrentStatus = newStatus;			
		}
	}
}
