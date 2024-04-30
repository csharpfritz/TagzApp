using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Npgsql;
using TagzApp.Common;
using TagzApp.Communication;
using TagzApp.Storage.Sqlite;

namespace Microsoft.Extensions.DependencyInjection;

public static class AppExtensions
{

	private static Task _MigrateTask = Task.CompletedTask;

	public static IServiceCollection AddSqliteServices(this IServiceCollection services, IConfigureTagzApp configureTagzApp, ConnectionSettings connectionSettings)
	{

		services.AddDbContext<TagzAppContext>(options =>
				{
					options.UseSqlite(connectionSettings.ContentConnectionString);
				});

		//services.AddScoped<IProviderConfigurationRepository, PostgresProviderConfigurationRepository>();
		services.AddSingleton<IMessagingService>(sp =>
		{
			var scope = sp.CreateScope();
			var notify = scope.ServiceProvider.GetRequiredService<INotifyNewMessages>();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<BaseProviderManager>>();
			//var safetyLogger = scope.ServiceProvider.GetRequiredService<ILogger<AzureSafetyModeration>>();
			var socialMediaProviders = scope.ServiceProvider.GetRequiredService<IEnumerable<ISocialMediaProvider>>();
			var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
			return new SqliteMessagingService(sp, notify, cache, logger, socialMediaProviders);
		});
		services.AddHostedService(s => s.GetRequiredService<IMessagingService>());

		services.AddScoped<IModerationRepository, SqliteModerationRepository>();
		using var builtServices = services.BuildServiceProvider();
		var ctx = builtServices.GetRequiredService<TagzAppContext>();
		_MigrateTask = ctx.Database.MigrateAsync();

		return services;

	}

}
