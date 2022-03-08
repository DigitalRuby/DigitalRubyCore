namespace FeatureFlags.Core.Validation;

/// <summary>
/// Helper methods for validating data
/// </summary>
public static class ValidationHelpers
{
	/// <summary>
	/// Validate and sanitize email address
	/// </summary>
	/// <param name="email">Email address</param>
	/// <returns>Validated and sanitized email address or null if email was null</returns>
	public static string? ValidateAndSanitizeEmail(string? email)
	{
		if (email is null)
		{
			return null;
		}

		try
		{
			var emailObj = new System.Net.Mail.MailAddress(email);
			email = emailObj.ToString();
		}
		catch
		{
			throw new ArgumentException("Email " + email + " is not a valid format");
		}
		return email;
	}

	/// <summary>
	/// Clean a string (normalize + trim)
	/// </summary>
	/// <param name="stringToClean">String</param>
	/// <returns>Normalized and trimmed string or null if stringToClean is null</returns>
	public static string? Clean(this string? stringToClean)
	{
		if (stringToClean is null)
		{
			return null;
		}
		return stringToClean.Trim().Normalize();
	}
}

