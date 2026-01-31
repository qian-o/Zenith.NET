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

            Background = new ImageBrush() { ImageSource = (surface = new(GraphicsContext, width, height)).WriteableBitmap };
        }
    }

    void IZenithView.Tick()
    {
        if (surface is null)
        {
            return;
        }

        UpdateRequested?.Invoke(this, new(scheduler.UpdateSeconds, scheduler.TotalSeconds));
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, surface.FrameBuffer));
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
    private readonly Texture color;
    private readonly Texture depthStencil;
    private readonly Buffer pixels;

    public Surface(GraphicsContext context, uint width, uint height)
    {
        color = context.CreateTexture(new()
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

        depthStencil = context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = ZenithViewHelper.DepthStencilFormat,
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
        CommandBuffer commandBuffer = Context.Graphics.CommandBuffer();
        commandBuffer.CopyTextureToBuffer(color, default, default, new() { Width = Width, Height = Height, Depth = 1 }, pixels, 0);
        commandBuffer.Submit(true);

        uint rowPitchInBytes = ZenithHelper.Align(Width * 4, GraphicsContext.TextureRowPitchAlignment);

        using (Stream stream = WriteableBitmap.PixelBuffer.AsStream())
        {
            MappedMemory mappedMemory = pixels.Map();

            byte* pointer = (byte*)mappedMemory.Pointer;

            switch (ZenithViewHelper.ColorFormat)
            {
                case PixelFormat.R8G8B8A8UNorm:
                    {
                        for (uint y = 0; y < Height; y++)
                        {
                            for (uint x = 0; x < Width; x++)
                            {
                                stream.WriteByte(pointer[(x * 4) + 2]);
                                stream.WriteByte(pointer[(x * 4) + 1]);
                                stream.WriteByte(pointer[(x * 4) + 0]);
                                stream.WriteByte(pointer[(x * 4) + 3]);
                            }

                            pointer += rowPitchInBytes;
                        }
                    }
                    break;

                case PixelFormat.B8G8R8A8UNorm:
                    for (uint y = 0; y < Height; y++)
                    {
                        stream.Write([.. new ReadOnlySpan<byte>(pointer, (int)(Width * 4))]);

                        pointer += rowPitchInBytes;
                    }
                    break;

                default:
                    throw new NotSupportedException($"Pixel format {ZenithViewHelper.ColorFormat} is not supported.");
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