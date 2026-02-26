using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLFence(MTLGraphicsContext context) : GraphicsResource(context)
{
    private readonly MTLSharedEvent @event = context.Device.NewSharedEvent();

    private ulong currentFenceValue;

    public void Wait(MTL4CommandQueue queue)
    {
        currentFenceValue++;

        queue.SignalEvent(@event, currentFenceValue);

        if (@event.SignaledValue < currentFenceValue)
        {
            @event.WaitUntilSignaledValue(currentFenceValue, ulong.MaxValue);
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        @event.Dispose();
    }
}
