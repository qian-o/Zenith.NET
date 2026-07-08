using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTimeline : Timeline
{
    private readonly ManualResetEvent @event = new(false);

    public ComPtr<ID3D12Fence> Fence;

    public DXTimeline(DXGraphicsContext context, DXCommandQueue queue) : base(context, queue)
    {
        context.Device.CreateFence(0, FenceFlags.None, SilkMarshal.GuidPtrOf<ID3D12Fence>(), (void**)Fence.GetAddressOf()).Success();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public new DXCommandQueue Queue => (DXCommandQueue)base.Queue;

    public override ulong CompletedValue => Fence.GetCompletedValue();

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SignalImpl(ulong value)
    {
        Queue.CommandQueue.Signal(Fence, value).Success();
    }

    protected override void WaitImpl(ulong value)
    {
        Fence.SetEventOnCompletion(value, (void*)@event.SafeWaitHandle.DangerousGetHandle()).Success();

        @event.WaitOne();
        @event.Reset();
    }

    protected override void SetResourceName(string name)
    {
        Fence.SetName(name).Success();
    }

    protected override void Destroy()
    {
        Fence.Dispose();

        @event.Dispose();
    }
}
