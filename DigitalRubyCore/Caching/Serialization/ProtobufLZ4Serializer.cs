using ProtoBuf;

namespace FeatureFlags.Core.Caching.Serialization;

/// <inheritdoc />
public class ProtobufLZ4Serializer : ISerializer
{
	/// <inheritdoc />
	public string TypeString => "pb-lz4";

	/// <inheritdoc />
	public byte[] Serialize<T>(T obj)
	{
		using var ms = new MemoryStream();
		Serializer.Serialize(ms, obj);
		return ms.ToArray();
	}

	/// <inheritdoc />
	public T Deserialize<T>(byte[] bytes)
	{
		using var ms = new MemoryStream(bytes);
		return Serializer.Deserialize<T>(ms);
	}
}
