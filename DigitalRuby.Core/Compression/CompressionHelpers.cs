using K4os.Compression.LZ4;

namespace DigitalRuby.Core.Compression;

/// <summary>
/// Compression helper methods
/// </summary>
public static class CompressionHelpers
{
	/// <summary>
	/// Convert uncompressed bytes to LZ4 compressed bytes
	/// </summary>
	/// <param name="data">Bytes</param>
	/// <returns>Compressed LZ4 bytes</returns>
	public static byte[] CompressLZ4(byte[] data)
	{
		return LZ4Pickler.Pickle(data);
	}

	/// <summary>
	/// Convert compressed LZ4 bytes to uncompressed bytes
	/// </summary>
	/// <param name="data">Compressed LZ4 bytes</param>
	/// <returns>Uncompressed bytes</returns>
	public static byte[] DecompressLZ4(byte[] data)
	{
		return LZ4Pickler.Unpickle(data);
	}
}
