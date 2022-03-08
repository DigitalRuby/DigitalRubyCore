namespace FeatureFlags.Core.Cloud.AWS;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

/// <summary>
/// Interface for dynamo db access
/// </summary>
public interface IDynamoDbRepository
{
	/// <summary>
	/// Create a dynamo table
	/// </summary>
	/// <param name="tableName">Table name</param>
	/// <param name="attributes">Key attributes</param>
	/// <param name="provisioning">Provisioned throughput or null for per request</param>
	/// <param name="cancelToken">Cancel token</param>
	/// <returns>Task</returns>
	Task<CreateTableResponse> CreateTableAsync(string tableName, IReadOnlyCollection<DynamoDbTableAttribute> attributes,
		DynamoDbProvisionedThroughput? provisioning = null, CancellationToken cancelToken = default);

	/// <summary>
	/// Delete a dynamo table
	/// </summary>
	/// <param name="tableName">Table name</param>
	/// <param name="cancelToken">Cancel token</param>
	/// <returns>Task</returns>
	Task DeleteTableAsync(string tableName, CancellationToken cancelToken = default);

	/// <summary>
	/// Get a dynamo table to perform operations on
	/// </summary>
	/// <param name="tableName">Table name</param>
	/// <returns>Task with the table</returns>
	Table GetTableClient(string tableName);

	/// <summary>
	/// Convert a type to a dynamodb document
	/// </summary>
	/// <typeparam name="T">Type</typeparam>
	/// <param name="entity">Entity</param>
	/// <returns>Document</returns>
	Document ToDocument<T>(T entity);

	/// <summary>
	/// Convert a dynamodb document to a type
	/// </summary>
	/// <typeparam name="T">Type</typeparam>
	/// <param name="doc">Document</param>
	/// <returns>Type</returns>
	T FromDocument<T>(Document doc);
}

/// <summary>
/// Provides access to dynamo db
/// </summary>
[Binding(ServiceLifetime.Singleton)]
public class DynamoDbRepository : IDynamoDbRepository
{
	protected readonly AmazonDynamoDBClient? client;
	protected readonly DynamoDBContext? dbContext;

