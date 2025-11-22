#if !WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Zenith.NET.Views.WinUI;

public unsafe partial class ZenithView
{
    private Texture? color;
    private Texture? depthStencil;
    private FrameBuffer? frameBuffer;
    private Texture? present;
    private WriteableBitmap? bitmap;

    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    private void OnRender(GraphicsContext graphicsContext)
    {
        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (color is null || depthStencil is null || frameBuffer is null || frameBuffer.Width != width || frameBuffer.Height != height || present is null || bitmap is null)
        {
            Destroy();

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

            frameBuffer = graphicsContext.CreateFrameBuffer(new()
            {
                ColorAttachments = [new() { Target = color }],
                DepthStencilAttachment = new() { Target = depthStencil }
            });

            present = graphicsContext.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R8G8B8A8UNorm,
                Width = width,
                Height = height,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.Dynamic
            });

            bitmap = new((int)width, (int)height);
        }

        UpdateRequested?.Invoke(this, new(updateStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds));
        updateStopwatch.Restart();

        RenderRequested?.Invoke(this, new(renderStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds, frameBuffer));
        renderStopwatch.Restart();

        CommandBuffer commandBuffer = graphicsContext.Graphics.CommandBuffer();
        commandBuffer.CopyTexture(color, default, default, present, default, default, new() { Width = width, Height = height, Depth = 1 });
        commandBuffer.Submit();

        graphicsContext.Graphics.WaitIdle();

        using (Stream stream = bitmap.PixelBuffer.AsStream())
        {
            MappedMemory mappedMemory = present.Map(default);

            int rowBytes = (int)(width * 4);

            byte* pixels = (byte*)mappedMemory.Pointer;

            for (uint y = 0; y < height; y++)
            {
                stream.Write([.. new ReadOnlySpan<byte>(pixels, rowBytes)], (int)(rowBytes * y), rowBytes);

                pixels += mappedMemory.RowPitch;
            }

            stream.Flush();

            present.Unmap();
        }

        bitmap.Invalidate();
    }

    private void Destroy()
    {
        bitmap?.Dispose();
        bitmap = null;

        present?.Dispose();
        present = null;

        frameBuffer?.Dispose();
        frameBuffer = null;

        color?.Dispose();
        color = null;

        depthStencil?.Dispose();
        depthStencil = null;
    }
}
#endif