using Gravatar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using TagzApp.ViewModels.Data;

namespace TagzApp.Blazor.Hubs;

[Authorize(Policy = RolesAndPolicies.Policy.Moderator)]
public class ModerationHub : Hub<IModerationClient>
{

	private readonly IMessagingService _Service;
	private readonly IModerationRepository _Repository;
	private readonly UserManager<TagzAppUser> _UserManager;
	private bool ModerationEnabled = false;

	private static readonly ConcurrentDictionary<string, string> _CurrentUsersModerating = new();

	public ModerationHub(
		IMessagingService svc,
		ApplicationConfiguration configuration,
		IModerationRepository repository,
		UserManager<TagzAppUser> userManager)
	{
		_Service = svc;
		_Repository = repository;
		_UserManager = userManager;
		ModerationEnabled = configuration.ModerationEnabled;

	}

	public override async Task OnConnectedAsync()
	{

		var tags = Context.GetHttpContext()?.Request.Query["t"].ToString();
		if (!string.IsNullOrEmpty(tags))
		{
			var tagsRequested = tags.Split(',');
			foreach (var tag in tagsRequested)
			{
				await Groups.AddToGroupAsync(Context.ConnectionId, tag.TrimStart('#').ToLowerInvariant());
			}
		}

		var thisUser = await _UserManager.GetUserAsync(Context.User);
		if (thisUser is not null)
		{
			_CurrentUsersModerating.TryAdd(thisUser.Email, thisUser.DisplayName);
			await Clients.All.NewModerator(new NewModerator(thisUser.Email, thisUser.Email.ToGravatar(), thisUser.DisplayName));
		}

		await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception? exception)
	{

		var thisUser = await _UserManager.GetUserAsync(Context.User);
		if (thisUser is not null)
		{
			_CurrentUsersModerating.Remove(thisUser.Email, out _);
			await Clients.All.RemoveModerator(thisUser.Email);
		}

		await base.OnDisconnectedAsync(exception);
	}

	public string[] GetTags()
	{

		return _Service.TagsTracked.Select(t => $"#{t.TrimStart('#')}").ToArray();

	}

	public async Task SetStatus(string provider, string providerId, ModerationState newState)
	{

		if (!ModerationEnabled || Context.User is null)
		{
			return;
		}

		var thisUser = await _UserManager.GetUserAsync(Context.User);
		await _Repository.Moderate(thisUser?.NormalizedUserName ?? thisUser.Email, provider, providerId, newState);

	}

	public async Task<NewModerator[]> GetCurrentModerators()
	{

		return _CurrentUsersModerating.Select((k) => new NewModerator(k.Key, k.Key.ToGravatar(), k.Value)).ToArray();

	}

	public async Task<int> GetBlockedUserCount()
	{

		return await _Repository.GetCurrentBlockedUserCount();

	}

	public AvailableProvider[] GetAvailableProviders()
	{

		return _Service.Providers
			.Where(p => p.Enabled)
			.Select(p => new AvailableProvider(p.Id, p.DisplayName))
			.ToArray();

	}

	public async Task<IEnumerable<ModerationContentModel>> GetFilteredContentByTag(string tag, string[] providers, int state)
	{

		var states = state == -1 ?
			[ModerationState.Pending, ModerationState.Approved, ModerationState.Rejected] :
			new[] { (ModerationState)state };

		var hiddenUsers = (await _Repository.GetBlockedUsers())
			.Where(b => b.Capabilities == BlockedUserCapabilities.Hidden)
			.ToArray();

		var results = (await _Service.GetFilteredContentByTag(tag, providers, states, 200))
			.Where(c => !hiddenUsers.Any(h => h.Provider.Equals(c.Item1.Provider, StringComparison.InvariantCultureIgnoreCase) && h.UserName.Equals(c.Item1.Author.UserName, StringComparison.InvariantCultureIgnoreCase)))
			.Take(100)
			.Select(c => ModerationContentModel.ToModerationContentModel(c.Item1, c.Item2))
			.ToArray();
		Console.WriteLine($"Found {results.Length} results for {tag} with {providers.Length} providers and {states.Length} states");
		return results;

	}

	public async Task BlockUser(string authorUserName, string provider, int blockedUserCapabilities)
	{
		var thisUser = await _UserManager.GetUserAsync(Context.User);
		var capabilities = (BlockedUserCapabilities)blockedUserCapabilities;
		await _Repository.BlockUser(authorUserName, provider, thisUser?.NormalizedUserName ?? thisUser.Email, new DateTimeOffset(new DateTime(2050, 1, 1), TimeSpan.Zero), capabilities);
	}

	// Add Message to the Queue in ModerationRepository
	public async Task AddToQueue(string provider, string providerId, string speakerNotes)
	{
		await _Repository.AddToQueue(provider, providerId, speakerNotes);

		var thisQueueItem = await _Repository.GetItemFromQueue(provider, providerId);
		await Clients.All.NewQueueItem(new ViewModels.Data.QueueItem
		{
			Content = (ContentModel)thisQueueItem.Content,
			SpeakerNotes = thisQueueItem.SpeakerNotes,
			IsCompleted = thisQueueItem.IsCompleted,
			OrderBy = thisQueueItem.OrderBy
		});

	}

	// Mark a QueueItem as completed in ModerationRepository
	public async Task MarkQueueItemAsCompleted(string provider, string providerId)
	{
		await _Repository.MarkQueueItemAsCompleted(provider, providerId);
	}

	// Get all QueueItems from ModerationRepository and return them as a ViewModels.Data.QueueItem
	public async Task<ViewModels.Data.QueueItem[]> GetQueueItems()
	{
		var queueItems = await _Repository.GetQueueItems();
		return queueItems.Select(q =>
			new ViewModels.Data.QueueItem
			{
				Content = (ContentModel)q.Content,
				SpeakerNotes = q.SpeakerNotes,
				IsCompleted = q.IsCompleted,
				OrderBy = q.OrderBy
			}
		).ToArray();
	}


	public async Task<ViewModels.Data.QueueItem[]> GetIncompleteQueueItems()
	{
		var queueItems = await _Repository.GetIncompleteQueueItems();
		return queueItems.Select(q =>
			new ViewModels.Data.QueueItem
			{
				Content = (ContentModel)q.Content,
				SpeakerNotes = q.SpeakerNotes,
				IsCompleted = q.IsCompleted,
				OrderBy = q.OrderBy
			}
		).ToArray();
	}


}

public interface IModerationClient
{

	Task NewWaterfallMessage(ContentModel model);

	Task NewApprovedMessage(ModerationContentModel model);

	Task NewRejectedMessage(ModerationContentModel model);

	Task NewModerator(NewModerator newModerator);

	Task RemoveModerator(string email);

	Task NewBlockedUserCount(int count);

	Task NewQueueItem(ViewModels.Data.QueueItem queueItem);

	Task MarkQueueItemAsCompleted(string provider, string providerId);

}

