#if !WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.Core;

namespace Zenith.NET.Views.WinUI;

public unsafe partial class ZenithView
{
    private Surface? surface;

    private void Frame()
    {
        EnsureResources();

        if (surface is null)
        {
            return;
        }

        UpdateRequested?.Invoke(this, new(dispatcher.UpdateSeconds, dispatcher.TotalSeconds));
        RenderRequested?.Invoke(this, new(dispatcher.RenderSeconds, dispatcher.TotalSeconds, surface.FrameBuffer));
    }

    private void Present()
    {
        Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => surface?.Present()).GetResults();
    }

    private void EnsureResources()
    {
        Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
            if (GraphicsContext is null)
            {
                return;
            }

            uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
            uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

            if (surface is null || surface.Width != width || surface.Height != height)
            {
                DestroyResources();

                Background = new ImageBrush() { ImageSource = (surface = new(GraphicsContext, width, height)).WriteableBitmap };
            }
        }).GetResults();
    }

    private void DestroyResources()
    {
        surface?.Dispose();
        surface = null;
    }
}

internal unsafe class Surface : DisposableObject
{
    private readonly Texture color;
    private readonly Texture depthStencil;
    private readonly Buffer pixels;

    public Surface(GraphicsContext context, uint width, uint height)
    {
        color = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.B8G8R8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget
        });

        depthStencil = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.D24UNormS8UInt,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.DepthStencil
        });

        pixels = context.CreateBuffer(new()
        {
            SizeInBytes = ZenithHelper.Align(width * 4, GraphicsContext.TextureRowPitchAlignment) * height,
            StrideInBytes = 4,
            Flags = BufferUsageFlags.MapRead
        });

        FrameBuffer = context.CreateFrameBuffer(new()
        {
            ColorAttachments = [new() { Target = color }],
            DepthStencilAttachment = new() { Target = depthStencil }
        });

        WriteableBitmap = new((int)width, (int)height);

        Context = context;
        Width = width;
        Height = height;
    }

    public FrameBuffer FrameBuffer { get; }

    public WriteableBitmap WriteableBitmap { get; }

    public GraphicsContext Context { get; }

    public uint Width { get; }

    public uint Height { get; }

    public void Present()
    {
        uint rowPitchInBytes = ZenithHelper.Align(Width * 4, GraphicsContext.TextureRowPitchAlignment);

        CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();
        commandBuffer.CopyTextureToBuffer(color, default, default, new() { Width = Width, Height = Height, Depth = 1 }, pixels, 0);
        commandBuffer.Submit(true);

        using (Stream stream = WriteableBitmap.PixelBuffer.AsStream())
        {
            MappedMemory mappedMemory = pixels.Map();

            byte* pointer = (byte*)mappedMemory.Pointer;

            for (uint y = 0; y < WriteableBitmap.PixelHeight; y++)
            {
                stream.Write([.. new ReadOnlySpan<byte>(pointer, WriteableBitmap.PixelHeight * 4)]);

                pointer += rowPitchInBytes;
            }

            pixels.Unmap();
        }

        WriteableBitmap.Invalidate();
    }

    protected override void Destroy()
    {
        WriteableBitmap.Dispose();
        FrameBuffer.Dispose();

        pixels.Dispose();
        depthStencil.Dispose();
        color.Dispose();
    }
}
#endif