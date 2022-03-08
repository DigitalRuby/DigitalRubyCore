using DigitalRuby.Core.Database;

using Microsoft.EntityFrameworkCore;

namespace DigitalRuby.Core.Tests.Database;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

[TestFixture]
public class DatabaseHelpersTests
{
	[Table("TestTable")]
	private class TestTable
	{
		[Key]
		public long Id { get; set; }

		public string Data { get; set; }
	}

	private class InMemoryContext : BaseDbContext
	{
		public DbSet<TestTable> TestTable { get; set; }

		public InMemoryContext(DbContextOptions options) : base(options)
		{

		}
	}

	/// <summary>
	/// Test creating in memory context works as expected
	/// </summary>
	/// <returns>Task</returns>
	[Test]
	public async Task TestInMemoryContext()
	{
		using var db = DatabaseHelpers.CreateInMemoryDbContext<InMemoryContext>();
		Assert.IsNotNull(db.Database.ProviderName);
		Assert.IsTrue(db.Database.ProviderName!.Contains("sqlite", System.StringComparison.OrdinalIgnoreCase));
		Assert.IsNotNull(db.Database.GetConnectionString());
		Assert.IsTrue(db.Database.GetConnectionString()!.Contains(":memory:", System.StringComparison.OrdinalIgnoreCase));

		TestTable poo = new() { Data = "poo" };
		db.TestTable.Add(poo);
		db.SaveChanges();
		var poo2 = await db.TestTable.FirstOrDefaultAsync();
		Assert.IsNotNull(poo2);
		Assert.AreEqual(1, poo2!.Id);
		Assert.AreEqual("poo", poo2.Data);
	}

	/// <summary>
	/// Test that read-write and read-only context behave as expected
	/// </summary>
	/// <returns>Task</returns>
	[Test]
	public async Task TestReadOnlyDbContext()
	{
		var dbFactory = new Core.Database.InMemoryDbContextFactory<InMemoryContext>();
		using (var db = dbFactory.CreateWritableDbContext())
		{
			db.Database.OpenConnection();
			db.Database.EnsureCreated();
			TestTable poo = new() { Data = "poo" };
			db.TestTable.Add(poo);
			using var tran = db.BeginTransaction();
			await db.SaveChangesAsync(true, default);
			db.CommitTransaction();
		}
		using (var db2 = await dbFactory.CreateWritableDbContextAsync())
		{
			db2.Database.OpenConnection();
			db2.Database.EnsureCreated();
			TestTable poo = new() { Data = "poo" };
			db2.TestTable.Add(poo);
			using var tran = await db2.BeginTransactionAsync();
			await db2.SaveChangesAsync(true, default);
			await db2.CommitTransactionAsync();
		}
		using (var db3 = dbFactory.CreateReadOnlyDbContext())
		{
			db3.Database.OpenConnection();
			db3.Database.EnsureCreated();
			TestTable poo = new() { Data = "poo" };
			db3.TestTable.Add(poo);
			Assert.Throws<InvalidOperationException>(() => db3.BeginTransaction());
			Assert.ThrowsAsync<InvalidOperationException>(() => db3.SaveChangesAsync(true, default));
		}
		using (var db4 = await dbFactory.CreateReadOnlyDbContextAsync())
		{
			db4.Database.OpenConnection();
			db4.Database.EnsureCreated();
			TestTable poo = new() { Data = "poo" };
			db4.TestTable.Add(poo);
			Assert.ThrowsAsync<InvalidOperationException>(() => db4.BeginTransactionAsync());
			Assert.ThrowsAsync<InvalidOperationException>(() => db4.SaveChangesAsync(true, default));
		}
	}
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
