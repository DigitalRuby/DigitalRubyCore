namespace FeatureFlags.Core.Logging;

/// <summary>
/// Actually honors max depth when writing json
/// </summary>
public class MaxDepthJsonTextWriter : JsonTextWriter
{
	/// <summary>
	/// Max depth
	/// </summary>
	public int? MaxDepth { get; set; }

	/// <summary>
	/// Max observed depth
	/// </summary>
	public int MaxObservedDepth { get; private set; }

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="writer">Text writer</param>
	/// <param name="settings">Json serializer settings</param>
	public MaxDepthJsonTextWriter(TextWriter writer, JsonSerializerSettings settings)
		: base(writer)
	{
		this.MaxDepth = (settings?.MaxDepth);
		this.MaxObservedDepth = 0;
	}

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="writer">Text writer</param>
	/// <param name="maxDepth">Max depth</param>
	public MaxDepthJsonTextWriter(TextWriter writer, int? maxDepth)
		: base(writer)
	{
		this.MaxDepth = maxDepth;
	}

	/// <inheritdoc />
	public override void WriteStartArray()
	{
		base.WriteStartArray();
		CheckDepth();
	}

	/// <inheritdoc />
	public override void WriteStartConstructor(string name)
	{
		base.WriteStartConstructor(name);
		CheckDepth();
	}

	/// <inheritdoc />
	public override void WriteStartObject()
	{
		base.WriteStartObject();
		CheckDepth();
	}

	private void CheckDepth()
	{
		MaxObservedDepth = Math.Max(MaxObservedDepth, Top);
		if (Top > MaxDepth)
		{
			throw new JsonSerializationException(string.Format("Depth {0} Exceeds MaxDepth {1} at path \"{2}\"", Top, MaxDepth, Path));
		}
	}
}
