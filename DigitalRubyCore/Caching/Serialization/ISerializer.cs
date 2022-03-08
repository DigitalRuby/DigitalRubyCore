namespace FeatureFlags.Core.Caching.Serialization;

/// <summary>
/// Interface for serialization
/// </summary>
public interface ISerializer
{
	/// <summary>
	/// Identifies the type of serializer using a short string
	/// </summary>
	string TypeString { get; }

	/// <summary>
	/// Deserializes bytes to an object
	/// </summary>
	/// <typeparam name="T">Type of object</typeparam>
	/// <param name="bytes">Bytes</param>
	/// <returns>Object</returns>
	T Deserialize<T>(byte[] bytes);

	/// <summary>
	/// Serializes bytes to an object
	/// </summary>
	/// <typeparam name="T">Type of object</typeparam>
	/// <param name="obj">Object to serialize</param>
	/// <returns>Bytes</returns>
	byte[] Serialize<T>(T obj);
}
