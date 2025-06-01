namespace Zenith.NET;

public enum ResourceType
{
    /// <summary>
    /// Bind as constant buffer.
    /// </summary>
    ConstantBuffer,

    /// <summary>
    /// Bind as read-only storage buffer.
    /// </summary>
    StructuredBuffer,

    /// <summary>
    /// Bind as read-write storage buffer.
    /// </summary>
    StructuredBufferReadWrite,

    /// <summary>
    /// Bind as read-only texture.
    /// </summary>
    Texture,

    /// <summary>
    /// Bind as read-write texture.
    /// </summary>
    TextureReadWrite,

    /// <summary>
    /// Bind as sampler.
    /// </summary>
    Sampler,

    /// <summary>
    /// Bind as ray tracing acceleration structure.
    /// </summary>
    AccelerationStructure
}
