using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandQueue : CommandQueue
{
    public ComPtr<ID3D12CommandQueue> CommandQueue;

    public ComPtr<ID3D12Fence1> Fence1;

    public DXCommandQueue(DXGraphicsContext context, CommandQueueType type, ComPtr<ID3D12CommandQueue> commandQueue) : base(context, type)
    {
        CommandQueue = commandQueue;

        context.Device14.CreateFence(0, FenceFlags.None, SilkMarshal.GuidPtrOf<ID3D12Fence1>(), (void**)Fence1.GetAddressOf()).Success();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override ulong GetCompletedValue()
    {
        return Fence1.GetCompletedValue();
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        throw new NotImplementedException();
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue)
    {
        throw new NotImplementedException();
    }

    protected override void WaitImpl(ulong waitValue)
    {
        if (Fence1.GetCompletedValue() < waitValue)
        {
            Fence1.SetEventOnCompletion(waitValue, default).Success();
        }
    }

    protected override void SetResourceName(string name)
    {
        CommandQueue.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        Fence1.Dispose();
    }
}
