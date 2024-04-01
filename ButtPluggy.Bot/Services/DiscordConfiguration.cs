namespace ButtPluggy.Bot.Services;
public class DiscordConfiguration {
	public required string Token { get; set; }
	public required ulong[] Channels { get; set; }
	public required string MessageFormat { get; set; }
}
