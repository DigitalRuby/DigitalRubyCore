using System.Runtime.Serialization;

namespace DigitalRuby.Core.Services;

/// <summary>
/// Base request class for requests
/// </summary>
public class BaseRequest
{
	/// <summary>
	/// Auto-populated, the user id making the request or 0 if anonymous
	/// </summary>
	[JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	[IgnoreDataMember]
	public string CurrentUserId { get; set; } = string.Empty;

	/// <summary>
	/// Auto-populated, device info making the request or null if unknown
	/// </summary>
	[JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	[IgnoreDataMember]
	public DeviceInfo? CurrentDevice { get; set; }
}

/// <summary>
/// Device info
/// </summary>
public class DeviceInfo
{
	/// <summary>
	/// OS family
	/// </summary>
	public string? OSFamily { get; set; }

	/// <summary>
	/// OS version
	/// </summary>
	public Version? OSVersion { get; set; }

	/// <summary>
	/// Device family
	/// </summary>
	public string? DeviceFamily { get; set; }

	/// <summary>
	/// Device model
	/// </summary>
	public string? DeviceModel { get; set; }

	/// <summary>
	/// Device brand
	/// </summary>
	public string? DeviceBrand { get; set; }

	/// <summary>
	/// IP address
	/// </summary>
	public string? IPAddress { get; set; }
}

/// <summary>
/// Version
/// </summary>
public struct Version : IComparable
{
	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="major">Major version</param>
	/// <param name="minor">Minor version</param>
	/// <param name="patch">Patch version</param>
	public Version(int major, int minor, int patch)
	{
		Major = major;
		Minor = minor;
		Patch = patch;
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return $"{Major}.{Minor}.{Patch}";
	}

	/// <summary>
	/// String implicit conversion from string to version
	/// </summary>
	/// <param name="v">Version</param>
	public static implicit operator string(Version v)
	{
		return v.ToString();
	}

	/// <summary>
	/// String implicit conversion to version from string
	/// </summary>
	/// <param name="s"></param>
	public static implicit operator Version(string s)
	{
		string[] pieces = s.Split('.');
		if (pieces.Length == 0 || !int.TryParse(pieces[0], NumberStyles.None, CultureInfo.InvariantCulture, out int majorVersion))
		{
			return new Version();
		}
		int minorVersion = 0;
		int patchVersion = 0;
		if (pieces.Length > 1)
		{
			int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out minorVersion);
			if (pieces.Length > 2)
			{
				int.TryParse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture, out patchVersion);
			}
		}
		return new Version(majorVersion, minorVersion, patchVersion);
	}

	/// <summary>
	/// System.Version implicit conversion to version from System.Version
	/// </summary>
	/// <param name="v">System version</param>
	public static implicit operator Version(System.Version v)
	{
		return new(v.Major, v.Minor, v.Build);
	}

	/// <summary>
	/// Major
	/// </summary>
	public int Major { get; set; }

	/// <summary>
	/// Minor
	/// </summary>
	public int Minor { get; set; }

	/// <summary>
	/// Patch
	/// </summary>
	public int Patch { get; set; }

	/// <inheritdoc />
	public int CompareTo(object? obj)
	{
		if (obj is Version v)
		{
			int compare = Major.CompareTo(v.Major);
			if (compare == 0)
			{
				compare = Minor.CompareTo(v.Minor);
				if (compare == 0)
				{
					compare = Patch.CompareTo(v.Patch);
				}
			}
			return compare;
		}
		return -1;
	}
}
