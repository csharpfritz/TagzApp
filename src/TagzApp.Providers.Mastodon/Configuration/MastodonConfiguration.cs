using System.Text.Json.Serialization;
using TagzApp.Communication.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TagzApp.Providers.Mastodon.Configuration;

/// <summary>
/// Defines the Mastodon configuration
/// </summary>
public class MastodonConfiguration : BaseProviderConfiguration<MastodonConfiguration>
{
	/// <summary>
	/// Declare the section name used
	/// </summary>
	public const string AppSettingsSection = "provider-mastodon";

	protected override string ConfigurationKey => AppSettingsSection;

	[JsonIgnore]
	public override string Description => "Search the Mastodon federated services for a specified hashtag";

	[JsonIgnore]
	public override string Name => "Mastodon";

	public override bool Enabled { get; set; }

	[JsonIgnore]
	public override string[] Keys => ["BaseAddress", "Timeout", "DefaultHeaders", "UseHttp2", "Enabled"];

	// HTTP Client properties
	public Uri? BaseAddress { get; set; }
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
	public HttpRequestHeaders? DefaultHeaders { get; set; }
	public bool UseHttp2 { get; set; } = true;

	public MastodonConfiguration()
	{
		BaseAddress = new Uri("https://mastodon.social");
	}

	public override string GetConfigurationByKey(string key)
	{
		return key switch
		{
			"BaseAddress" => BaseAddress?.ToString() ?? string.Empty,
			"Timeout" => Timeout.ToString(),
			"DefaultHeaders" => SerializeHeaders(DefaultHeaders),
			"UseHttp2" => UseHttp2.ToString(),
			"Enabled" => Enabled.ToString(),
			_ => string.Empty
		};
	}

	public override void SetConfigurationByKey(string key, string value)
	{
		switch (key)
		{
			case "BaseAddress":
				BaseAddress = string.IsNullOrEmpty(value) ? null : new Uri(value);
				break;
			case "Timeout":
				Timeout = TimeSpan.Parse(value);
				break;
			case "DefaultHeaders":
				DefaultHeaders = DeserializeHeaders(value);
				break;
			case "UseHttp2":
				UseHttp2 = bool.Parse(value);
				break;
			case "Enabled":
				Enabled = bool.Parse(value);
				break;
			default:
				throw new NotImplementedException($"Configuration key '{key}' is not supported.");
		}
	}

	protected override void UpdateFromConfiguration(MastodonConfiguration other)
	{
		BaseAddress = other.BaseAddress;
		Timeout = other.Timeout;
		DefaultHeaders = other.DefaultHeaders;
		UseHttp2 = other.UseHttp2;
		Enabled = other.Enabled;
	}

	public void UpdateFrom(MastodonConfiguration other)
	{
		UpdateFromConfiguration(other);
	}

	private static string SerializeHeaders(HttpRequestHeaders? headers)
	{
		if (headers == null) return string.Empty;

		var headerDict = headers.ToDictionary(h => h.Key, h => h.Value.ToArray());
		return JsonSerializer.Serialize(headerDict);
	}

	private static HttpRequestHeaders? DeserializeHeaders(string value)
	{
		if (string.IsNullOrEmpty(value)) return null;

		try
		{
			var headerDict = JsonSerializer.Deserialize<Dictionary<string, string[]>>(value);
			if (headerDict == null) return null;

			var client = new HttpClient();
			foreach (var kvp in headerDict)
			{
				client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
			}
			return client.DefaultRequestHeaders;
		}
		catch
		{
			return null;
		}
	}
}
