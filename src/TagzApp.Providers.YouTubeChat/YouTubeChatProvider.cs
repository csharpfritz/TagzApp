using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TagzApp.Common.Configuration;

namespace TagzApp.Providers.YouTubeChat;

public class YouTubeChatProvider : ISocialMediaProvider, IDisposable
{
	private readonly IOptionsMonitor<YouTubeChatConfiguration> _ConfigMonitor;
	private readonly IDisposable? _ConfigChangeSubscription;
	private readonly ILogger<YouTubeChatProvider> _Logger;
	public const string ProviderName = "YouTubeChat";

	public const string ProviderId = "YOUTUBE-CHAT";

	public string Id => ProviderId;
	public string DisplayName => ProviderName;
	public string Description { get; init; } = "Listen to messages in YouTube LiveChat for a Live Stream";
	public TimeSpan NewContentRetrievalFrequency { get; set; } = TimeSpan.FromSeconds(15);

	public string NewestId { get; set; } = string.Empty;
	public string RefreshToken { get; set; } = string.Empty;
	public string YouTubeEmailId { get; set; } = string.Empty;
	public bool Enabled => _ConfigMonitor.CurrentValue.Enabled;

	private readonly HttpClient _HttpClient;
	private string _GoogleException = string.Empty;

	private CancellationTokenSource _TokenSource = new();
	private YouTubeService? _Service;
	private bool _DisposedValue;
	private string _NextPageToken = string.Empty;

	private SocialMediaStatus _Status = SocialMediaStatus.Unhealthy;
	private string _StatusMessage = "Not started";

	// Production constructor with reactive configuration
	public YouTubeChatProvider(IOptionsMonitor<YouTubeChatConfiguration> configMonitor, IConfiguration configuration, 
		HttpClient httpClient, ILogger<YouTubeChatProvider> logger)
	{
		_ConfigMonitor = configMonitor;
		_Logger = logger;
		_HttpClient = httpClient;

		// Subscribe to configuration changes
		_ConfigChangeSubscription = _ConfigMonitor.OnChange(async (config, name) =>
		{
			await HandleConfigurationChange(config);
		});
	}

	// Testing constructor with static configuration
	internal YouTubeChatProvider(IOptions<YouTubeChatConfiguration> settings, IConfiguration configuration,
		HttpClient httpClient, ILogger<YouTubeChatProvider> logger)
	{
		_ConfigMonitor = settings.ToStaticMonitor();
		_Logger = logger;
		_HttpClient = httpClient;
		_ConfigChangeSubscription = null; // No change subscription for static configurations
	}

	public async Task<IEnumerable<Content>> GetContentForHashtag(Hashtag tag, DateTimeOffset since)
	{
		var currentConfig = _ConfigMonitor.CurrentValue;

		if (string.IsNullOrEmpty(currentConfig.LiveChatId) || (!string.IsNullOrEmpty(_GoogleException) && _GoogleException.StartsWith(currentConfig.LiveChatId))) return Enumerable.Empty<Content>();
		var liveChatListRequest = new LiveChatMessagesResource.ListRequest(_Service!, currentConfig.LiveChatId, new(new[] { "id", "snippet", "authorDetails" }));
		liveChatListRequest.MaxResults = 2000;
		liveChatListRequest.ProfileImageSize = 36;

		if (!string.IsNullOrEmpty(_NextPageToken)) liveChatListRequest.PageToken = _NextPageToken;

		LiveChatMessageListResponse contents;
		try
		{
			contents = await liveChatListRequest.ExecuteAsync();
			_NextPageToken = contents.NextPageToken;
			NewContentRetrievalFrequency = contents.PollingIntervalMillis.HasValue ? TimeSpan.FromMilliseconds(contents.PollingIntervalMillis.Value * 10) : TimeSpan.FromSeconds(6);

		}
		catch (Exception ex)
		{
			Console.WriteLine($"Exception while fetching YouTubeChat: {ex.Message}");
			if (ex.Message.Contains("live chat is no longer live"))
			{
				_GoogleException = $"{currentConfig.LiveChatId}:{ex.Message}";
				// Note: Cannot modify configuration here - it's reactive and readonly
			}

			_Status = SocialMediaStatus.Unhealthy;
			_StatusMessage = $"Exception while fetching YouTubeChat: {ex.Message}";

			return Enumerable.Empty<Content>();
		}

		_Status = SocialMediaStatus.Healthy;
		_StatusMessage = $"OK -- adding ({contents.Items.Count}) messages for chatid '{currentConfig.LiveChatId}' at {DateTimeOffset.UtcNow}";

		try
		{
			var outItems = contents.Items.Select(i => new Content
			{
				Author = new Creator
				{
					DisplayName = i.AuthorDetails.DisplayName,
					ProfileImageUri = new Uri(i.AuthorDetails.ProfileImageUrl),
					ProfileUri = new Uri($"https://www.youtube.com/channel/{i.AuthorDetails.ChannelId}")
				},
				Provider = Id,
				ProviderId = i.Id,
				Text = string.IsNullOrEmpty(i.Snippet.DisplayMessage) ? "- REMOVED MESSAGE -" : i.Snippet.DisplayMessage,
				SourceUri = new Uri($"https://youtube.com/livechat/{currentConfig.LiveChatId}"),
				Timestamp = DateTimeOffset.Parse(i.Snippet.PublishedAtRaw),
				Type = ContentType.Message,
				HashtagSought = tag?.Text ?? ""
			}).ToArray();
			return outItems;

		}
		catch (Exception ex)
		{

			Console.WriteLine($"Exception while parsing YouTubeChat: {ex.Message}");

			_Status = SocialMediaStatus.Unhealthy;
			_StatusMessage = $"Exception while parsing YouTubeChat: {ex.Message}";

			return Enumerable.Empty<Content>();

		}


	}

