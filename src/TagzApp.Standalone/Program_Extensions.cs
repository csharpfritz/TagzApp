using TagzApp.Common.Models;

namespace TagzApp.Standalone;

public static class Program_Extensions
{

	public static MauiAppBuilder? AddTagzApp(this MauiAppBuilder? builder)
	{

		// TODO: analyze how this interacts with the app configuration from ConfigureTagzAppFactory
		builder.Services.AddScoped<ApplicationConfiguration>();

		var configure = ConfigureTagzAppFactory.Create(builder.Configuration, builder.Services.BuildServiceProvider());

		return builder;

	}

}
