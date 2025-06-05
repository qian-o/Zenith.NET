namespace Zenith.NET;

/// <summary>
/// Describes a single resource binding element for a shader or pipeline, including its type, binding index, array count, and shader stage visibility.
/// </summary>
public record struct ResourceElementDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceElementDesc"/> struct with default values.
    /// </summary>
    public ResourceElementDesc()
    {
        Type = ResourceType.ConstantBuffer;
        Index = 0;
        Count = 1;
        StageFlags = ShaderStageFlags.None;
    }

    /// <summary>
    /// Specifies the type of resource to be bound (e.g., buffer, texture, sampler).
    /// </summary>
    public ResourceType Type { get; set; }

    /// <summary>
    /// The binding index of the resource within the shader or pipeline layout.
    /// </summary>
    public uint Index { get; set; }

    /// <summary>
    /// For regular resources, this value is 1; for array resources, this value is the size of the array.
    /// </summary>
    public uint Count { get; set; }

    /// <summary>
    /// Specifies which shader stages can access this resource.
    /// </summary>
    public ShaderStageFlags StageFlags { get; set; }

    /// <summary>
    /// Validates the current <see cref="ResourceElementDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!Enum.IsDefined(Type))
        {
            return false;
        }

        if (Count is 0)
        {
            return false;
        }

        if (StageFlags is ShaderStageFlags.None)
        {
            return false;
        }

        return true;
    }
}
