using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TagzApp.Communication.Extensions;
using TagzApp.Communication.Configuration;
using TagzApp.Providers.Mastodon.Configuration;

namespace TagzApp.Providers.Mastodon;

public class MastodonConfigurationSetup : IConfigureOptions<MastodonConfiguration>
{
	public void Configure(MastodonConfiguration options)
	{
		// This will be called when the configuration is first accessed
		var config = MastodonConfiguration
			.CreateFromConfigurationAsync<MastodonConfiguration>(ConfigureTagzAppFactory.Current)
			.GetAwaiter()
			.GetResult();

		options.UpdateFrom(config);
	}
}

public class StartMastodon : IConfigureProvider
{
	private const string _DisplayName = "Mastodon";

	public async Task<IServiceCollection> RegisterServices(IServiceCollection services, CancellationToken cancellationToken = default)
	{
		// Register configuration setup
		services.AddSingleton<IConfigureOptions<MastodonConfiguration>, MastodonConfigurationSetup>();
		
		// Configure options with the setup class
		services.Configure<MastodonConfiguration>(options => { /* options configured by setup class */ });

		// Create separate HttpClientOptions for HTTP client configuration
		var currentConfig = await ConfigureTagzAppFactory.Current.GetConfigurationById<MastodonConfiguration>(MastodonConfiguration.AppSettingsSection);
		var httpClientOptions = new HttpClientOptions
		{
			BaseAddress = currentConfig.BaseAddress,
			Timeout = currentConfig.Timeout,
			DefaultHeaders = ConvertHeaders(currentConfig.DefaultHeaders),
			UseHttp2 = currentConfig.UseHttp2
		};

		// Register HTTP client with separate options
		services.AddHttpClient<ISocialMediaProvider, MastodonProvider, HttpClientOptions>(httpClientOptions);
		services.AddTransient<ISocialMediaProvider, MastodonProvider>();
		
		return services;
	}

	private static Dictionary<string, string>? ConvertHeaders(System.Net.Http.Headers.HttpRequestHeaders? headers)
	{
		if (headers == null) return null;

		var result = new Dictionary<string, string>();
		foreach (var header in headers)
		{
			result[header.Key] = string.Join(", ", header.Value);
		}
		return result.Count > 0 ? result : null;
	}
}
