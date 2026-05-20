namespace Zenith.NET;

[Flags]
public enum TextureUsages
{
    None = 0,

    CopySrc = 1 << 0,

    CopyDst = 1 << 1,

    Sampled = 1 << 2,

    Storage = 1 << 3,

    ColorAttachment = 1 << 4,

    DepthStencil = 1 << 5
}
