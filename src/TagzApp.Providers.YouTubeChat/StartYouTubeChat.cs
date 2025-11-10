using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TagzApp.Providers.YouTubeChat;

public class YouTubeChatConfigurationSetup : IConfigureOptions<YouTubeChatConfiguration>
{
	public void Configure(YouTubeChatConfiguration options)
	{
		// This will be called when the configuration is first accessed
		YouTubeChatConfiguration config;
		try
		{
			config = YouTubeChatConfiguration
				.CreateFromConfigurationAsync<YouTubeChatConfiguration>(ConfigureTagzAppFactory.Current)
				.GetAwaiter()
				.GetResult();
		}
		catch (System.Text.Json.JsonException)
		{
			// Configuration format change or decryption issue detected
			config = new YouTubeChatConfiguration();
			// Save the new format to database
			config.SaveToConfigurationAsync(ConfigureTagzAppFactory.Current)
				.GetAwaiter()
				.GetResult();
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to decrypt"))
		{
			// Encryption/decryption error - create new default configuration
			config = new YouTubeChatConfiguration();
			// Save the new format to database
			config.SaveToConfigurationAsync(ConfigureTagzAppFactory.Current)
				.GetAwaiter()
				.GetResult();
		}

		options.UpdateFrom(config);
	}
}

public class StartYouTubeChat : IConfigureProvider
{
	public async Task<IServiceCollection> RegisterServices(IServiceCollection services, CancellationToken cancellationToken = default)
	{
		// Register configuration setup
		services.AddSingleton<IConfigureOptions<YouTubeChatConfiguration>, YouTubeChatConfigurationSetup>();

		// Configure options with the setup class
		services.Configure<YouTubeChatConfiguration>(options => { /* options configured by setup class */ });

		// Register the provider
		services.AddSingleton<ISocialMediaProvider, YouTubeChatProvider>();

		return services;
	}
}
