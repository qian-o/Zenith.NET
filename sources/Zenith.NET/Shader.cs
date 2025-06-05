namespace Zenith.NET;

/// <summary>
/// Represents a shader module, encapsulating compiled shader bytecode and its stage information.
/// </summary>
public abstract class Shader(GraphicsContext context, ShaderDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the shader description.
    /// </summary>
    public ShaderDesc Desc { get; } = desc;
}
