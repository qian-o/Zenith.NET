using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSwapChainTargets(MTLGraphicsContext context, MTLSwapChain swapChain) : GraphicsResource(context)
{
    private MTLTexture? colorTarget;
    private MTLTexture? depthStencilTarget;

    public MTLTexture ColorTarget => colorTarget!;

    public MTLTexture? DepthStencilTarget => depthStencilTarget;

    public void EnsureTargets(uint width, uint height, CAMetalDrawable drawable)
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

        if (colorTarget.Desc.Width != width || colorTarget.Desc.Height != height)
        {
            DestroyTargets();

            EnsureTargets(width, height, drawable);

            return;
        }

        colorTarget.Texture = drawable.Texture;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DestroyTargets();
    }

    public void DestroyTargets()
    {
        depthStencilTarget?.Dispose();
        depthStencilTarget = null;

        colorTarget?.Dispose();
        colorTarget = null;
    }
}
