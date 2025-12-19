using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXFence : GraphicsResource
{
    private readonly EventWaitHandle eventWaitHandle = new(false, EventResetMode.ManualReset);

    public ComPtr<ID3D12Fence> Fence;

    private ulong currentFenceValue;

    public DXFence(DXGraphicsContext context) : base(context)
    {
        context.Device.CreateFence(0, FenceFlags.None, out Fence).Success();
    }

    public void Wait(ComPtr<ID3D12CommandQueue> queue)
    {
        currentFenceValue++;

        queue.Signal(Fence, currentFenceValue).Success();

        if (Fence.GetCompletedValue() < currentFenceValue)
        {
            Fence.SetEventOnCompletion(currentFenceValue, (void*)eventWaitHandle.SafeWaitHandle.DangerousGetHandle()).Success();

            eventWaitHandle.WaitOne();
            eventWaitHandle.Reset();
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Fence.Dispose();

        eventWaitHandle.Dispose();
    }
}
