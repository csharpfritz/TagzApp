using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using TagzApp.ViewModels.Data;

namespace TagzApp.Blazor.Client.Components.Pages;
public partial class Waterfall
{

	private SortedList<string, TagzApp.ViewModels.Data.ContentModel> _Content = new();

	WaterfallModal Modal = new();
	ContentModel ModalContent { get; set; } = null!;

	PauseButton thePauseButton = new();

	private HashSet<dynamic> _PauseQueue = new();

	public HubConnection Connection { get; set; } = null!;

	private bool _IsDisposing = false;

	[Parameter, EditorRequired]
	public string TagTracked { get; set; }

	protected override async Task OnInitializedAsync()
	{

		await StartSignalRHub();

		var existingContent = await Connection.InvokeAsync<IEnumerable<ContentModel>>("GetExistingContentForTag", TagTracked);

		// Console.WriteLine($"Received {existingContent.Count()} messages for tag {TagTracked}");

		foreach (var content in existingContent)
		{
			if (_Content.ContainsKey(content.Timestamp.ToString("yyyyMMddHHmmss") + content.ProviderId)) continue;
			_Content.Add(content.Timestamp.ToString("yyyyMMddHHmmss") + content.ProviderId, content);
		}

		await base.OnInitializedAsync();

	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{

		if (firstRender)
		{
			await JSRuntime.InvokeVoidAsync("WaterfallUi.setupWaterfall");
		}

		await JSRuntime.InvokeVoidAsync("window.Masonry.setupPage");

		await base.OnAfterRenderAsync(firstRender);

	}



	private async Task StartSignalRHub()
	{
		Connection = new HubConnectionBuilder()
			.WithUrl(NavigationManager.ToAbsoluteUri($"/messages?t={TagTracked}"))
			.WithAutomaticReconnect()
			.Build();

		Connection.On<ContentModel>("NewWaterfallMessage", (content) =>
		{

			if (thePauseButton.IsPaused)
			{
				_PauseQueue.Add(content);
				thePauseButton.Counter = _PauseQueue.Count;
				return;
			}

			_Content.Add(content.Timestamp.ToString("yyyyMMddHHmmss") + content.ProviderId, content);
			StateHasChanged();
		});

		Connection.On<string, string>("RemoveMessage", (provider, providerId) =>
		{

			// NOTE: Remove messages immediately from the waterfall regardless of pause state

			var thisMessage = _Content.FirstOrDefault(c => c.Value.Provider == provider && c.Value.ProviderId == providerId);
			if (thisMessage.Value is not null)
			{
				// TODO: Change this to not use the DateTimeOffset as a key
				_Content.Remove(thisMessage.Key);
				StateHasChanged();
			}

		});

		Connection.Closed += async _ =>
		{

			if (_IsDisposing) return;

			while (!_IsDisposing)
			{

				await Task.Delay(2000);
				try
				{

					await StartSignalRHub();

				}
				catch (Exception ex)
				{

					Console.WriteLine($"Error while attempting to reconnect ({Connection.State.ToString()}) to server: {ex.Message}");

				}

			}

			return;

		};

		Connection.Closed += async _ =>
		{

			if (_IsDisposing) return;

			while (!_IsDisposing)
			{

				await Task.Delay(2000);
				try
				{

					await StartSignalRHub();

				}
				catch (Exception ex)
				{

					Console.WriteLine($"Error while attempting to reconnect ({Connection.State.ToString()}) to server: {ex.Message}");

				}

			}

			return;

		};

		await Connection.StartAsync();
	}

	Task SendMessageTask;
	async Task ShowModal(ContentModel content)
	{

		thePauseButton.SetPauseState(true);
		ModalContent = content;
		SendMessageTask = Task.Run(async () =>
		{

			Console.WriteLine("Sending message to overlay");

			for (var i = 0; i < 10; i++)
			{

				if (i > 0) Console.WriteLine($"Retry #{i} to activate overlay");
				try
				{
					await Connection.InvokeAsync("SendMessageToOverlay", TagTracked, content.Provider, content.ProviderId);
					break;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Exception while sending message to overlay: {ex.Message}");
					await Task.Delay(1000 + i * 1000);
				}

			}

		});
		await Task.Delay(200);  // LAME delay to allow the content to propogate into the Modal component
		await Modal.Open();

	}

	public async Task OnPauseClick(bool newPauseState)
	{
		if (!newPauseState)
		{
			// Add all the items in the pause queue to the waterfall
			foreach (var item in _PauseQueue)
			{
				if (item is ContentModel content)
				{
					_Content.Add(content.Timestamp.ToString("yyyyMMddHHmmss") + content.ProviderId, content);
				}
			}
			_PauseQueue.Clear();
			thePauseButton.Counter = 0;
		}

		await InvokeAsync(StateHasChanged);
	}

	public ValueTask DisposeAsync()
	{
		_IsDisposing = true;
		if (Connection is null) return ValueTask.CompletedTask;
		return Connection.DisposeAsync();
		// return ValueTask.CompletedTask;
	}

}
