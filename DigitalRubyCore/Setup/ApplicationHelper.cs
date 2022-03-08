using FeatureFlags.Core.Database;
using FeatureFlags.Core.Mvc;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Npgsql;

using StackExchange.Redis;

namespace FeatureFlags.Core.Setup;

public static class ApplicationHelper
{
	private static string? GetNamespaceFilter()
	{
		// only auto bind from assemblies related to our root namespace
		string? namespaceFilter = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
		int dot = namespaceFilter.IndexOf('.');
		if (dot >= 0)
		{
			namespaceFilter = namespaceFilter[..dot];
		}

		// if testhost, grab all types
		if (namespaceFilter is not null && namespaceFilter.StartsWith("testhost", StringComparison.OrdinalIgnoreCase))
		{
			namespaceFilter = null;
		}

		return namespaceFilter;
	}

	/// <summary>
	/// Create a web application, adding common services like swagger, jwt, etc.
	/// </summary>
	/// <param name="args">Command line args</param>
	/// <param name="postSetup">Action to add more services</param>
	/// <returns>Web application</returns>
	public static WebApplication CreateWebApplication(string[] args, Action<IServiceCollection, IConfiguration>? postSetup = null)
	{
		var builder = WebApplication.CreateBuilder(args);
		var config = builder.Configuration;
		var nsFilter = GetNamespaceFilter();
		builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

		// auto register classes tagged with Binding or Configuration attribute
		builder.Services.BindServicesFromBindingAttribute(nsFilter);
		builder.Services.BindConfigurationFromConfigurationAttribute(config, nsFilter);

		// logging
		builder.Services.AddSingleton<JsonConsoleFormatter>();
		builder.Services.AddSingleton<JsonConsoleFormatterOptions>(new JsonConsoleFormatterOptions { UseUtcTimestamp = true });

		// general hashing
		builder.Services.AddSingleton<Argon2Hasher_V2_Options>(new Argon2Hasher_V2_Options { Secret = Encoding.UTF8.GetBytes(builder.Configuration["PasswordSecret"]) });

		// forward headers like x-forwarded-for so they are used as the remote address
		builder.Services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All);

		// use newtonsoft json for controllers
		builder.Services.AddControllers().AddNewtonsoftJson(opt =>
		{
			opt.SerializerSettings.Converters.Add(new StringEnumConverter
			{
				NamingStrategy = new CamelCaseNamingStrategy()
			});
			opt.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
			opt.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
			opt.SerializerSettings.DateParseHandling = DateParseHandling.None;
			opt.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
			opt.SerializerSettings.FloatParseHandling = FloatParseHandling.Decimal;
			opt.SerializerSettings.FloatFormatHandling = FloatFormatHandling.DefaultValue;
			opt.SerializerSettings.Culture = System.Globalization.CultureInfo.InvariantCulture;
			opt.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;
			opt.SerializerSettings.MaxDepth = 10;
			opt.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
			if (builder.Environment.IsProduction())
			{
				opt.SerializerSettings.Formatting = Formatting.None;
			}
			else
			{
				opt.SerializerSettings.Formatting = Formatting.Indented;
			}
		});

		// jwt tokens
		builder.Services.AddJwtAuthentication(config);

		// swagger (api docs)
		builder.Services.AddSwagger(config);

		// IMemoryCache
		builder.Services.AddMemoryCache();

		// redis
		var connMultiplexer = ConnectionMultiplexer.Connect(config["RedisConnectionString"], options => options.AbortOnConnectFail = false);
		builder.Services.AddSingleton<IConnectionMultiplexer>(connMultiplexer);
		builder.Services.AddStackExchangeRedisCache(options => options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(connMultiplexer));

		// db contexts
		// use newtonsoft json for postgres jsonb
		Npgsql.NpgsqlConnection.GlobalTypeMapper.UseJsonNet();
		var dbSection = config.GetSection("Databases");
		foreach (var section in dbSection.GetChildren())
		{
			string connString = section["ConnectionString"];
			string dbType = section["DbType"];
			string type = section["ContextType"];
			Type? typeObj = ReflectionHelpers.GetType(type, nsFilter);
			if (!string.IsNullOrWhiteSpace(dbType) && typeObj is not null)
			{
				var optionsAction = (DbContextOptionsBuilder options) =>
				{
					switch (dbType.ToLowerInvariant())
					{
					case "postgres":
						options.UseNpgsql(connString).EnableServiceProviderCaching();
						break;

					case "sqlite":
						options.UseSqlite(connString);
						break;

					case "sqlserver":
						options.UseSqlServer(connString).EnableServiceProviderCaching();
						break;

					default:
						throw new ArgumentException("DbType " + dbType + " not supported");
					}
				};
				var method = typeof(EntityFrameworkServiceCollectionExtensions)
					.GetMethod(nameof(EntityFrameworkServiceCollectionExtensions.AddPooledDbContextFactory),
					BindingFlags.Static | BindingFlags.Public,
					new Type[] { typeof(IServiceCollection), typeof(Action<DbContextOptionsBuilder>), typeof(int) });
				method!.MakeGenericMethod(typeObj).Invoke(null, new object[]
				{
					builder.Services,
					optionsAction,
					1024
				});
			}
		}

		if (System.Diagnostics.Debugger.IsAttached)
		{
			// standard console logger
		}
		else
		{
			// json console logger
			builder.Logging.ClearProviders();
			builder.Logging.AddConsole(options =>
			{
				options.FormatterName = JsonConsoleFormatter.Name;
			}).AddConsoleFormatter<JsonConsoleFormatter, JsonConsoleFormatterOptions>();
		}

		postSetup?.Invoke(builder.Services, builder.Configuration);

		return builder.Build();
	}

	/// <summary>
	/// Setup application middleware pipeline
	/// </summary>
	/// <param name="app">Web application</param>
	/// <returns>WebApplication</returns>
	public static WebApplication SetupMiddleware(this WebApplication app)
	{
		app.UseUnhandledExceptionMiddleware();
		app.UseSwagger();
		app.UseSwaggerUI();

		if (app.Environment.IsProduction())
		{
			app.UseHsts();
			//app.UseHttpsRedirection();
		}

		app.UseForwardedHeaders();
		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();
		app.UseStaticFiles();
		app.MapControllers();

		return app;
	}
}

