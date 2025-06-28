namespace Zenith.NET;

[Flags]
public enum TextureUsageFlags
{
    None = 0,

    RenderTarget = 1 << 0,

    DepthStencil = 1 << 1,

    ShaderReadOnly = 1 << 2,

    ShaderReadWrite = 1 << 3
}
