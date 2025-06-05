namespace Zenith.NET;

/// <summary>
/// Specifies intended usages for a texture resource.
/// </summary>
[Flags]
public enum TextureUsageFlags
{
    None = 0,

    /// <summary>
    /// The texture can be used as a shader resource (readable in shaders).
    /// </summary>
    ShaderResource = 1 << 0,

    /// <summary>
    /// The texture can be used as an unordered-access resource (read/write in compute or pixel shaders).
    /// </summary>
    UnorderedAccess = 1 << 1,

    /// <summary>
    /// The texture can be used as a render target (color attachment for rendering).
    /// </summary>
    RenderTarget = 1 << 2,

    /// <summary>
    /// The texture can be used as a depth-stencil buffer.
    /// </summary>
    DepthStencil = 1 << 3
}
