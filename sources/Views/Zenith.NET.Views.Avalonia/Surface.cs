using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace Zenith.NET.Views.Avalonia;

internal class Surface(GraphicsContext graphicsContext, uint width, uint height) : DisposableObject
{
    public Texture Drawable { get; } = graphicsContext.CreateTexture(new()
    {
        Type = TextureType.Texture2D,
        Format = ZenithViewHelper.DrawableFormat,
        Width = width,
        Height = height,
        Depth = 1,
        MipLevels = 1,
        ArrayLayers = 1,
        SampleCount = SampleCount.Count1,
        Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment | TextureUsages.CopySrc | TextureUsages.CopyDst
    });

    public WriteableBitmap Bitmap { get; } = new(new((int)width, (int)height), new(96, 96), DrawableFormat(), AlphaFormat.Premul);

    public uint Width { get; } = width;

    public uint Height { get; } = height;

    public void Present()
    {
        using ILockedFramebuffer lockedFramebuffer = Bitmap.Lock();

        Extent3D extent = new()
        {
            Width = Width,
            Height = Height,
            Depth = 1
        };

        TextureData data = new()
        {
            Pointer = lockedFramebuffer.Address,
            SizeInBytes = (uint)(lockedFramebuffer.RowBytes * Height),
            RowStrideInBytes = (uint)lockedFramebuffer.RowBytes,
            SliceStrideInBytes = (uint)(lockedFramebuffer.RowBytes * Height)
        };

        Drawable.Download(default, default, extent, data);
    }

    protected override void Destroy()
    {
        Bitmap.Dispose();
        Drawable.Dispose();
    }

    private static AvaloniaPixelFormat DrawableFormat()
    {
        return ZenithViewHelper.DrawableFormat switch
        {
            PixelFormat.R8G8B8A8UNorm => AvaloniaPixelFormat.Rgba8888,
            PixelFormat.B8G8R8A8UNorm => AvaloniaPixelFormat.Bgra8888,
            _ => default
        };
    }
}
