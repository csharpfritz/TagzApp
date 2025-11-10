using System.Text.Json;
using System.Text.Json.Serialization;

namespace TagzApp.Providers.Twitter.Configuration;

/// <summary>
/// Defines the Twitter configuration
/// </summary>
public class TwitterConfiguration : BaseProviderConfiguration<TwitterConfiguration>
{
	/// <summary>
	/// The configuration key used to store this configuration in the TagzApp configuration system
	/// </summary>
	protected override string ConfigurationKey => "TWITTER";

	// HTTP Client properties from HttpClientOptions
	public Uri? BaseAddress { get; set; } = new Uri("https://api.twitter.com");
	public TimeSpan Timeout { get; set; } = TimeSpan.Zero;
	public Dictionary<string, string>? DefaultHeaders { get; set; }
	public bool UseHttp2 { get; set; } = true;

	public string? BearerToken { get; set; }

	[JsonIgnore]
	public override string Name => "X (formerly Twitter)";

	[JsonIgnore]
	public override string Description => "Search the X (formerly Twitter) service for a specified hashtag";

	public override bool Enabled { get; set; }

	[JsonIgnore]
	public override string[] Keys => ["BaseAddress", "Timeout", "DefaultHeaders", "UseHttp2", "BearerToken", "Enabled"];

	/// <summary>
	/// Declare the section name used
	/// </summary>
	public const string AppSettingsSection = "providers:twitter";

	public TwitterConfiguration()
	{
		BaseAddress = new Uri("https://api.twitter.com");
	}

	public override string GetConfigurationByKey(string key)
	{
		return key switch
		{
			"BaseAddress" => BaseAddress?.ToString() ?? string.Empty,
			"Timeout" => Timeout.ToString(),
			"DefaultHeaders" => SerializeHeaders(DefaultHeaders) ?? string.Empty,
			"BearerToken" => BearerToken ?? string.Empty,
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
				BaseAddress = new Uri(value);
				break;
			case "BearerToken":
				BearerToken = value;
				break;
			case "Enabled":
				Enabled = bool.Parse(value);
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
			default:
				throw new NotImplementedException($"Unable to set value for key '{key}'");
		}
	}

	/// <summary>
	/// Updates this instance with values from another configuration instance
	/// </summary>
	/// <param name="source">The source configuration to copy from</param>
	protected override void UpdateFromConfiguration(TwitterConfiguration source)
	{
		BaseAddress = source.BaseAddress;
		Timeout = source.Timeout;
		DefaultHeaders = source.DefaultHeaders;
		UseHttp2 = source.UseHttp2;
		BearerToken = source.BearerToken;
		Enabled = source.Enabled;
	}

	/// <summary>
	/// Public method to update configuration from another instance
	/// </summary>
	/// <param name="source">The source configuration to copy from</param>
	public void UpdateFrom(TwitterConfiguration source)
	{
		UpdateFromConfiguration(source);
	}

	// Helper methods for header serialization
	private static Dictionary<string, string> DeserializeHeaders(string value)
	{
		if (string.IsNullOrEmpty(value)) return new();
		return JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new();
	}

	private static string SerializeHeaders(Dictionary<string, string>? headers)
	{
		if (headers is null) return string.Empty;
		return JsonSerializer.Serialize(headers);
	}
}
