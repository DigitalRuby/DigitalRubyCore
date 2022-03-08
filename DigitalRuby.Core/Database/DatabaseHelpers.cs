namespace DigitalRuby.Core.Database;

/// <summary>
/// Db context helper methods
/// </summary>
public static class DatabaseHelpers
{
	/// <summary>
	/// Create a read only db context
	/// </summary>
	/// <typeparam name="T">Type of db context</typeparam>
	/// <param name="dbContextFactory">Db context factory</param>
	/// <returns>Read only db context</returns>
	public static T CreateReadOnlyDbContext<T>(this IDbContextFactory<T> dbContextFactory) where T : BaseDbContext
    {
		var ctx = dbContextFactory.CreateDbContext();
		ctx.MakeReadOnly();
		return ctx;
	}

	/// <summary>
	/// Create a read only db context (async)
	/// </summary>
	/// <typeparam name="T">Type of db context</typeparam>
	/// <param name="dbContextFactory">Db context factory</param>
	/// <returns>Read only db context</returns>
	public static async Task<T> CreateReadOnlyDbContextAsync<T>(this IDbContextFactory<T> dbContextFactory) where T : BaseDbContext
    {
		var ctx = await dbContextFactory.CreateDbContextAsync();
		ctx.MakeReadOnly();
		return ctx;
	}

	/// <summary>
	/// Create a read/write db context. If you are not saving changes, you can gain performance by using <see cref="CreateReadOnlyDbContext"/> instead.
	/// </summary>
	/// <typeparam name="T">Type of db context</typeparam>
	/// <param name="dbContextFactory">Db context factory</param>
	/// <returns>Read/write db context</returns>
	public static T CreateWritableDbContext<T>(this IDbContextFactory<T> dbContextFactory) where T : BaseDbContext
    {
		var ctx = dbContextFactory.CreateDbContext();
		ctx.MakeReadWrite();
		return ctx;
	}

	/// <summary>
	/// Create a read/write db context (async). If you are not saving changes, you can gain performance by using <see cref="CreateReadOnlyDbContext"/> instead.
	/// </summary>
	/// <typeparam name="T">Type of db context</typeparam>
	/// <param name="dbContextFactory">Db context factory</param>
	/// <returns>Read/write db context</returns>
	public static async Task<T> CreateWritableDbContextAsync<T>(this IDbContextFactory<T> dbContextFactory) where T : BaseDbContext
    {
		var ctx = await dbContextFactory.CreateDbContextAsync();
		ctx.MakeReadWrite();
		return ctx;
	}

	/// <summary>
	/// Create an in memory db context
	/// </summary>
	/// <typeparam name="T">Type of db context</typeparam>
	/// <returns>In memory db context</returns>
	/// <exception cref="ApplicationException"></exception>
	public static T CreateInMemoryDbContext<T>() where T : BaseDbContext
	{
		DbContextOptionsBuilder<T> optionsBuilder = new();
		optionsBuilder.UseSqlite("DataSource=:memory:");
		optionsBuilder.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));

		//optionsBuilder.UseInMemoryDatabase<T>("InMemory");
		var context = Activator.CreateInstance(typeof(T), new[] { optionsBuilder.Options }) as T ??
			throw new ApplicationException("Failed to create in memory db");
		context.Database.OpenConnection();
		context.Database.EnsureCreated();
		return context;
	}
}

/// <summary>
/// For places where something needs a db context factory and you don't have services available, always returns in memory db context
/// </summary>
/// <typeparam name="TContext">Type of db context</typeparam>
public class InMemoryDbContextFactory<TContext> : IDbContextFactory<TContext> where TContext : BaseDbContext
{
	private readonly TContext inMemContext;

	/// <summary>
	/// Constructor
	/// </summary>
	public InMemoryDbContextFactory()
	{
		inMemContext = DatabaseHelpers.CreateInMemoryDbContext<TContext>();
	}

	/// <summary>
	/// Create (or return) an in memory db context
	/// </summary>
	/// <returns>Newly created in memory context</returns>
	public TContext CreateDbContext()
	{
		return inMemContext;
	}
}
