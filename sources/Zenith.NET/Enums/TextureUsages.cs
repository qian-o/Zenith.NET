namespace Zenith.NET;

[Flags]
public enum TextureUsages
{
    None = 0,

    Sampled = 1 << 0,

    Storage = 1 << 1,

    ColorAttachment = 1 << 2,

    DepthStencilAttachment = 1 << 3,

    CopySrc = 1 << 4,

    CopyDst = 1 << 5
}
