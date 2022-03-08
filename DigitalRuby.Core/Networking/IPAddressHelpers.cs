using System.Net;

namespace DigitalRuby.Core.Networking;

/// <summary>
/// Helpers for ip addresses
/// </summary>
public static class IPAddressHelpers
{
	private static readonly char[] cfHeaderSeparators = new char[] { ',', ';' };

	/// <summary>
	/// Map ip address to ipv4 if it is an ipv6 address mapped to ipv4
	/// </summary>
	/// <param name="ip">IP address</param>
	/// <returns>IP address mapped to ipv4 if mapped to ipv4 in ipv6 format</returns>
	private static System.Net.IPAddress? MapToIPv4IfIPv6(this System.Net.IPAddress? ip)
	{
		if (ip is null)
		{
			return ip;
		}
		else if (ip.IsIPv4MappedToIPv6)
		{
			return ip.MapToIPv4();
		}
		return ip;
	}

	/// <summary>
	/// Remove the scope id from the ip address if there is a scope id
	/// </summary>
	/// <param name="ipAddress">IP address to remove scope id from</param>
	/// <param name="ownsIP">Whether this ip is owned by the caller</param>
	/// <returns>This ip address if no scope id removed, otherwise a new ip address with scope removed if ownsIP is false, or the same ip
	/// with scope removed if ownsIP is true</returns>
	private static IPAddress? RemoveScopeId(this IPAddress? ipAddress, bool ownsIP = false)
	{
		if (ipAddress is null)
		{
			return null;
		}

		if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
		{
			if (ownsIP)
			{
				ipAddress.ScopeId = 0;
			}
			else
			{
				return new IPAddress(ipAddress.GetAddressBytes());
			}
		}
		return ipAddress;
	}

	/// <summary>
	/// Clean ip address - remove scope and convert to ipv4 if ipv6 mapped to ipv4
	/// </summary>
	/// <param name="ip">IP address</param>
	/// <param name="ownsIP">Whether this ip is owned by the caller</param>
	/// <returns>Cleaned ip address</returns>
	public static System.Net.IPAddress? Clean(this System.Net.IPAddress? ip, bool ownsIP = false)
	{
		return ip?.RemoveScopeId(ownsIP).MapToIPv4IfIPv6();
	}

	/// <summary>
	/// An extension method to determine if an IP address is internal, as specified in RFC1918
	/// </summary>
	/// <param name="ip">The IP address that will be tested</param>
	/// <returns>Returns true if the IP is internal or null, false if it is external</returns>
	public static bool IsInternal(this System.Net.IPAddress? ip)
	{
		if (ip is null)
		{
			return true;
		}

		try
		{
			ip = ip.Clean()!;
			if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
			{
				byte[] bytes = ip.GetAddressBytes();
				if (bytes is null || bytes.Length < 4)
				{
					return true;
				}
				return bytes[0] switch
				{
					10 or 127 => true,
					172 => bytes[1] >= 16 && bytes[1] < 32,
					192 => bytes[1] == 168,
					0 => true,
					_ => false,
				};
			}

			string addressAsString = ip.ToString();

			// equivalent of 127.0.0.1 in IPv6
			if (string.IsNullOrWhiteSpace(addressAsString) ||
				addressAsString.Length < 3 ||
				addressAsString == "::1")
			{
				return true;
			}

			// The original IPv6 Site Local addresses (fec0::/10) are deprecated. Unfortunately IsIPv6SiteLocal only checks for the original deprecated version:
			else if (ip.IsIPv6SiteLocal)
			{
				return true;
			}

			string firstWord = addressAsString.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0];

			// These days Unique Local Addresses (ULA) are used in place of Site Local. 
			// ULA has two variants: 
			//      fc00::/8 is not defined yet, but might be used in the future for internal-use addresses that are registered in a central place (ULA Central). 
			//      fd00::/8 is in use and does not have to registered anywhere.
			if (firstWord.Length >= 4 && firstWord[..2] == "fc")
			{
				return true;
			}
			else if (firstWord.Length >= 4 && firstWord[..2] == "fd")
			{
				return true;
			}
			// Link local addresses (prefixed with fe80) are not routable
			else if (firstWord == "fe80")
			{
				return true;
			}
			// Discard Prefix
			else if (firstWord == "100")
			{
				return true;
			}

			// Any other IP address is not Unique Local Address (ULA)
			return false;
		}
		catch
		{
			//Logger.Warn("Invalid ip isinternal check: {0}, {1}", ip, ex);
			return true;
		}
	}

	/// <summary>
	/// Get remote ip address, optionally allowing for x-forwarded-for or x-real-ip header check
	/// Falls back to the socket address if no found headers
	/// </summary>
	/// <param name="context">Http context</param>
	/// <param name="mode">Whether to allow x-forwarded-for or x-real-ip header check, default is to allow forwarded headers</param>
	/// <returns>IPAddress</returns>
	public static System.Net.IPAddress? GetRemoteIPAddress(this HttpContext context, RemoteAddressMode mode = RemoteAddressMode.Forwarded)
	{
		if (mode == RemoteAddressMode.Forwarded)
		{
			string cfHeader = context.Request.Headers["CF-Connecting-IP"];
			string cfBackupHeader = context.Request.Headers["X-Forwarded-For"];
			string header = (string.IsNullOrWhiteSpace(cfHeader) ? cfBackupHeader : cfHeader);
			if (!string.IsNullOrWhiteSpace(header))
			{
				int pos = header.IndexOfAny(cfHeaderSeparators);
				if (pos >= 0)
				{
					header = header[..pos];
				}
				header = header.Trim();
				if (System.Net.IPAddress.TryParse(header, out System.Net.IPAddress? ip))
				{
					return ip.Clean();
				}
			}
		}
		else if (mode == RemoteAddressMode.RealIP)
		{
			string realIP = context.Request.Headers["X-Real-IP"];
			if (!string.IsNullOrWhiteSpace(realIP) && System.Net.IPAddress.TryParse(realIP, out System.Net.IPAddress? ipAddress))
			{
				return ipAddress.Clean();
			}
		}
		return context.Connection.RemoteIpAddress.Clean();
	}
}

/// <summary>
/// Remote address modes
/// </summary>
public enum RemoteAddressMode
{
	/// <summary>
	/// Use the socket ip address of the connection, for a proxy like nginx this will be a local machine ip
	/// </summary>
	Socket,

	/// <summary>
	/// Use the forwarded header address, this is the origin, or start of the request
	/// </summary>
	Forwarded,

	/// <summary>
	/// Use the real ip header address, this is usually whatever connected to the last proxy in the chain
	/// </summary>
	RealIP
}

