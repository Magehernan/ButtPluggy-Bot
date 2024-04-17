using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Numerics;
using System.Threading.Channels;

namespace ButtPluggy.Bot.Services;
public class BotRunner : BackgroundService {
	private readonly ILogger<BotRunner> logger;
	private readonly IHttpClientFactory httpClientFactory;
	private readonly IOptions<DiscordConfiguration> discordConfiguration;
	private readonly DiscordSocketClient client;
	private readonly ChannelReader<(string to, BigInteger tokenId)> channelReader;
	private readonly BigInteger[] lastProcessed = Enumerable.Repeat<BigInteger>(9999, 10).ToArray();
	private int lastProcessedId = 0;


	public BotRunner(ILogger<BotRunner> logger, Channel<(string, BigInteger)> channel, IHttpClientFactory httpClientFactory, IOptions<DiscordConfiguration> discordConfiguration) {
		this.logger = logger;
		this.httpClientFactory = httpClientFactory;
		this.discordConfiguration = discordConfiguration;
		channelReader = channel.Reader;
		client = new DiscordSocketClient(new() { GatewayIntents = GatewayIntents.None });
		client.Log += Log;
	}

	private Task Log(LogMessage msg) {
		logger.LogInformation("{msg}", msg);
		return Task.CompletedTask;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		logger.LogInformation("---->  Bot Start");
		await client.LoginAsync(TokenType.Bot, discordConfiguration.Value.Token);
		await client.StartAsync();
		client.Ready += Client_Ready;
		await Task.Delay(-1, stoppingToken);
		logger.LogInformation("<----  Bot Stop");
	}

	private Task Client_Ready() {
		///await SendMessageAsync("0xD3C7...0f4f0", 340);
		_ = ProcessMessagesAsync();

		return Task.CompletedTask;
	}

	private async Task ProcessMessagesAsync() {
		await foreach ((string to, BigInteger tokenId) in channelReader.ReadAllAsync()) {
			await SendMessageAsync(to, tokenId);
		}
	}

	private bool HasBeenProcessed(BigInteger tokenId) {
		for (int i = 0; i < 10; i++) {
			if (lastProcessed[i] == tokenId) {
				return true;
			}
		}

		lastProcessed[lastProcessedId % 10] = tokenId;
		lastProcessedId++;

		return false;
	}

	private async Task SendMessageAsync(string to, BigInteger tokenId) {
		if (HasBeenProcessed(tokenId)) {
			return;
		}

		string? name = null;
		string urlInfo = $"https://buttpluggy.com/data/{tokenId:0000}.json";
		try {
			using HttpClient httpClient = httpClientFactory.CreateClient();
			ButtPluggyInfo info = await httpClient.GetFromJsonAsync<ButtPluggyInfo>(urlInfo);
			name = info.Name;
		} catch (Exception e) {
			logger.LogError(e, "Get info from {urlInfo}", urlInfo);
		}
		name ??= tokenId.ToString();
		string messageText = string.Format(discordConfiguration.Value.MessageFormat, name, tokenId, to);
		logger.LogInformation("Sending message to {messageText}", messageText);

		foreach (ulong channelId in discordConfiguration.Value.Channels) {
			try {
				if (await client.GetChannelAsync(channelId) is not IMessageChannel channelMessage) {
					continue;
				}

				logger.LogInformation("Sending message to {channelId}", channelId);
				await channelMessage.SendMessageAsync(messageText);
			} catch (Exception e) {
				logger.LogError(e, "Sending message to channel {channel}", channelId);
			}
		}
	}

	private record struct ButtPluggyInfo(string Name, string Image);
}
