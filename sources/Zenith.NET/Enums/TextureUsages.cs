namespace Zenith.NET;

[Flags]
public enum TextureUsages
{
    None = 0,

    ColorAttachment = 1 << 0,

    DepthStencil = 1 << 1,

    Sampled = 1 << 2,

    Storage = 1 << 3
}
