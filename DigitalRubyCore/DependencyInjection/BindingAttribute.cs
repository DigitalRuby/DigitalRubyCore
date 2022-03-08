namespace FeatureFlags.Core.DependencyInjection;

/// <summary>
/// Register a class for DI
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class BindingAttribute : Attribute
{
    /// <summary>
    /// Service scope
    /// </summary>
    public ServiceLifetime Scope { get; }

    /// <summary>
    /// Whether to bind all interfaces
    /// </summary>
    public bool BindInterfaces { get; }

    private static readonly IReadOnlyCollection<Type> ignoreServiceInterfaces = new HashSet<Type>
    {
        typeof(IDisposable),
        typeof(IAsyncDisposable),
        typeof(ICloneable),
        typeof(IComparable),
        typeof(IComparer),
        typeof(IConvertible),
        typeof(IEnumerable),
        typeof(IEnumerator),
        typeof(IEqualityComparer),
        typeof(IEquatable<>),
        typeof(IList),
        typeof(IOrderedEnumerable<>),
        typeof(IOrderedQueryable),
        typeof(IOrderedQueryable<>),
        typeof(IQueryable),
        typeof(IQueryable<>),
        typeof(IReadOnlyCollection<>),
        typeof(IReadOnlyDictionary<,>),
        typeof(IReadOnlyList<>),
        typeof(IReadOnlySet<>),
        typeof(ISet<>)
    };

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="scope">Lifetime of this service</param>
    /// <param name="bindInterfaces">Whether to bind all interfaces</param>
    public BindingAttribute(ServiceLifetime scope, bool bindInterfaces = true)
    {
        Scope = scope;
        BindInterfaces = bindInterfaces;
    }

    /// <summary>
    /// Bind a service given the life time
    /// </summary>
    /// <param name="services">Services</param>
    /// <param name="type">Type of service</param>
    public void BindServiceOfType(IServiceCollection services, Type type)
    {
        if (type.IsAbstract ||
            (Scope != ServiceLifetime.Singleton && Scope != ServiceLifetime.Scoped && Scope != ServiceLifetime.Transient))
        {
            return;
        }

        services.Add(new ServiceDescriptor(type, type, Scope));

        if (BindInterfaces)
        {
            foreach (var interfaceToBind in type.GetInterfaces())
            {
                if (!ignoreServiceInterfaces.Contains(interfaceToBind))
                {
                    services.Add(new ServiceDescriptor(interfaceToBind, type, Scope));
                }
            }
        }
    }
}
