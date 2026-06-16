using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandQueue(MTLGraphicsContext context, CommandQueueType type, MTL4CommandQueue commandQueue) : CommandQueue(context, type)
{
    public MTL4CommandQueue CommandQueue = commandQueue;

    public MTLSharedEvent Event = context.Device.MakeSharedEvent();

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
        throw new NotImplementedException();
    }

    protected override void SignalImpl(ulong signalValue)
    {
        CommandQueue.SignalEvent(Event, signalValue);
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        throw new NotImplementedException();
    }

    protected override void WaitImpl(ulong waitValue)
    {
        Event.Wait(waitValue, ulong.MaxValue);
    }

    protected override void WaitImpl(ReadOnlySpan<CommandSubmission> submissions)
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

        Event.Dispose();
    }
}
