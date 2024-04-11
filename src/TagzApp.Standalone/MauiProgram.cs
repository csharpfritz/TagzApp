global using TagzApp.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TagzApp.Common.Models;

namespace TagzApp.Standalone;
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		builder.AddTagzApp();

		builder.Services.AddSingleton<NavigatorService>();
		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
