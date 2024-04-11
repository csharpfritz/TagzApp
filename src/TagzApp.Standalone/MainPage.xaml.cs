using Microsoft.AspNetCore.Components;

namespace TagzApp.Standalone;

public partial class MainPage : ContentPage	
{
	public MainPage(IServiceProvider services)
	{

		InitializeComponent();
		Services = services;
	}

	public NavigationManager NavigationManager { get; }
	public IServiceProvider Services { get; }

	private void MenuItem_Clicked(object sender, EventArgs e)
	{

		var menuItem = (MenuItem)sender;
		var url = menuItem.Text switch {
			"Home"		=> "/",
			_					=> "/page2"
		};

		using var scope = Services.CreateScope();
		var navMgr = scope.ServiceProvider.GetRequiredService<NavigatorService>();
		navMgr.NavigateTo(url);

	}


}
