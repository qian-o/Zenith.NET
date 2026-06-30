#if !WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Zenith.NET.Views.WinUI;

public partial class ZenithView
{
    private Surface? surface;

    void IZenithView.EnsureResources()
    {
        if (GraphicsContext is null)
        {
            return;
        }

        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (surface is null || surface.Width != width || surface.Height != height)
        {
            ((IZenithView)this).ReleaseResources();

            Background = new ImageBrush() { ImageSource = (surface = new(GraphicsContext, width, height)).Bitmap };
        }
    }

    void IZenithView.Tick()
    {
        if (surface is null)
        {
            return;
        }

        UpdateRequested?.Invoke(this, new(scheduler.UpdateSeconds, scheduler.TotalSeconds));
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, surface.Drawable));
    }

    void IZenithView.Present()
    {
        surface?.Present();
    }

    void IZenithView.ReleaseResources()
    {
        surface?.Dispose();
        surface = null;
    }
}

internal unsafe class Surface(GraphicsContext context, uint width, uint height) : DisposableObject
{
    private readonly byte[] pixels = new byte[width * height * 4];

    public Texture Drawable { get; } = context.CreateTexture(new()
    {
        Type = TextureType.Texture2D,
        Format = ZenithViewHelper.DrawableFormat,
        Width = width,
        Height = height,
        Depth = 1,
        MipLevels = 1,
        ArrayLayers = 1,
        SampleCount = SampleCount.Count1,
        Usages = TextureUsages.ColorAttachment | TextureUsages.TransferDst
    });

    public WriteableBitmap Bitmap { get; } = new((int)width, (int)height);

    public uint Width { get; } = width;

    public uint Height { get; } = height;

    public void Present()
    {
        fixed (byte* pPixels = pixels)
        {
            Extent3D extent = new()
            {
                Width = Width,
                Height = Height,
                Depth = 1
            };

            TextureData data = new()
            {
                Pointer = (nint)pPixels,
                SizeInBytes = (uint)pixels.Length,
                RowStrideInBytes = Width * 4,
                SliceStrideInBytes = (uint)pixels.Length
            };

            Drawable.Download(default, default, extent, data);
        }

        if (ZenithViewHelper.DrawableFormat is PixelFormat.R8G8B8A8UNorm)
        {
            for (int i = 0; i < pixels.Length; i += 4)
            {
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            }
        }

        using Stream stream = Bitmap.PixelBuffer.AsStream();
        stream.Write(pixels);

        Bitmap.Invalidate();
    }

    protected override void Destroy()
    {
        Bitmap.Dispose();
        Drawable.Dispose();
    }
}
#endif