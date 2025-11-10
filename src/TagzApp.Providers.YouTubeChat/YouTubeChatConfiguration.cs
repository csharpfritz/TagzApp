namespace TagzApp.Providers.YouTubeChat;

public class YouTubeChatConfiguration : BaseProviderConfiguration<YouTubeChatConfiguration>
{
	public const string AppSettingsSection = "providers:youtubechat";

	public const string Key_Google_ClientId = "Authentication:Google:ClientId";
	public const string Key_Google_ClientSecret = "Authentication:Google:ClientSecret";

	public const string Scope_YouTube = "https://www.googleapis.com/auth/youtube";

	protected override string ConfigurationKey => AppSettingsSection;

	public override string Name => "YouTubeChat";
	public override string Description => "Listen to messages in YouTube LiveChat for a Live Stream";
	public override bool Enabled { get; set; }

	public override string[] Keys => ["ChannelTitle", "ChannelId", "ChannelEmail", "BroadcastId", "BroadcastTitle", "LiveChatId", "RefreshToken", "YouTubeApiKey", "Enabled"];

	/// <summary>
	/// Title of the YouTube Channel we are monitoring
	/// </summary>
	public string ChannelTitle { get; set; } = string.Empty;

	public string ChannelId { get; set; } = string.Empty;

	/// <summary>
	/// Email used to authenticate with YouTube
	/// </summary>
	public string ChannelEmail { get; set; } = string.Empty;

	/// <summary>
	/// Id of the Broadcast we are monitoring
	/// </summary>
	public string BroadcastId { get; set; } = string.Empty;

	/// <summary>
	/// Title of the Broadcast we are monitoring
	/// </summary>
	public string BroadcastTitle { get; set; } = string.Empty;

	/// <summary>
	/// Id of the LiveChat we are monitoring
	/// </summary>
	public string LiveChatId { get; set; } = string.Empty;

	/// <summary>
	/// Token used to refresh the access token
	/// </summary>
	public string RefreshToken { get; set; } = string.Empty;

	/// <summary>
	/// API Key used to authenticate with YouTube
	/// </summary>
	public string YouTubeApiKey { get; set; } = string.Empty;

	public override string GetConfigurationByKey(string key)
	{
		return key switch
		{
			"ChannelTitle" => ChannelTitle,
			"ChannelEmail" => ChannelEmail,
			"ChannelId" => ChannelId,
			"BroadcastId" => BroadcastId,
			"BroadcastTitle" => BroadcastTitle,
			"LiveChatId" => LiveChatId,
			"RefreshToken" => RefreshToken,
			"YouTubeApiKey" => YouTubeApiKey,
			"Enabled" => Enabled.ToString(),
			_ => string.Empty
		};
	}

	public override void SetConfigurationByKey(string key, string value)
	{
		switch (key)
		{
			case "ChannelTitle":
				ChannelTitle = value;
				break;
			case "ChannelId":
				ChannelId = value;
				break;
			case "ChannelEmail":
				ChannelEmail = value;
				break;
			case "BroadcastId":
				BroadcastId = value;
				break;
			case "BroadcastTitle":
				BroadcastTitle = value;
				break;
			case "LiveChatId":
				LiveChatId = value;
				break;
			case "RefreshToken":
				RefreshToken = value;
				break;
			case "YouTubeApiKey":
				YouTubeApiKey = value;
				break;
			case "Enabled":
				Enabled = bool.Parse(value);
				break;
			default:
				throw new NotImplementedException($"Configuration key '{key}' is not supported.");
		}
	}

	protected override void UpdateFromConfiguration(YouTubeChatConfiguration other)
	{
		ChannelTitle = other.ChannelTitle;
		ChannelId = other.ChannelId;
		ChannelEmail = other.ChannelEmail;
		BroadcastId = other.BroadcastId;
		BroadcastTitle = other.BroadcastTitle;
		LiveChatId = other.LiveChatId;
		RefreshToken = other.RefreshToken;
		YouTubeApiKey = other.YouTubeApiKey;
		Enabled = other.Enabled;
	}

	public void UpdateFrom(YouTubeChatConfiguration other)
	{
		UpdateFromConfiguration(other);
	}
}
