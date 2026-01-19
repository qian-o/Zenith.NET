using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace Zenith.NET.Views.Avalonia;

internal unsafe class Surface : DisposableObject
{
    private readonly Texture color;
    private readonly Texture depthStencil;
    private readonly Buffer pixels;

    public Surface(GraphicsContext graphicsContext, uint width, uint height)
    {
        color = graphicsContext.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget
        });

        depthStencil = graphicsContext.CreateTexture(new()
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

        pixels = graphicsContext.CreateBuffer(new()
        {
            SizeInBytes = ZenithHelper.Align(width * 4, GraphicsContext.TextureRowPitchAlignment) * height,
            StrideInBytes = 4,
            Flags = BufferUsageFlags.MapRead
        });

        FrameBuffer = graphicsContext.CreateFrameBuffer(new()
        {
            ColorAttachments = [new() { Target = color }],
            DepthStencilAttachment = new() { Target = depthStencil }
        });

        WriteableBitmap = new(new((int)width, (int)height), new(96, 96), AvaloniaPixelFormat.Rgba8888, AlphaFormat.Premul);

        GraphicsContext = graphicsContext;
        Width = width;
        Height = height;
    }

    public FrameBuffer FrameBuffer { get; }

    public WriteableBitmap WriteableBitmap { get; }

    public GraphicsContext GraphicsContext { get; }

    public uint Width { get; }

    public uint Height { get; }

    public void Present()
    {
        CommandBuffer commandBuffer = GraphicsContext.Copy.CommandBuffer();
        commandBuffer.CopyTextureToBuffer(color, default, default, new() { Width = Width, Height = Height, Depth = 1 }, pixels, 0);
        commandBuffer.Submit(true);

        uint rowPitchInBytes = ZenithHelper.Align(Width * 4, GraphicsContext.TextureRowPitchAlignment);

        using ILockedFramebuffer lockedFramebuffer = WriteableBitmap.Lock();

        MappedMemory mappedMemory = pixels.Map();

        if (lockedFramebuffer.RowBytes == rowPitchInBytes)
        {
            Unsafe.CopyBlock((void*)lockedFramebuffer.Address, (void*)mappedMemory.Pointer, mappedMemory.SizeInBytes);
        }
        else
        {
            Parallel.For(0, Height, y =>
            {
                byte* srcPtr = (byte*)mappedMemory.Pointer + (rowPitchInBytes * y);
                byte* dstPtr = (byte*)lockedFramebuffer.Address + (lockedFramebuffer.RowBytes * y);

                Unsafe.CopyBlock(dstPtr, srcPtr, (uint)lockedFramebuffer.RowBytes);
            });
        }

        pixels.Unmap();
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
