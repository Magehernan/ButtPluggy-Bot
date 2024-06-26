﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Nethereum.Hex.HexTypes;
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
		Web3 web3Ens;
		if (blockchainConfiguration.Value.Rpc.Equals(blockchainConfiguration.Value.EnsRpc)) {
			web3Ens = web3;
		} else {
			web3Ens = new(blockchainConfiguration.Value.EnsRpc);
		}
		ensService = web3Ens.Eth.GetEnsService();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		while (!stoppingToken.IsCancellationRequested) {
			logger.LogInformation("-----> BlockchainListener Start");
			try {
				await SubscribeToEventAsync(stoppingToken);
			} catch {
				await Task.Delay(2000);
			}
			logger.LogInformation("<----- BlockchainListener Stop");
		}
	}
	private async Task SubscribeToEventAsync(CancellationToken stoppingToken) {
		HexBigInteger currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
		ulong startBlock = (ulong)currentBlock.Value - 100;

		IBlockProgressRepository blockProgressRepository = new InMemoryBlockchainProgressRepository();

		BlockchainProcessor blockchainProcessor = web3.Processing.Logs.CreateProcessor(
			new EventLogProcessorHandler<TransferEventDTO>(ProcessTransferEvent),
			1,
			new() {
				Address = [blockchainConfiguration.Value.ButtPluggyAddress],
			},
			blockProgressRepository
		);

		logger.LogInformation("[Execute] Log Processor from: {address}", blockchainConfiguration.Value.ButtPluggyAddress);
		while (!stoppingToken.IsCancellationRequested) {
			try {
				await blockchainProcessor.ExecuteAsync(stoppingToken, startBlock, 10000).ConfigureAwait(false);
			} catch (OperationCanceledException) {
			} catch (Exception e) {
				logger.LogError(e, "Log Processor from: {address}", blockchainConfiguration.Value.ButtPluggyAddress);
				await Task.Delay(15000, stoppingToken).ConfigureAwait(false);
			}
		}
		logger.LogInformation("[Stop] Log Processor from: {address}", blockchainConfiguration.Value.ButtPluggyAddress);

	}

	private async void ProcessTransferEvent(EventLog<TransferEventDTO> log) {
		logger.LogInformation("Event Receive {event}", log.Event);
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