	private async Task<YouTubeService> GetGoogleService()
	{

		if (_Service is not null) return _Service;

		var currentConfig = _ConfigMonitor.CurrentValue;
		_Service = new YouTubeService(new BaseClientService.Initializer
		{
			ApiKey = currentConfig.YouTubeApiKey,
			ApplicationName = "TagzApp"
		});

		_Status = SocialMediaStatus.Degraded;
		_StatusMessage = "Starting YouTubeChat client";

		return _Service;
	}


	public async Task StartAsync()
	{
		var currentConfig = _ConfigMonitor.CurrentValue;

		// if (string.IsNullOrEmpty(LiveChatId) || string.IsNullOrEmpty(RefreshToken)) return;

		_Service = await GetGoogleService();
		await YouTubeEmoteTranslator.LoadEmotes(_HttpClient, 10);

		if (!currentConfig.Enabled)
		{
			_Status = SocialMediaStatus.Disabled;
			_StatusMessage = "YouTubeChat client is disabled";
		}
		else
		{
			_Status = SocialMediaStatus.Healthy;
			_StatusMessage = "YouTubeChat provider is ready";
		}
		
		_Logger.LogInformation("YouTubeChat provider started. Status: {Status}, Enabled: {Enabled}", _Status, currentConfig.Enabled);

	}

	public async Task<string> GetChannelForUserAsync()
	{
		var currentConfig = _ConfigMonitor.CurrentValue;
		var service = await GetGoogleService();

		var channelRequest = service.Search.List("snippet");
		//channelRequest.Mine = true;
		channelRequest.ChannelId = currentConfig.ChannelId;
		var channels = channelRequest.Execute();

		// Not sure if this is needed, can't replicate "fisrt" error. (https://github.com/FritzAndFriends/TagzApp/issues/241)
		return channels.Items?.First().Snippet.Title ?? "Unknown Channel Title";

	}

	public IEnumerable<YouTubeBroadcast> GetBroadcastsForUser()
	{
		var currentConfig = _ConfigMonitor.CurrentValue;
		var service = GetGoogleService().GetAwaiter().GetResult();

		var listRequest = service.Search.List("snippet");
		//listRequest.Q = searchString;
		listRequest.ChannelId = currentConfig.ChannelId;
		listRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Upcoming;
		listRequest.Type = "video";
		listRequest.MaxResults = 500;
		listRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
		SearchListResponse broadcasts;
		try
		{
			broadcasts = listRequest.Execute();
		}
		catch (Google.GoogleApiException ex)
		{
			// GoogleApiException: The service youtube has thrown an exception. HttpStatusCode is Forbidden. The user is not enabled for live streaming.
			Console.WriteLine($"Exception while fetching YouTube broadcasts: {ex.Message}");

			_Status = SocialMediaStatus.Unhealthy;
			_StatusMessage = $"Exception while fetching YouTube broadcasts: {ex.Message}";

			return Enumerable.Empty<YouTubeBroadcast>();
		}

		var outBroadcasts = new List<YouTubeBroadcast>();

		broadcasts = ConvertToYouTubeBroadcasts(service, listRequest, broadcasts, outBroadcasts);

		listRequest = service.Search.List("snippet");
		//listRequest.Q = searchString;
		listRequest.ChannelId = currentConfig.ChannelId;
		listRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
		listRequest.Type = "video";
		listRequest.MaxResults = 5;
		listRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
		try
		{
			broadcasts = listRequest.Execute();
		}
		catch (Google.GoogleApiException ex)
		{
			// GoogleApiException: The service youtube has thrown an exception. HttpStatusCode is Forbidden. The user is not enabled for live streaming.
			Console.WriteLine($"Exception while fetching YouTube broadcasts: {ex.Message}");

			_Status = SocialMediaStatus.Unhealthy;
			_StatusMessage = $"Exception while fetching YouTube broadcasts: {ex.Message}";

			return Enumerable.Empty<YouTubeBroadcast>();
		}
		ConvertToYouTubeBroadcasts(service, listRequest, broadcasts, outBroadcasts);


		return outBroadcasts.OrderBy(b => b.BroadcastTime);

	}

