using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSwapChainFrameBuffer(MTLGraphicsContext context, MTLSwapChain swapChain) : GraphicsResource(context)
{
    private MTLTexture? colorTarget;
    private MTLTexture? depthStencilTarget;
    private MTLFrameBuffer? frameBuffer;

    public MTLFrameBuffer Get(uint width, uint height, CAMetalDrawable drawable)
    {
        colorTarget ??= new(context, new()
        {
            Type = TextureType.Texture2D,
            Format = swapChain.Desc.ColorTargetFormat,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget
        }, drawable.Texture);

        depthStencilTarget ??= swapChain.Desc.DepthStencilTargetFormat is not null ? new(context, new()
        {
            Type = TextureType.Texture2D,
            Format = swapChain.Desc.DepthStencilTargetFormat.Value,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.DepthStencil
        }) : null;

        frameBuffer ??= new(context, new()
        {
            ColorAttachments = [new() { Target = colorTarget }],
            DepthStencilAttachment = depthStencilTarget is not null ? new() { Target = depthStencilTarget } : null
        });

        if (frameBuffer.Width != width || frameBuffer.Height != height)
        {
            DestroyFrameBuffer();

            return Get(width, height, drawable);
        }

        frameBuffer.Descriptor.ColorAttachments[0].Texture = colorTarget.Texture = drawable.Texture;

        return frameBuffer;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DestroyFrameBuffer();
    }

    private void DestroyFrameBuffer()
    {
        frameBuffer?.Dispose();
        frameBuffer = null;

        depthStencilTarget?.Dispose();
        depthStencilTarget = null;

        colorTarget?.Dispose();
        colorTarget = null;
    }
}
