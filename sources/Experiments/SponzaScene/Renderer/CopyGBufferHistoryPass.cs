using Zenith.NET;

namespace SponzaScene.Renderer;

/// <summary>
/// Copies current frame's G-Buffer data to history textures for next frame's temporal effects
/// </summary>
internal class CopyGBufferHistoryPass : RenderPass
{
    public CopyGBufferHistoryPass() : base("Copy GBuffer History Pass")
    {
    }

    public override void Resize(uint width, uint height)
    {
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        TextureExtent extent = new() { Width = context.Width, Height = context.Height, Depth = 1 };

        // Copy Position to PositionHistory
        commandBuffer.CopyTexture(
            context.Position!,
            default,
            default,
            context.PositionHistory!,
            default,
            default,
            extent);

        // Copy Normal to NormalHistory
        commandBuffer.CopyTexture(
            context.Normal!,
            default,
            default,
            context.NormalHistory!,
            default,
            default,
            extent);

        // Store current view projection for next frame
        context.PrevViewProjection = context.View * context.Projection;
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        // No UI needed for this pass
    }

    protected override void Destroy()
    {
        base.Destroy();
    }
}
