using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandQueue : CommandQueue
{
    public MTL4CommandQueue CommandQueue;

    public MTLCommandQueue(MTLGraphicsContext context, CommandQueueType type) : base(context, type)
    {
        CommandQueue = context.Device.MakeMTL4CommandQueue();
        CommandQueue.AddResidencySet(context.ResidencySet);

        Timeline = new MTLTimeline(context, this);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override Timeline Timeline { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new MTLCommandBuffer(Context, this);
    }

    protected override void SubmitImpl(ReadOnlySpan<TimelineValue> waits, CommandBuffer commandBuffer)
    {
        foreach (TimelineValue wait in waits)
        {
            CommandQueue.WaitForEvent(wait.Timeline.Metal().Event, wait.Value);
        }

        CommandQueue.Commit([commandBuffer.Metal().CommandBuffer]);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        base.Destroy();

        CommandQueue.RemoveResidencySet(Context.ResidencySet);
        CommandQueue.Dispose();
    }
}
