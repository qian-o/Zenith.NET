using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace Zenith.NET.Views.Avalonia;

internal class Surface(GraphicsContext graphicsContext, uint width, uint height) : DisposableObject
{
    public Texture Target { get; } = graphicsContext.CreateTexture(TextureDesc.Texture2D(ZenithViewHelper.ColorFormat, width, height, 1, SampleCount.Count1));

    public WriteableBitmap Bitmap { get; } = new(new((int)width, (int)height), new(96, 96), ColorFormat(), AlphaFormat.Premul);

    public uint Width { get; } = width;

    public uint Height { get; } = height;

    public void Present()
    {
        using ILockedFramebuffer lockedFramebuffer = Bitmap.Lock();

        Target.Download(default, default, new() { Width = Width, Height = Height, Depth = 1 }, new()
        {
            Pointer = lockedFramebuffer.Address,
            Layout = new()
            {
                SizeInBytes = (uint)(lockedFramebuffer.RowBytes * Height),
                RowStrideInBytes = (uint)lockedFramebuffer.RowBytes,
                SliceStrideInBytes = (uint)(lockedFramebuffer.RowBytes * Height)
            }
        });
    }

    protected override void Destroy()
    {
        Bitmap.Dispose();
        Target.Dispose();
    }

    private static AvaloniaPixelFormat ColorFormat()
    {
        return ZenithViewHelper.ColorFormat switch
        {
            PixelFormat.R8G8B8A8UNorm => AvaloniaPixelFormat.Rgba8888,
            PixelFormat.B8G8R8A8UNorm => AvaloniaPixelFormat.Bgra8888,
            _ => throw new NotSupportedException($"Pixel format {ZenithViewHelper.ColorFormat} is not supported.")
        };
    }
}
