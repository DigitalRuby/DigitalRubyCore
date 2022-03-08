namespace DigitalRuby.Core.DependencyInjection;

/// <summary>
/// Register a class for DI from configuration
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ConfigurationAttribute : Attribute
{
	/// <summary>
	/// Config path
	/// </summary>
	public string ConfigPath { get; }

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="configPath">Config to path to bind from configuration</param>
	public ConfigurationAttribute(string configPath)
	{
		ConfigPath = configPath;
	}
}
