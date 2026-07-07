using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandQueue : CommandQueue
{
    public MTL4CommandQueue CommandQueue;

    public MTLSharedEvent Event;

    public MTLCommandQueue(MTLGraphicsContext context, CommandQueueType type) : base(context, type)
    {
        CommandQueue = context.Device.MakeMTL4CommandQueue();
        Event = context.Device.MakeSharedEvent();

        CommandQueue.AddResidencySet(context.ResidencySet);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override ulong GetCompletedValue()
    {
        return Event.SignaledValue;
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new MTLCommandBuffer(Context, this);
    }

    protected override void SignalImpl(ulong signalValue)
    {
        CommandQueue.SignalEvent(Event, signalValue);
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        CommandQueue.Commit([commandBuffer.Metal().CommandBuffer]);
    }

    protected override void WaitImpl(ulong waitValue)
    {
        Event.Wait(waitValue, ulong.MaxValue);
    }

    protected override void InsertWaitsImpl(ReadOnlySpan<CommandSubmission> submissions)
    {
        foreach (CommandSubmission submission in submissions)
        {
            if (submission.Queue is null)
            {
                continue;
            }

            CommandQueue.WaitForEvent(submission.Queue.Metal().Event, submission.Value);
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        base.Destroy();

        CommandQueue.RemoveResidencySet(Context.ResidencySet);

        Event.Dispose();
        CommandQueue.Dispose();
    }
}
