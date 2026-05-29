using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandQueue : CommandQueue
{
    private readonly ManualResetEvent @event = new(false);

    public ComPtr<ID3D12CommandQueue> CommandQueue;

    public ComPtr<ID3D12Fence1> Fence1;

    public DXCommandQueue(DXGraphicsContext context, CommandQueueType type, ComPtr<ID3D12CommandQueue> commandQueue) : base(context, type)
    {
        CommandQueue = commandQueue;

        context.Device14.CreateFence(0, FenceFlags.None, SilkMarshal.GuidPtrOf<ID3D12Fence1>(), (void**)Fence1.GetAddressOf()).Success();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

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
        return new DXCommandBuffer(Context, this);
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue)
    {
        foreach (CommandSubmission wait in waits)
        {
            CommandQueue.Wait(wait.Queue.DirectX12().Fence1, wait.Value).Success();
        }

        CommandQueue.ExecuteCommandLists(1, commandBuffer.DirectX12().CommandList.GetAddressOf());
        CommandQueue.Signal(Fence1, signalValue).Success();
    }

    protected override void WaitImpl(ulong waitValue)
    {
        Fence1.SetEventOnCompletion(waitValue, (void*)@event.SafeWaitHandle.DangerousGetHandle()).Success();

        @event.WaitOne();
        @event.Reset();
    }

    protected override void SetResourceName(string name)
    {
        CommandQueue.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        Fence1.Dispose();

        @event.Dispose();
    }
}
