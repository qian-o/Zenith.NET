namespace Zenith.NET;

[Flags]
public enum TextureUsages
{
    None = 0,

    CopySrc = 1 << 0,

    CopyDst = 1 << 1,

    ColorAttachment = 1 << 2,

    DepthStencil = 1 << 3,

    Sampled = 1 << 4,

    Storage = 1 << 5
}
