using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLTimeline(MTLGraphicsContext context, MTLCommandQueue queue) : Timeline(context, queue)
{
    public MTLSharedEvent Event = context.Device.MakeSharedEvent();

    public override nint GetNativeObject(NativeObjectType type)
    {
        return type switch
        {
            NativeObjectType.MTLSharedEvent => Event.NativePtr,
            _ => default
        };
    }

    protected override ulong GetCompletedValue()
    {
        return Event.SignaledValue;
    }

    protected override void SignalImpl(ulong value)
    {
        queue.CommandQueue.SignalEvent(Event, value);
    }

    protected override void WaitImpl(ulong value)
    {
        Event.Wait(value, ulong.MaxValue);
    }

    protected override void SetResourceName(string name)
    {
        Event.Label = name;
    }

    protected override void Destroy()
    {
        Event.Dispose();
    }
}
