using Zenith.NET;

namespace SponzaScene.Renderer;

internal class CopyLitColorPass : RenderPass
{
    public CopyLitColorPass() : base("Copy LitColor Pass")
    {
    }

    public override void Resize(uint width, uint height)
    {
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        // Copy current LitColor to LitColorHistory for next frame's SSGI
        commandBuffer.CopyTexture(
            context.LitColor!, 
            default, 
            default, 
            context.LitColorHistory!, 
            default, 
            default, 
            new TextureExtent { Width = context.Width, Height = context.Height, Depth = 1 });
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
