using Isopoh.Cryptography.Argon2;

namespace DigitalRuby.Core.Cryptography;

/// <summary>
/// Interface to hash secrets
/// </summary>
public interface ISecretHasher
{
	/// <summary>
	/// Compute hash from bytes
	/// </summary>
	/// <param name="bytes">Bytes</param>
	/// <param name="salt">Optional salt</param>
	/// <returns>Hashed bytes</returns>
	byte[] GetHash(byte[] bytes, byte[]? salt = null);
}

/// <summary>
/// Argon2 implementation of IHasher
/// </summary>
[Binding(ServiceLifetime.Singleton)]
public class Argon2Hasher_V2 : ISecretHasher
{
	/// <summary>
	/// Prefix for this hasher
	/// </summary>
	public static readonly IReadOnlyCollection<byte> Prefix = Encoding.UTF8.GetBytes("AV2_");

	/// <summary>
	/// Memory cost - this must never be change!
	/// </summary>
	private const int HashMemoryCost = 8192;

	/// <summary>
	/// Time cost - this must never change!
	/// </summary>
	private const int HashTimeCost = 5;

	/// <summary>
	/// Number of lanes - this must never change!
	/// </summary>
	private const int HashLanes = 16;

	/// <summary>
	/// Hash length - this must never change!
	/// </summary>
	private const int HashLength = 32;

	private readonly byte[]? secretBytes;
	private readonly byte[]? secretBytes2;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="options">Options</param>
	public Argon2Hasher_V2(Argon2Hasher_V2_Options options)
	{
		secretBytes = options.Secret;
		secretBytes2 = options.Secret2;
	}

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="secretBytes">Secret bytes</param>
	/// <param name="secretBytes2">Secret bytes2</param>
	public Argon2Hasher_V2(byte[]? secretBytes, byte[]? secretBytes2)
	{
		this.secretBytes = secretBytes;
		this.secretBytes2 = secretBytes2;
	}

	/// <inheritdoc />
	public byte[] GetHash(byte[] bytes, byte[]? salt = null)
	{
		if (salt is not null && salt.Length < 8)
		{
			byte[] newSalt = new byte[8];
			for (int i = 0; i < 8; i++)
			{
				newSalt[i] = i < salt.Length ? salt[i] : (byte)42;
			}
			salt = newSalt;
		}

		var config = new Argon2Config
		{
			Type = Argon2Type.HybridAddressing,
			Version = Argon2Version.Nineteen,
			TimeCost = HashTimeCost,
			MemoryCost = HashMemoryCost,
			Lanes = HashLanes,
			Threads = 1,
			Password = bytes,
			Salt = salt,
			Secret = secretBytes,
			AssociatedData = secretBytes2,
			HashLength = HashLength
		};

		using var argon = new Argon2(config);
		using var hashResult = argon.Hash();

		// copy data out, the hash result dispose will make the buffer invalid
		MemoryStream ms = new();
		foreach (var b in Prefix)
		{
			ms.WriteByte(b);
		}
		ms.Write(hashResult.Buffer);
		return ms.ToArray();
	}
}

/// <summary>
/// Argon 2 hasher options
/// </summary>
public class Argon2Hasher_V2_Options
{
	/// <summary>
	/// Secret
	/// </summary>
	public byte[]? Secret { get; set; }

	/// <summary>
	/// Other secret
	/// </summary>
	public byte[]? Secret2 { get; set; }
}
