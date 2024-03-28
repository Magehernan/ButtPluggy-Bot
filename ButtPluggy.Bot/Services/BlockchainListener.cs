﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Channels;

namespace ButtPluggy.Bot.Services;

public class BlockchainListener : BackgroundService {
	private readonly ILogger<BlockchainListener> logger;
	private readonly IOptions<BlockchainConfiguration> blockchainConfiguration;
	private readonly ENSService ensService;
	private readonly ChannelWriter<(string to, BigInteger tokenId)> channelWriter;
	private readonly Web3 web3;
	public BlockchainListener(ILogger<BlockchainListener> logger, Channel<(string, BigInteger)> channel, IOptions<BlockchainConfiguration> blockchainConfiguration) {
		this.logger = logger;
		channelWriter = channel.Writer;
		this.blockchainConfiguration = blockchainConfiguration;
		web3 = new(blockchainConfiguration.Value.Rpc);
		ensService = web3.Eth.GetEnsService();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		logger.LogInformation("-----> BlockchainListener Start");
		while (!stoppingToken.IsCancellationRequested) {
			try {
				await SubscribeToEventAsync(stoppingToken);
			} catch {
				await Task.Delay(30000);
			}
		}
		logger.LogInformation("<----- BlockchainListener Stop");
	}

	private async Task SubscribeToEventAsync(CancellationToken stoppingToken) {
		HexBigInteger currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
		NewFilterInput filterTransfers = Event<TransferEventDTO>.GetEventABI().CreateFilterInput(
			blockchainConfiguration.Value.ButtPluggyAddress,
			fromBlock: new(currentBlock)
		);
		StreamingWebSocketClient client = new(blockchainConfiguration.Value.WebSocket);
		EthLogsSubscription subscription = new(client);

		subscription.SubscriptionDataResponse += Subscription_SubscriptionDataResponse;

		await client.StartAsync();

		await subscription.SubscribeAsync(filterTransfers);

		while (!stoppingToken.IsCancellationRequested) {
			if (subscription.SubscriptionState == SubscriptionState.Unsubscribing) {
				return;
			}
			await Task.Delay(5000);
		}
	}

	private void Subscription_SubscriptionDataResponse(object? sender, StreamingEventArgs<FilterLog> log) {
		try {
			EventLog<TransferEventDTO> eventLog = log.Response.DecodeEvent<TransferEventDTO>();
			ProcessTransferEvent(eventLog);
		} catch (Exception e) {
			logger.LogError(e, "decoding log");
		}
	}

	private async void ProcessTransferEvent(EventLog<TransferEventDTO> log) {
		if (log.Event is null) {
			Console.WriteLine("Found not standard transfer log");
			return;
		}

		if (!AddressUtil.ZERO_ADDRESS.Equals(log.Event.From, StringComparison.OrdinalIgnoreCase)) {
			return;
		}
		string to;
		try {
			to = await ensService.ReverseResolveAsync(log.Event.To);
		} catch {
			to = $"{log.Event.To[..6]}...{log.Event.To[^4..]}";
		}
		channelWriter.TryWrite((to, log.Event.TokenId));
	}
}