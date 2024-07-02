using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using TagzApp.ViewModels.Data;

namespace TagzApp.Blazor.Client.Components.Pages;
public partial class Moderation
{

	[CascadingParameter(Name = "HideFooter")] public bool HideFooter { get; set; }

	public int BlockedUserCount { get; set; } = 0;

	public bool ShowModal { get; set; } = false;

	public MessageDetails MessageDetailsDialog { get; set; }

	private ModerationContentModel SelectedContent { get; set; }

	public PauseButton ThePauseButton { get; set; } = new();
	private HashSet<dynamic> _PauseQueue = new();

	private SortedList<DateTimeOffset, TagzApp.ViewModels.Data.ModerationContentModel> _Content = new();

	private HashSet<NewModerator> _Moderators = new();

	private IEnumerable<AvailableProvider> _Providers = Enumerable.Empty<AvailableProvider>();

	private List<string> _FilteredProviders = [];
	private bool _Loading = true;

	private FilterModerationState _FilterApprovalStatus = FilterModerationState.All;

	private FilterModerationState FilterApprovalStatus
	{
		get { return _FilterApprovalStatus; }
		set
		{
			_FilterApprovalStatus = value;
			_Loading = true;
			_Content.Clear();
			StateHasChanged();
			FilterContent = InitializeContent().ContinueWith(async t =>
			{
				_Loading = false;
				await InvokeAsync(StateHasChanged);
				FilterContent = null;
			});
		}
	}

	public HubConnection Connection { get; set; }

	private bool _IsDisposing = false;

	string _Tag = string.Empty;

	protected override async Task OnInitializedAsync()
	{

		HideFooter = true;

		await ConfigureSignalRConnection();

		_Tag = (await Connection.InvokeAsync<string[]>("GetTags"))?.First();


		BlockedUserCount = await Connection.InvokeAsync<int>("GetBlockedUserCount");

		_Providers = await Connection.InvokeAsync<IEnumerable<AvailableProvider>>("GetAvailableProviders");
		_FilteredProviders = _Providers.Select(p => p.Id).ToList();

		_Moderators = (await Connection.InvokeAsync<NewModerator[]>("GetCurrentModerators")).ToHashSet();

		await base.OnInitializedAsync();

		await InitializeContent();

	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await JSRuntime.InvokeVoidAsync("window.WaterfallUi.ConfigureKeyboardSupport");
		await base.OnAfterRenderAsync(firstRender);
	}

	async Task ConfigureSignalRConnection()
	{

		Connection = new HubConnectionBuilder()
					.WithUrl(NavigationManager.ToAbsoluteUri("/mod"))
					.WithAutomaticReconnect()
					.Build();

		await ListenForModerationContent();

		Connection.Closed += async _ =>
		{

			if (_IsDisposing) return;

			while (!_IsDisposing)
			{

				await Task.Delay(2000);
				try
				{

					await ConfigureSignalRConnection();

				}
				catch (Exception ex)
				{

					Console.WriteLine($"Error while attempting to reconnect ({Connection.State.ToString()}) to server: {ex.Message}");

				}

			}

		};

		await Connection.StartAsync();

	}

	async Task ListenForModerationContent()
	{

		Connection.On<ContentModel>("NewWaterfallMessage", async (content) =>
		{

			if (!_FilteredProviders.Any(c => c.Equals(content.Provider, StringComparison.InvariantCultureIgnoreCase))) return;
			if (_FilterApprovalStatus != FilterModerationState.All && _FilterApprovalStatus != FilterModerationState.Pending) return;

			if (ThePauseButton.IsPaused)
			{
				if (!_PauseQueue.Any(p => p.ProviderId == content.ProviderId) && !_Content.Any(c => c.Value.ProviderId == content.ProviderId))
				{
					_PauseQueue.Add(content);
					ThePauseButton.Counter = _PauseQueue.Count();
				}

				return;
			}

			_Content.Add(content.Timestamp, ModerationContentModel.ToModerationContentModel(content));
			await InvokeAsync(StateHasChanged);
		});

		Connection.On<ModerationContentModel>("NewApprovedMessage", HandleApprovedMessage);

		Connection.On<ModerationContentModel>("NewRejectedMessage", async (content) =>
		{

			if (ThePauseButton.IsPaused)
			{
				if (!_PauseQueue.Any(p => p.ProviderId == content.ProviderId) && !_Content.Any(c => c.Value.ProviderId == content.ProviderId))
				{
					_PauseQueue.Add(content);
				}
				else if (_PauseQueue.Any(p => p.ProviderId == content.ProviderId))
				{
					_PauseQueue.RemoveWhere(p => p.ProviderId == content.ProviderId);
					_PauseQueue.Add(content);
				}
				ThePauseButton.Counter = _PauseQueue.Count();

				return;
			}

			var existing = _Content.FirstOrDefault(p => p.Value.ProviderId == content.ProviderId);
			if (existing.Value is not null && existing.Value.State == ModerationState.Rejected) return;

			if (existing.Value is not null)
			{
				existing.Value.State = ModerationState.Rejected;
				existing.Value.ModerationTimestamp = content.ModerationTimestamp;
			}
			else
			{
				_Content.Add(content.Timestamp, content);
			}

		});

		Connection.On<int>("NewBlockedUserCount", async (newCount) =>
		{
			BlockedUserCount = newCount;
			await InvokeAsync(StateHasChanged);
		});

		Connection.On<NewModerator>("NewModerator", async (moderator) =>
		{
			_Moderators.Add(moderator);
			await InvokeAsync(StateHasChanged);
		});

		Connection.On<string>("RemoveModerator", async (moderatorEmail) =>
		{
			var thisMod = _Moderators.FirstOrDefault(m => m.Email.Equals(moderatorEmail, StringComparison.InvariantCultureIgnoreCase));
			if (thisMod is not null)
			{
				_Moderators.Remove(thisMod);
				await InvokeAsync(StateHasChanged);
			}
		});

	}

