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

internal unsafe class Surface : DisposableObject
{
    private readonly byte[] pixels;

    public Surface(GraphicsContext context, uint width, uint height)
    {
        pixels = new byte[width * height * 4];

        Target = context.CreateTexture(new()
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

        Bitmap = new((int)width, (int)height);

        Context = context;
        Width = width;
        Height = height;
    }

    public Texture Target { get; }

    public WriteableBitmap Bitmap { get; }

    public GraphicsContext Context { get; }

    public uint Width { get; }

    public uint Height { get; }

    public void Present()
    {
        uint rowPitchInBytes = Width * 4;

        fixed (byte* pPixels = pixels)
        {
            Target.Download(default, default, new() { Width = Width, Height = Height, Depth = 1 }, new()
            {
                Pointer = (nint)pPixels,
                Layout = new()
                {
                    SizeInBytes = (uint)pixels.Length,
                    RowPitchInBytes = rowPitchInBytes,
                    SlicePitchInBytes = (uint)pixels.Length
                }
            });
        }

        if (ZenithViewHelper.ColorFormat is PixelFormat.R8G8B8A8UNorm)
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