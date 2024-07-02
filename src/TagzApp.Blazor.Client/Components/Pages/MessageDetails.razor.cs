using Microsoft.AspNetCore.Components;
using TagzApp.ViewModels.Data;
using Microsoft.AspNetCore.SignalR.Client;
using Humanizer;

namespace TagzApp.Blazor.Client.Components.Pages;

public partial class MessageDetails
{


	[Parameter]
	public ModerationContentModel Model { get; set; }

	[CascadingParameter]
	public Components.Pages.Moderation ModerationPage { get; set; }

	private string ValidationMessage { get; set; }

	private string modalDisplay = "block";
	private string modalClass = "fade";
	private bool showBackdrop = true;
	private string _InnerModalCssClass = string.Empty;

	public bool AddedToQueue { get; set; } = false;

	public BlockedUserCapabilities BlockedUserCapabilities { get; set; } = BlockedUserCapabilities.Moderated;

	protected override async Task OnInitializedAsync()
	{

		await Task.Delay(300).ContinueWith(async _ =>

		{

			modalClass = "show";

			await InvokeAsync(StateHasChanged);
		});

		await base.OnInitializedAsync();
	}

	async Task BlockUser()
	{

		await ModerationPage.Connection.InvokeAsync("BlockUser", Model.AuthorUserName, Model.Provider);

		ValidationMessage = $"User {Model.AuthorUserName} has been blocked on {Model.Provider.ToLowerInvariant().Humanize(LetterCasing.Title)}";

	}

	async Task UpdateMessageOnQueue()
	{

		System.Console.WriteLine($"Adding to Queue: {AddedToQueue}");

		if (!AddedToQueue)
		{

			// Connect to the SignalR hub
			await ModerationPage.Connection.InvokeAsync("AddToQueue", Model.Provider, Model.ProviderId, "");
			ValidationMessage = $"Added message with ID {Model.ProviderId} to the queue for {Model.Provider.ToLowerInvariant().Humanize(LetterCasing.Title)}";

		}

	}

	public async Task Open()
	{

		modalDisplay = "block";
		modalClass = "show";
		showBackdrop = true;
		StateHasChanged();

	}


	public void Close()
	{
		modalDisplay = "none";
		modalClass = string.Empty;
		showBackdrop = false;
		_InnerModalCssClass = string.Empty;
		StateHasChanged();
	}

}
