namespace FeatureFlags.Core.Cloud.AWS;

public static class AwsHelpers
{
	/// <summary>
	/// Parse a string into an Amazon region
	/// </summary>
	/// <param name="value">Value</param>
	/// <returns>Region end point or default if parse fail</returns>
	public static Amazon.RegionEndpoint ParseEndPoint(string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			FieldInfo? field = typeof(Amazon.RegionEndpoint).GetField(value, BindingFlags.Static | BindingFlags.Public);
			if (field is not null)
			{
				Amazon.RegionEndpoint? fieldValue = field.GetValue(null) as Amazon.RegionEndpoint;
				if (fieldValue is not null)
				{
					return fieldValue;
				}
			}
		}
		return Amazon.RegionEndpoint.USEast2;
	}
}