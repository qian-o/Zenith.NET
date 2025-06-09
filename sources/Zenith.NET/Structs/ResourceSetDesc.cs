namespace Zenith.NET;

/// <summary>
/// Describes a set of GPU resources to be bound together, including the associated resource layout and resource array.
/// </summary>
public record struct ResourceSetDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceSetDesc"/> struct with default values.
    /// </summary>
    public ResourceSetDesc()
    {
        Layout = null!;
        Resources = [];
    }

    /// <summary>
    /// The resource layout that defines the expected types and arrangement of resources.
    /// </summary>
    public ResourceLayout Layout { get; set; }

    /// <summary>
    /// The array of resources to be bound according to the layout.
    /// </summary>
    public GraphicsResource[] Resources { get; set; }

    /// <summary>
    /// Validates the current <see cref="ResourceSetDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (Layout is null)
        {
            return false;
        }

        if (Resources is null)
        {
            return false;
        }

        uint index = 0;
        foreach (ResourceElementDesc element in Layout.Desc.Elements)
        {
            Func<GraphicsResource, bool> validateFunc = element.Type switch
            {
                ResourceType.ConstantBuffer
                or ResourceType.StructuredBuffer
                or ResourceType.StructuredBufferReadWrite => static resource => resource is Buffer,

                ResourceType.Texture
                or ResourceType.TextureReadWrite => static resource => resource is Texture,

                ResourceType.Sampler => static resource => resource is Sampler,

                ResourceType.AccelerationStructure => static resource => resource is TLAS,

                _ => _ => false
            };

            for (uint i = 0; i < element.Count; i++)
            {
                if (index >= Resources.Length)
                {
                    return false;
                }

                if (!validateFunc(Resources[index]))
                {
                    return false;
                }

                index++;
            }
        }

        return true;
    }
}
