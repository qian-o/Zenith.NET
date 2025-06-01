namespace Zenith.NET;

[Flags]
public enum TextureUsage
{
    None = 0,

    /// <summary>
    /// A Texture can be used as a shader resource.
    /// </summary>
    ShaderResource = 1 << 0,

    /// <summary>
    /// A Texture can be used as an unordered-access resource.
    /// </summary>
    UnorderedAccess = 1 << 1,

    /// <summary>
    /// A Texture can be used as a render target.
    /// </summary>
    RenderTarget = 1 << 2,

    /// <summary>
    /// A Texture can be used as a depth-stencil buffer.
    /// </summary>
    DepthStencil = 1 << 3
}
