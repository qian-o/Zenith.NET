using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandEncoder(MTLGraphicsContext context, MTL4CommandBuffer commandBuffer) : GraphicsResource(context)
{
    private MTL4RenderCommandEncoder? render;
    private MTL4ComputeCommandEncoder? compute;

    public MTL4RenderCommandEncoder Render => EnsureRender();

    public MTL4ComputeCommandEncoder Compute => EnsureCompute();

    public void BeginDebugEvent(string label)
    {
        render?.PushDebugGroup(label);
        compute?.PushDebugGroup(label);
    }

    public void EndDebugEvent()
    {
        render?.PopDebugGroup();
        compute?.PopDebugGroup();
    }

    public void InsertDebugMarker(string label)
    {
        render?.InsertDebugSignpost(label);
        compute?.InsertDebugSignpost(label);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }

    private MTL4RenderCommandEncoder EnsureRender()
    {
        throw new NotImplementedException();
    }

    private MTL4ComputeCommandEncoder EnsureCompute()
    {
        throw new NotImplementedException();
    }
}
