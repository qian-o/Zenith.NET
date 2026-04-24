using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace Zenith.NET.Views.Avalonia;

internal class Surface : DisposableObject
{
    public Surface(GraphicsContext graphicsContext, uint width, uint height)
    {
        Target = graphicsContext.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = ZenithViewHelper.ColorFormat,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget
        });

        Bitmap = new(new((int)width, (int)height), new(96, 96), ColorFormat(), AlphaFormat.Premul);

        GraphicsContext = graphicsContext;
        Width = width;
        Height = height;
    }

    public Texture Target { get; }

    public WriteableBitmap Bitmap { get; }

    public GraphicsContext GraphicsContext { get; }

    public uint Width { get; }

    public uint Height { get; }

    public void Present()
    {
        using ILockedFramebuffer lockedFramebuffer = Bitmap.Lock();

        Target.Download(default, default, new() { Width = Width, Height = Height, Depth = 1 }, new()
        {
            Pointer = lockedFramebuffer.Address,
            Layout = new()
            {
                SizeInBytes = (uint)(lockedFramebuffer.RowBytes * Height),
                RowPitchInBytes = (uint)lockedFramebuffer.RowBytes,
                SlicePitchInBytes = (uint)(lockedFramebuffer.RowBytes * Height)
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
