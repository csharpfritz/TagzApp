using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TagzApp.Communication.Configuration;
using TagzApp.Communication.Extensions;
using TagzApp.Providers.Twitter.Configuration;

namespace TagzApp.Providers.Twitter;

public class StartTwitter : IConfigureProvider
{
	public async Task<IServiceCollection> RegisterServices(IServiceCollection services, CancellationToken cancellationToken = default)
	{
		// Load initial configuration
		var initialConfig = await BaseProviderConfiguration<TwitterConfiguration>.CreateFromConfigurationAsync<TwitterConfiguration>(ConfigureTagzAppFactory.Current);

		// Configure options for IOptionsMonitor
		services.Configure<TwitterConfiguration>(options =>
		{
			options.UpdateFrom(initialConfig);
		});

		// Add a configuration reload service that can be used to update the options
		services.AddSingleton<IConfigureOptions<TwitterConfiguration>, TwitterConfigurationSetup>();

		// Create HttpClientOptions from the configuration for HTTP client setup
		var httpClientOptions = new HttpClientOptions
		{
			BaseAddress = initialConfig.BaseAddress,
			Timeout = initialConfig.Timeout,
			DefaultHeaders = initialConfig.DefaultHeaders,
			UseHttp2 = initialConfig.UseHttp2
		};

		services.AddHttpClient<ISocialMediaProvider, TwitterProvider, HttpClientOptions>(httpClientOptions);
		services.AddSingleton<ISocialMediaProvider, TwitterProvider>();

		return services;
	}
}

/// <summary>
/// Handles configuration setup for TwitterConfiguration
/// </summary>
public class TwitterConfigurationSetup : IConfigureOptions<TwitterConfiguration>
{
	public void Configure(TwitterConfiguration options)
	{
		// This will be called when the configuration is first accessed
		var config = BaseProviderConfiguration<TwitterConfiguration>
			.CreateFromConfigurationAsync<TwitterConfiguration>(ConfigureTagzAppFactory.Current)
			.GetAwaiter()
			.GetResult();

		options.UpdateFrom(config);
	}
}