	/// <summary>
	/// No-op constructor if no config
	/// </summary>
	public DynamoDbRepository()
	{
	}

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="config">Config</param>
	public DynamoDbRepository(DynamoDbConfiguration config)
	{
		var dynamoConfig = new AmazonDynamoDBConfig
		{
			RegionEndpoint = AwsHelpers.ParseEndPoint(config.Region)
		};
		this.client = new AmazonDynamoDBClient(config.AccessKey, config.SecretKey, dynamoConfig);
		this.dbContext = new DynamoDBContext(client, new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 });
	}

	/// <inheritdoc />
	public async Task<CreateTableResponse> CreateTableAsync(string tableName, IReadOnlyCollection<DynamoDbTableAttribute> attributes,
		DynamoDbProvisionedThroughput? provisionedThroughput = null, CancellationToken cancelToken = default)
	{
		var attributesAws = new List<Amazon.DynamoDBv2.Model.AttributeDefinition>();
		var keySchemaAws = new List<Amazon.DynamoDBv2.Model.KeySchemaElement>();
		foreach (var attribute in attributes)
		{
			ScalarAttributeType type = attribute.Type switch
			{
				AttributeType.String => ScalarAttributeType.S,
				AttributeType.Number => ScalarAttributeType.N,
				AttributeType.Binary => ScalarAttributeType.B,
				AttributeType.SetStrings => "SS",
				AttributeType.SetNumbers => "NS",
				AttributeType.SetBinaries => "BS",
				_ => throw new ArgumentException("Unsupported attribute type " + attribute.Type),
			};
			attributesAws.Add(new(attribute.Name, type));
			Amazon.DynamoDBv2.KeyType type2 = (attribute.KeyType == KeyType.Hash ? Amazon.DynamoDBv2.KeyType.HASH : Amazon.DynamoDBv2.KeyType.RANGE);
			keySchemaAws.Add(new(attribute.Name, type2));
		}
		long readCapacity = provisionedThroughput?.ReadCapacityUnits ?? 0;
		long writeCapacity = provisionedThroughput?.WriteCapacityUnits ?? 0;
		var provisionedThroughputAws = (readCapacity <= 0 || writeCapacity <= 0 ? null : new Amazon.DynamoDBv2.Model.ProvisionedThroughput(readCapacity, writeCapacity));
		var request = new CreateTableRequest(tableName, keySchemaAws, attributesAws, provisionedThroughputAws);
		if (provisionedThroughputAws is null)
		{
			request.BillingMode = BillingMode.PAY_PER_REQUEST;
		}
		else
		{
			request.BillingMode = BillingMode.PROVISIONED;
		}
		var result = await client!.CreateTableAsync(request, cancelToken);
		Stopwatch timer = Stopwatch.StartNew();
		while (true)
		{
			if (timer.Elapsed.TotalSeconds > 10.0)
			{
				throw new TimeoutException("Timed out waiting for table " + tableName + " to be active");
			}
			var desc = await client.DescribeTableAsync(tableName, cancelToken);
			if (desc.Table.TableStatus == TableStatus.ACTIVE)
			{
				break;
			}
		}

		return result;
	}

	/// <inheritdoc />
	public Task DeleteTableAsync(string tableName, CancellationToken cancelToken = default)
	{
		return client!.DeleteTableAsync(tableName, cancelToken);
	}

	/// <inheritdoc />
	public Table GetTableClient(string tableName)
	{
		return Table.LoadTable(client, tableName);
	}

	/// <inheritdoc />
	public Document ToDocument<T>(T entity)
	{
		return dbContext!.ToDocument(entity);
	}

	/// <inheritdoc />
	public T FromDocument<T>(Document doc)
	{
		return dbContext!.FromDocument<T>(doc);
	}
}

/// <summary>
/// Config for dynamodb repository
/// </summary>
[Configuration("DynamoDbConfig")]
public class DynamoDbConfiguration
{
	/// <summary>
	/// The region ths config is in
	/// </summary>
	public string Region { get; set; } = nameof(Amazon.RegionEndpoint.USEast2);
	
	/// <summary>
	/// Dynamo access key
	/// </summary>
	public string AccessKey { get; set; } = string.Empty;

	/// <summary>
	/// Dynamo secret key
	/// </summary>
	public string SecretKey { get; set; } = string.Empty;
}

/// <summary>
/// Dynamo table attribute
/// </summary>
public class DynamoDbTableAttribute
{
	/// <summary>
	/// Attribute field name
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Attribute type
	/// </summary>
	public AttributeType Type { get; set; }

	/// <summary>
	/// Key type
	/// </summary>
	public KeyType KeyType { get; set; }
}

/// <summary>
/// Attribute types
/// </summary>
public enum AttributeType
{
	/// <summary>
	/// String
	/// </summary>
	String,

	/// <summary>
	/// Number
	/// </summary>
	Number,

	/// <summary>
	/// Binary
	/// </summary>
	Binary,

	/// <summary>
	/// Set of strings
	/// </summary>
	SetStrings,

	/// <summary>
	/// Set of numbers
	/// </summary>
	SetNumbers,

	/// <summary>
	/// Set of binaries
	/// </summary>
	SetBinaries
}

/// <summary>
/// Key types
/// </summary>
public enum KeyType
{
	/// <summary>
	/// Hash
	/// </summary>
	Hash,

	/// <summary>
	/// Range
	/// </summary>
	Range
}

/// <summary>
/// Dynamo provisioned throughput
/// </summary>
public class DynamoDbProvisionedThroughput
{
	/// <summary>
	/// Read capacity units
	/// </summary>
	public int ReadCapacityUnits { get; set; }

	/// <summary>
	/// Write capacity units
	/// </summary>
	public int WriteCapacityUnits { get; set; }
}

