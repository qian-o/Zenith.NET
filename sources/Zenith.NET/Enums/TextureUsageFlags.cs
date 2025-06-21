namespace Zenith.NET;

[Flags]
public enum TextureUsageFlags
{
    None = 0,

    ShaderResource = 1 << 0,

    UnorderedAccess = 1 << 1,

    RenderTarget = 1 << 2,

    DepthStencil = 1 << 3
}
