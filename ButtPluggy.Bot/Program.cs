﻿using ButtPluggy.Bot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Numerics;
using System.Threading.Channels;

#if DEBUG
Console.Title = System.Reflection.Assembly.GetEntryAssembly()!.GetName().Name!;
#endif

HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings() {
	Args = args,
	EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
	ContentRootPath = Directory.GetCurrentDirectory(),
});
builder.Services.AddOptions<DiscordConfiguration>().BindConfiguration(nameof(DiscordConfiguration));
builder.Services.AddOptions<BlockchainConfiguration>().BindConfiguration(nameof(BlockchainConfiguration));
builder.Services.AddSingleton(Channel.CreateUnbounded<(string, BigInteger)>(new() { SingleReader = true }));
builder.Services.AddHttpClient();
builder.Services.AddHostedService<BotRunner>();
builder.Services.AddHostedService<BlockchainListener>();

IHost host = builder.Build();
host.Run();
