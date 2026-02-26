using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandQueue(MTLGraphicsContext context, CommandQueueType type, MTL4CommandQueue queue) : CommandQueue(context, type)
{
    private readonly MTLFence fence = new(context);

    protected override CommandBuffer CreateCommandBuffer()
    {
        throw new NotImplementedException();
    }

    protected override void WaitIdleImpl()
    {
        fence.Wait(queue);
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        base.Destroy();

        fence.Dispose();
    }
}
