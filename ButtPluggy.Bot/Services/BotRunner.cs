﻿using Discord;
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

	public BotRunner(ILogger<BotRunner> logger, Channel<(string, BigInteger)> channel, IHttpClientFactory httpClientFactory, IOptions<DiscordConfiguration> discordConfiguration) {
		this.logger = logger;
		this.httpClientFactory = httpClientFactory;
		this.discordConfiguration = discordConfiguration;
		channelReader = channel.Reader;
		client = new DiscordSocketClient();
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
		await Task.Delay(-1);
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

	private async Task SendMessageAsync(string to, BigInteger tokenId) {
		using HttpClient httpClient = httpClientFactory.CreateClient();
		ButtPluggyInfo info = await httpClient.GetFromJsonAsync<ButtPluggyInfo>($"https://buttpluggy.com/data/{tokenId:0000}.json");
		string messageText = $"""
			A wild buttpluggy [{info.Name}](https://buttpluggy.com/buttpluggy/{tokenId}) appeared, {to} capture it
			""";

		foreach (ulong channelId in discordConfiguration.Value.Channels) {
			if (await client.GetChannelAsync(channelId) is not IMessageChannel channelMessage) {
				continue;
			}

			await channelMessage.SendMessageAsync(messageText);
		}
	}

	private record struct ButtPluggyInfo(string Name, string Image);
}