	async Task HandleApprovedMessage(ModerationContentModel content)
	{

		if (ThePauseButton.IsPaused)
		{
			if (!_PauseQueue.Any(p => p.ProviderId == content.ProviderId) && !_Content.Any(c => c.Value.ProviderId == content.ProviderId))
			{
				_PauseQueue.Add(content);
				ThePauseButton.Counter = _PauseQueue.Count();
			}
			else if (_PauseQueue.Any(p => p.ProviderId == content.ProviderId))
			{
				_PauseQueue.RemoveWhere(p => p.ProviderId == content.ProviderId);
				_PauseQueue.Add(content);
				ThePauseButton.Counter = _PauseQueue.Count();
			}
			return;
		}

		var existing = _Content.FirstOrDefault(p => p.Value.ProviderId == content.ProviderId);
		if (existing.Value is not null && existing.Value.State == ModerationState.Approved) return;

		if (existing.Value is not null)
		{
			existing.Value.State = ModerationState.Approved;
			existing.Value.ModerationTimestamp = content.ModerationTimestamp;
		}
		else
		{
			_Content.Add(content.Timestamp, content);
		}
		await InvokeAsync(StateHasChanged);

	}

	async Task InitializeContent()
	{

		var approvalStatus = (int)_FilterApprovalStatus;
		var currentContent = (await Connection.InvokeAsync<IEnumerable<ModerationContentModel>>("GetFilteredContentByTag", _Tag, _FilteredProviders.ToArray(), approvalStatus))
			.ToArray();

		foreach (var content in currentContent.OrderByDescending(c => c.Timestamp).ToArray())
		{
			_Content.Add(content.Timestamp, content);
		}

		_Loading = false;

	}

	public async Task Moderate(ModerationAction action)
	{

		await Connection.InvokeAsync("SetStatus", action.Provider, action.ProviderId, action.State);
		// TODO: Tag message appropriately to indicate moderation state

	}

	public async Task OnPauseClick(bool newPauseState)
	{

		// Console.WriteLine($"New Pause State: {newPauseState}");

		if (!newPauseState)
		{
			// Add all the items in the pause queue to the waterfall
			foreach (var item in _PauseQueue)
			{
				if (item is ContentModel content)
				{
					_Content.Add(content.Timestamp, ModerationContentModel.ToModerationContentModel(content));
				}
			}
			_PauseQueue.Clear();
			ThePauseButton.Counter = 0;
		}

		await InvokeAsync(StateHasChanged);
	}

	public async Task ShowMessageDetails(ModerationContentModel content)
	{
		SelectedContent = content;
		ShowModal = true;
		await MessageDetailsDialog.Open();
		StateHasChanged();
	}

	async Task ToggleProviderFilter(string providerId)
	{

		if (_FilteredProviders.Contains(providerId))
		{
			_FilteredProviders.Remove(providerId);
		}
		else
		{
			_FilteredProviders.Add(providerId);
		}

		_Content.Clear();
		_Loading = true;
		StateHasChanged();
		FilterContent = InitializeContent().ContinueWith(async t =>
		{
			_Loading = false;
			await InvokeAsync(StateHasChanged);
			FilterContent = null;
		});

	}

	private Task FilterContent;

	private enum FilterModerationState
	{
		All = -1,
		Pending = 0,
		Approved = 1,
		Rejected = 2,
	};

	public async ValueTask DisposeAsync()
	{

		_IsDisposing = true;
		if (Connection is not null)
		{
			await Connection.DisposeAsync();
		}

	}

}
