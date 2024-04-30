using Microsoft.EntityFrameworkCore;

namespace TagzApp.Storage.Sqlite;

internal class TagzAppContext : DbContext
{
	public TagzAppContext(DbContextOptions options) : base(options)
	{
	}
	public DbSet<SqliteContent> Content { get; set; }

	public DbSet<SqliteModerationAction> ModerationActions { get; set; }

	public DbSet<SqliteBlockedUser> BlockedUsers { get; set; }

	//public DbSet<Settings> Settings => Set<Settings>();

	public DbSet<Tag> TagsWatched { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{

		modelBuilder.Entity<SqliteContent>().HasAlternateKey(c => new { c.Provider, c.ProviderId });
		modelBuilder.Entity<SqliteContent>().HasOne(c => c.ModerationAction)
			.WithOne(m => m.Content).HasForeignKey<SqliteModerationAction>(m => m.ContentId);

		modelBuilder.Entity<SqliteModerationAction>().HasAlternateKey(c => new { c.Provider, c.ProviderId });

		modelBuilder.Entity<Tag>().Property(t => t.Text)
			.HasMaxLength(50)
			.IsRequired();

		base.OnModelCreating(modelBuilder);

	}

}
