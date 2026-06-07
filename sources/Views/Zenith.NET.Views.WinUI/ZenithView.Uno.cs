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
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, surface.Target));
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

    public Texture Target { get; } = context.CreateTexture(TextureDesc.Texture2D(ZenithViewHelper.Format, width, height, 1, SampleCount.Count1));

    public WriteableBitmap Bitmap { get; } = new((int)width, (int)height);

    public uint Width { get; } = width;

    public uint Height { get; } = height;

    public void Present()
    {
        fixed (byte* pPixels = pixels)
        {
            Target.Download(default, default, new() { Width = Width, Height = Height, Depth = 1 }, new()
            {
                Pointer = (nint)pPixels,
                SizeInBytes = (uint)pixels.Length,
                RowStrideInBytes = Width * 4,
                SliceStrideInBytes = (uint)pixels.Length
            });
        }

        if (ZenithViewHelper.Format is PixelFormat.R8G8B8A8UNorm)
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
        Target.Dispose();
    }
}
#endif