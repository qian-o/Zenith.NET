using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandEncoder(MTLGraphicsContext context, MTL4CommandBuffer commandBuffer) : GraphicsResource(context)
{
    private MTL4RenderCommandEncoder? render;
    private MTL4ComputeCommandEncoder? compute;

    public MTL4RenderCommandEncoder Render => AcquireRender();

    public MTL4ComputeCommandEncoder Compute => AcquireCompute();

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

    private MTL4RenderCommandEncoder AcquireRender()
    {
        ReleaseCompute();

        throw new NotImplementedException();
    }

    private MTL4ComputeCommandEncoder AcquireCompute()
    {
        ReleaseRender();

        return compute = commandBuffer.MakeComputeCommandEncoder();
    }

    private void ReleaseRender()
    {
        render?.EndEncoding();
        render?.Dispose();
        render = null;
    }

    private void ReleaseCompute()
    {
        compute?.EndEncoding();
        compute?.Dispose();
        compute = null;
    }
}
