namespace Zenith.NET;

[Flags]
public enum TextureUsageFlags
{
    None = 0,

    RenderTarget = 1 << 0,

    DepthStencil = 1 << 1,

    ShaderResource = 1 << 2,

    UnorderedAccess = 1 << 3,

    Dynamic = 1 << 4
}
