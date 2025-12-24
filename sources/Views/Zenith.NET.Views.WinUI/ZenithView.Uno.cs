#if !WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Zenith.NET.Views.WinUI;

public unsafe partial class ZenithView
{
    private Texture? color;
    private Texture? depthStencil;
    private FrameBuffer? frameBuffer;
    private Buffer? present;
    private WriteableBitmap? bitmap;
    private uint rowPitchInBytes;

    private void OnRender(GraphicsContext context)
    {
        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (color is null || depthStencil is null || frameBuffer is null || frameBuffer.Width != width || frameBuffer.Height != height || present is null || bitmap is null)
        {
            Destroy();

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

            frameBuffer = context.CreateFrameBuffer(new()
            {
                ColorAttachments = [new() { Target = color }],
                DepthStencilAttachment = new() { Target = depthStencil }
            });

            present = context.CreateBuffer(new()
            {
                SizeInBytes = (rowPitchInBytes = ZenithHelper.Align(width * 4, GraphicsContext.TextureRowPitchAlignment)) * height,
                StrideInBytes = 4,
                Flags = BufferUsageFlags.CopyDestination | BufferUsageFlags.MapRead
            });

            Background = new ImageBrush() { ImageSource = bitmap = new((int)width, (int)height) };
        }

        UpdateRequested?.Invoke(this, new(timer.GetAndRestartUpdate(), timer.TotalSeconds));
        RenderRequested?.Invoke(this, new(timer.GetAndRestartRender(), timer.TotalSeconds, frameBuffer));

        CommandBuffer commandBuffer = context.Graphics.CommandBuffer();
        commandBuffer.CopyTextureToBuffer(color, default, default, new() { Width = width, Height = height, Depth = 1 }, present, 0);
        commandBuffer.Submit();

        context.Graphics.WaitIdle();

        using (Stream stream = bitmap.PixelBuffer.AsStream())
        {
            MappedMemory mappedMemory = present.Map();

            byte* pixels = (byte*)mappedMemory.Pointer;

            for (uint y = 0; y < height; y++)
            {
                stream.Write([.. new ReadOnlySpan<byte>(pixels, (int)(width * 4))]);

                pixels += rowPitchInBytes;
            }

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

        depthStencil?.Dispose();
        depthStencil = null;

        color?.Dispose();
        color = null;
    }
}
#endif