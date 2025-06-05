namespace Zenith.NET;

/// <summary>
/// Represents a sampler state object, which defines how textures are sampled in shaders.
/// </summary>
public abstract class Sampler(GraphicsContext context, SamplerDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the sampler description.
    /// </summary>
    public SamplerDesc Desc { get; } = desc;
}
