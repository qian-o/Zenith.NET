using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandEncoder(MTLGraphicsContext context, MTL4CommandBuffer commandBuffer) : GraphicsResource(context)
{
    private Scissor[]? todoScissors;
    private Viewport[]? todoViewports;

    public MTL4RenderCommandEncoder? Render { get; private set; }

    public MTL4ComputeCommandEncoder? Compute { get; private set; }

    public void Begin()
    {
        Compute = commandBuffer.MakeComputeCommandEncoder();
    }

    public void End()
    {
        EndRender();
        EndCompute();
    }

    public void BeginRenderPass(MTL4RenderPassDescriptor descriptor)
    {
        EndCompute();

        Render = commandBuffer.MakeRenderCommandEncoder(descriptor);

        if (todoScissors is not null)
        {
            SetScissors(todoScissors);

            todoScissors = null;
        }

        if (todoViewports is not null)
        {
            SetViewports(todoViewports);

            todoViewports = null;
        }
    }

    public void EndRenderPass()
    {
        EndRender();

        Compute = commandBuffer.MakeComputeCommandEncoder();
    }

    public void SetScissors(Scissor[] scissors)
    {
        if (Render is null)
        {
            todoScissors = [.. scissors];
        }
        else
        {
            MTLScissorRect[] mtlScissors = [.. scissors.Select(static item => new MTLScissorRect((uint)item.X, (uint)item.Y, item.Width, item.Height))];

            Render.SetScissorRects(mtlScissors);
        }
    }

    public void SetViewports(Viewport[] viewports)
    {
        if (Render is null)
        {
            todoViewports = [.. viewports];
        }
        else
        {
            MTLViewport[] mtlViewports = [.. viewports.Select(static item => new MTLViewport(item.X, item.Y, item.Width, item.Height, item.MinDepth, item.MaxDepth))];

            Render.SetViewports(mtlViewports);
        }
    }

    public void BeginDebugEvent(string label)
    {
        Render?.PushDebugGroup(label);
        Compute?.PushDebugGroup(label);
    }

    public void EndDebugEvent()
    {
        Render?.PopDebugGroup();
        Compute?.PopDebugGroup();
    }

    public void InsertDebugMarker(string label)
    {
        Render?.InsertDebugSignpost(label);
        Compute?.InsertDebugSignpost(label);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Render?.Dispose();
        Render = null;

        Compute?.Dispose();
        Compute = null;
    }

    private void EndRender()
    {
        Render?.EndEncoding();
        Render?.Dispose();
        Render = null;
    }

    private void EndCompute()
    {
        Compute?.EndEncoding();
        Compute?.Dispose();
        Compute = null;
    }
}
