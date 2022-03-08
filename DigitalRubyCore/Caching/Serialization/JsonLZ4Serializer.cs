using FeatureFlags.Core.Compression;

using K4os.Compression.LZ4;

namespace FeatureFlags.Core.Caching.Serialization;

/// <inheritdoc />
public class JsonLZ4Serializer : ISerializer
{
	private static readonly Type byteArrayType = typeof(byte[]);

	/// <inheritdoc />
	public string TypeString => "js-lz4";

	/// <inheritdoc />
	public byte[] Serialize<T>(T obj)
	{
		// skip json for raw bytes
		if (typeof(T) == byteArrayType)
		{
			return CompressionHelpers.CompressLZ4((byte[])(object)obj!);
		}

		var stringBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Formatting.None));
		return CompressionHelpers.CompressLZ4(stringBytes);
	}

	/// <inheritdoc />
	public T Deserialize<T>(byte[] bytes)
	{
		// skip json for raw bytes
		if (typeof(T) == byteArrayType)
		{
			return (T)(object)CompressionHelpers.DecompressLZ4(bytes);
		}

		var stringBytes = CompressionHelpers.DecompressLZ4(bytes);
		var jsonString = Encoding.UTF8.GetString(stringBytes);
		return JsonConvert.DeserializeObject<T>(jsonString)!;
	}
}
