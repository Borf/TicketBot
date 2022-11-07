using Google.Apis.Requests;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Google.Apis.Sheets.v4.SpreadsheetsResource;

namespace TicketBot
{
	public class TicketDb
	{
		private string SpreadsheetId = "1NsS_OTMQtc2E4bSWJ2E84uaiQQzjZMBHlLAxHvfgz6g";
		private SheetsService Service { get; }
		public List<Ticket> CachedTickets { get; set; } = new();

		public TicketDb(SheetsService service)
		{
			Service = service;
		}

		public List<Ticket> GetTickets()
		{
			if (CachedTickets.Count == 0)
			{
				List<Ticket> ticketList = new List<Ticket>();
				var result = Service.Spreadsheets.Values.Get(SpreadsheetId, "Overview!A:R").Execute();
				for (int row = 1; row < result.Values.Count; row++)
				{
					while (result.Values[row].Count < 18)
						result.Values[row].Add("");
					var data = result.Values[row].Select(r => (string)r).ToList();

					var ticket = BuildTicket(data);
					ticket.Row = row;
					ticketList.Add(ticket);
				}

				CachedTickets = ticketList;
			}
			return CachedTickets;
		}

		public List<Ticket> GetChangedTickets()
		{
			List<Ticket> ticketList = new List<Ticket>();
			var result = Service.Spreadsheets.Values.Get(SpreadsheetId, "Overview!A:Q").Execute();
			for (int row = 1; row < result.Values.Count; row++)
			{
				var ticket = BuildTicket(result.Values[row].Select(r => (string)r).ToList());
				ticket.Row = row;
				ticketList.Add(ticket);
			}
			var changedTickets = ticketList.Where(t => CachedTickets.FirstOrDefault(tt => t.Id == tt.Id) != t).ToList();
			CachedTickets = ticketList;
			return changedTickets;
		}

		private static Ticket BuildTicket(List<string> data)
		{
			while (data.Count < 18)
				data.Add("");

			var ticket = new Ticket()
			{
				Id = int.Parse(data[0]),
				CurrentStatus = String.IsNullOrEmpty(data[1]) ? Ticket.Status.Reported : Enum.Parse<Ticket.Status>(data[1]),
				ReportDate = DateTime.Parse(data[2], new CultureInfo("fr-FR")),
				Reporter = data[3],
				ReporterId = ulong.Parse(data[4]),
				//					Region = Enum.Parse<Regions>(data[5]),
				Server = data[6],
				CharacterId = String.IsNullOrEmpty(data[7]) ? 0 : ulong.Parse(data[7]),
				DCMessageId = String.IsNullOrEmpty(data[8]) ? 0 : ulong.Parse(data[8]),
				DCLastEdit = String.IsNullOrEmpty(data[9]) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(data[9]),
				Channel = data[10],
				StatusHistory = JsonSerializer.Deserialize<List<Ticket.StatusHistoryEntry>>(data[11] ?? "[]") ?? new(),
				ShortDescription = data[12],
				LongDescription = data[13],
				Images = data[14],
				//data[15]
				Response = data[16],
				Confirmation = data[17],
			};
			if (Enum.TryParse(data[5], out Ticket.Regions r))
				ticket.Region = r;
			ticket.ResetChange();
			return ticket;
		}

		private void AddVariable<T>(ChangeLogged<T> t, Func<string> cellName, List<ValueRange> data, Func<T, string>? stringer = null)
		{
			if (t.Changed)
			{
				string value = "";
				if (stringer != null)
					value = stringer(t.Value);
				else
					value = t.ToString();
				data.Add(new ValueRange()
				{
					//Range = "Overview!C" + (ticket.StartRow + 2),
					Range = cellName(),
					Values = new[] { new[] { value } }
				});
			}
		}

