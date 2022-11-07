using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Text.Json;
using TicketBot;

IServiceProvider services;
IConfiguration configuration;

configuration = new ConfigurationBuilder()
	.AddJsonFile("config/appsettings.json", optional: true)
	.AddUserSecrets<Program>()
	.Build();

GoogleCredential credential;
using(var stream = new FileStream("config/client_secrets.json", FileMode.Open, FileAccess.Read))
{
	credential = GoogleCredential.FromStream(stream);
	if (credential.IsCreateScopedRequired)
		credential = credential.CreateScoped(new string[] { SheetsService.Scope.Drive });
}


var service = new SheetsService(new BaseClientService.Initializer()
{
	HttpClientInitializer = credential,
	ApplicationName = "TicketBot"
});

var serviceBuilder = new ServiceCollection()
	.AddSingleton(configuration)
	.AddSingleton(new DiscordSocketConfig()
	{
		GatewayIntents = Discord.GatewayIntents.AllUnprivileged
			& ~Discord.GatewayIntents.GuildScheduledEvents
			& ~Discord.GatewayIntents.GuildInvites,
		//		AlwaysDownloadUsers = true,
	})
	.AddSingleton<DiscordSocketClient>()
	.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
	.AddSingleton<InteractionHandler>()
	.AddSingleton(new TicketDb(service))
	.AddSingleton<SheetChecker>()
	;

try
{
	serviceBuilder.AddSingleton<Config>(JsonSerializer.Deserialize<Config>(File.ReadAllText("config/config.json")) ?? new Config());
}catch(Exception)
{
	serviceBuilder.AddSingleton<Config>();
}
services = serviceBuilder.BuildServiceProvider();

{
	var client = services.GetRequiredService<DiscordSocketClient>();

	client.Log += LogAsync;

	// Here we can initialize the service that will register and execute our commands
	await services.GetRequiredService<InteractionHandler>()
		.InitializeAsync();

	// Bot token can be provided from the Configuration object we set up earlier
	await client.LoginAsync(TokenType.Bot, configuration["token"]);
	await client.StartAsync();

	//ewww
	while (client.ConnectionState != ConnectionState.Connected)
		await Task.Delay(100);

	await client.SetGameAsync("your bugs", null, ActivityType.Watching);

	var sheetChecker = services.GetRequiredService<SheetChecker>();
	await sheetChecker.StartAsync();

}





Console.WriteLine("Waiting");
await Task.Delay(-1);



async Task LogAsync(LogMessage message) => Console.WriteLine(message.ToString());