	private static SearchListResponse ConvertToYouTubeBroadcasts(YouTubeService service, SearchResource.ListRequest listRequest, SearchListResponse broadcasts, List<YouTubeBroadcast> outBroadcasts)
	{
		var first = true;
		while (first || !string.IsNullOrEmpty(broadcasts.NextPageToken))// && outBroadcasts.Count < 20)
		{

			if (first)
			{
				first = false;
			}
			else
			{
				listRequest.PageToken = broadcasts.NextPageToken;
				broadcasts = listRequest.Execute();
			}

			foreach (var broadcast in broadcasts.Items)
			{

				var videoRequest = service.Videos.List("liveStreamingDetails");
				videoRequest.Id = broadcast.Id.VideoId;
				var videoResponse = videoRequest.Execute();

				if (videoResponse.Items.First().LiveStreamingDetails is null) continue;

				var liveChatId = videoResponse.Items.First().LiveStreamingDetails.ActiveLiveChatId;
				if (string.IsNullOrEmpty(liveChatId)) continue;

				outBroadcasts.Add(
					new YouTubeBroadcast(
						broadcast.Id.VideoId,
						broadcast.Snippet.Title,
						videoResponse.Items.First().LiveStreamingDetails.ScheduledStartTimeDateTimeOffset,
						liveChatId
					));

			}


		}

		return broadcasts;
	}

	#region Dispose Pattern

	protected virtual void Dispose(bool disposing)
	{
		if (!_DisposedValue)
		{
			if (disposing)
			{
				_Service?.Dispose();
				_TokenSource.Cancel();
				_ConfigChangeSubscription?.Dispose();
			}

			// TODO: free unmanaged resources (unmanaged objects) and override finalizer
			// TODO: set large fields to null
			_DisposedValue = true;
		}
	}

	// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
	// ~YouTubeChatProvider()
	// {
	//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public Task<(SocialMediaStatus Status, string Message)> GetHealth() => Task.FromResult((_Status, _StatusMessage));

	private async Task HandleConfigurationChange(YouTubeChatConfiguration newConfig)
	{
		_Logger.LogInformation("YouTubeChat provider configuration changed. Enabled: {Enabled}", newConfig.Enabled);
		
		// Handle configuration changes - for YouTubeChat, major changes require provider restart
		// to take effect since they affect the YouTube service initialization
		
		if (newConfig.Enabled)
		{
			await StartAsync();
		}
		else
		{
			await StopAsync();
		}
	}

	public Task StopAsync()
	{
		_Status = SocialMediaStatus.Disabled;
		_StatusMessage = "YouTubeChat provider is stopped";
			
		_Logger.LogInformation("YouTubeChat provider stopped");
		return Task.CompletedTask;
	}

	public async Task<IProviderConfiguration> GetConfiguration(IConfigureTagzApp configure)
	{
		return await YouTubeChatConfiguration.CreateFromConfigurationAsync<YouTubeChatConfiguration>(configure);
	}

	public async Task SaveConfiguration(IConfigureTagzApp configure, IProviderConfiguration providerConfiguration)
	{
		var config = (YouTubeChatConfiguration)providerConfiguration;
		await config.SaveToConfigurationAsync(configure);
		
		// The IOptionsMonitor will automatically pick up the changes from the saved configuration
		// No need to manually update since it's reactive
	}

	#endregion

}


