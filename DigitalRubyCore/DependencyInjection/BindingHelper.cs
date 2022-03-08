namespace FeatureFlags.Core.DependencyInjection;

/// <summary>
/// Dependency injection helper for services
/// </summary>
public static class BindingHelper
{
    /// <summary>
    /// Bind all services with binding attribute
    /// </summary>
    /// <param name="services">Service collection</param>
	/// <param name="namespacePrefix">Namespace prefix to only bind types from a certain set of assemblies</param>
    public static void BindServicesFromBindingAttribute(this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
		string? namespacePrefix = null)
    {
        Type attributeType = typeof(BindingAttribute);
        foreach (var type in ReflectionHelpers.GetAllTypes(namespacePrefix))
        {
            var attr = type.GetCustomAttributes(attributeType, true);
            if (attr is not null && attr.Length != 0)
            {
                ((BindingAttribute)attr[0]).BindServiceOfType(services, type);
            }
        }
    }

	/// <summary>
	/// Bind an object from configuration as a singleton
	/// </summary>
	/// <typeparam name="T">Type of object</typeparam>
	/// <param name="services">Services</param>
	/// <param name="configuration">Configuration</param>
	/// <param name="key">Key to read from configuration</param>
	public static void BindSingleton<T>(this IServiceCollection services, IConfiguration configuration, string key) where T : class, new()
	{
		T configObj = new();
		configuration.Bind(key, configObj);
		services.AddSingleton<T>(configObj);
	}

	/// <summary>
	/// Bind an object from configuration as a singleton
	/// </summary>
	/// <param name="services">Services</param>
	/// <param name="type">Type of object to bind</param>
	/// <param name="configuration">Configuration</param>
	/// <param name="key">Key to read from configuration</param>
	public static void BindSingleton(this IServiceCollection services, Type type, IConfiguration configuration, string key)
	{
		object configObj = Activator.CreateInstance(type) ?? throw new ApplicationException("Failed to create object of type " + type.FullName);
		configuration.Bind(key, configObj);
		services.AddSingleton(type, configObj);
	}

	/// <summary>
	/// Bind all classes with configuration attribute from configuration as singletons
	/// </summary>
	/// <param name="services">Service collection</param>
	/// <param name="configuration">Configuration</param>
	/// <param name="namespacePrefix">Namespace prefix to only bind types from a certain set of assemblies</param>
	public static void BindConfigurationFromConfigurationAttribute(this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
		IConfiguration configuration,
		string? namespacePrefix = null)
	{
		Type attributeType = typeof(ConfigurationAttribute);
		foreach (var type in ReflectionHelpers.GetAllTypes(namespacePrefix))
		{
			var attr = type.GetCustomAttributes(attributeType, true);
			if (attr is not null && attr.Length != 0)
			{
				string path = ((ConfigurationAttribute)attr[0]).ConfigPath;
				services.BindSingleton(type, configuration, path);
			}
		}
	}
}