		public void Update(Ticket ticket)
		{
			var images = ticket.Images.Value.Split("\n").Select(url => url.Trim()).ToList();

			var data = new List<ValueRange>();
			AddVariable(ticket.CurrentStatus, () => "B" + (ticket.Row + 1), data);
			AddVariable(ticket.ReportDate, () => "C" + (ticket.Row + 1), data, t => t.ToString("dd/MM/yyyy"));
			AddVariable(ticket.Reporter, () => "D" + (ticket.Row + 1), data);
			AddVariable(ticket.ReporterId, () => "E" + (ticket.Row + 1), data);
			AddVariable(ticket.Region, () => "F" + (ticket.Row + 1), data);
			AddVariable(ticket.Server, () => "G" + (ticket.Row + 1), data);
			AddVariable(ticket.CharacterId, () => "H" + (ticket.Row + 1), data);
			AddVariable(ticket.DCMessageId, () => "I" + (ticket.Row + 1), data);
			AddVariable(ticket.DCLastEdit, () => "J" + (ticket.Row + 1), data);
			AddVariable(ticket.Channel, () => "K" + (ticket.Row + 1), data);
			AddVariable(ticket.StatusHistory, () => "L" + (ticket.Row + 1), data, t => JsonSerializer.Serialize(t));
			AddVariable(ticket.ShortDescription, () => "M" + (ticket.Row + 1), data);
			AddVariable(ticket.LongDescription, () => "N" + (ticket.Row + 1), data);
			AddVariable(ticket.Images, () => "O" + (ticket.Row + 1), data);
			if (images.Count > 0)
			{
				AddVariable(ticket.Images, () => "P" + (ticket.Row + 1), data, t => "=IMAGE(\"" + images[0] + "\")");
				if (images.Count > 1 && ticket.Images.Changed)
				{
					var cols = new[] { "S", "T", "U", "V" };
					for (int i = 1; i < Math.Min(5, images.Count); i++)
					{
						data.Add(new ValueRange()
						{
							Range = cols[i - 1] + (ticket.Row + 1),
							Values = new[] { new[] { "=IMAGE(\"" + images[i] + "\")" } }
						});
					}
				}
			}
			AddVariable(ticket.Response, () => "Q" + (ticket.Row + 1), data);
			AddVariable(ticket.Confirmation, () => "R" + (ticket.Row + 1), data);

			if (data.Count > 0)
			{
				BatchUpdateValuesRequest body = new BatchUpdateValuesRequest()
				{
					ValueInputOption = "USER_ENTERED",
					Data = data,

				};
				var result = Service.Spreadsheets.Values.BatchUpdate(body, SpreadsheetId).Execute();
			}
			ticket.ResetChange();
		}

		internal void Add(Ticket ticket)
		{
			var vr = new ValueRange();
			vr.Range = "Overview!A:A";
			var result = Service.Spreadsheets.Values.Get(SpreadsheetId, "Overview!A:A").Execute();
			var rowCount = result.Values.Count;
			ticket.Row = rowCount;
			Console.WriteLine("Rowcount: " + rowCount);

			var ss = Service.Spreadsheets.Get(SpreadsheetId).Execute();
			var sheetId = ss.Sheets[0].Properties.SheetId;
			CachedTickets.Add(ticket);
			{
				ValueRange vr2 = new ValueRange() { Values = new[] { new[] {
					ticket.Id.ToString(),
					ticket.CurrentStatus.ToString(),
					ticket.ReportDate.Value.ToString("dd/MM/yyyy"),
					ticket.Reporter.ToString(),
					ticket.ReporterId.ToString(),
					ticket.Region.ToString(),
					ticket.Server.ToString(),
					ticket.CharacterId.ToString(),
					ticket.DCMessageId.ToString(),
					ticket.DCLastEdit.ToString(),
					ticket.Channel.ToString(),
					JsonSerializer.Serialize(ticket.StatusHistory.Value),
					ticket.ShortDescription.ToString(),
					ticket.LongDescription.ToString(),
					ticket.Images.ToString(),
					"",//ticket.imagepreviews
					ticket.Response.ToString(),
					ticket.Confirmation.ToString(),
				}} };
				var req = Service.Spreadsheets.Values.Update(vr2, SpreadsheetId, "Overview!A" + (rowCount + 1));
				req.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
				req.Execute();
			}
		}

		public Ticket GetTicket(int ticketId)
		{
			if (CachedTickets == null)
			{
				return GetTickets().First(t => t.Id == ticketId);
			}
			var row = CachedTickets.FirstOrDefault(t => t.Id == ticketId)?.Row ?? 0;
			if(row == 0)
			{
				throw new Exception("Row not found");
			}
			
			var result = Service.Spreadsheets.Values.Get(SpreadsheetId, "Overview!A"+(row+1)+":Q"+(row+1)).Execute();
			var data = result.Values[0].Select(r => (string)r).ToList();

			var ticket = BuildTicket(data);
			ticket.Row = row;
			return ticket;
		}
		
	}
}
