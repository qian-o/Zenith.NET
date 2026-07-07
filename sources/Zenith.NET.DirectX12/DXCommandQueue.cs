using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandQueue : CommandQueue
{
    private readonly ManualResetEvent @event = new(false);

    public ComPtr<ID3D12CommandQueue> CommandQueue;

    public ComPtr<ID3D12Fence> Fence;

    public DXCommandQueue(DXGraphicsContext context, CommandQueueType type) : base(context, type)
    {
        CommandQueueDesc commandQueueDesc = new() { Type = DXFormats.DirectX12(type) };

        context.Device.CreateCommandQueue(&commandQueueDesc, SilkMarshal.GuidPtrOf<ID3D12CommandQueue>(), (void**)CommandQueue.GetAddressOf()).Success();
        context.Device.CreateFence(0, FenceFlags.None, SilkMarshal.GuidPtrOf<ID3D12Fence>(), (void**)Fence.GetAddressOf()).Success();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override ulong GetCompletedValue()
    {
        return Fence.GetCompletedValue();
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new DXCommandBuffer(Context, this);
    }

    protected override void SignalImpl(ulong signalValue)
    {
        CommandQueue.Signal(Fence, signalValue).Success();
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        CommandQueue.ExecuteCommandLists(1, (ID3D12CommandList**)commandBuffer.DirectX12().CommandList.GetAddressOf());
    }

    protected override void WaitImpl(ulong waitValue)
    {
        Fence.SetEventOnCompletion(waitValue, (void*)@event.SafeWaitHandle.DangerousGetHandle()).Success();

        @event.WaitOne();
        @event.Reset();
    }

    protected override void InsertWaitsImpl(ReadOnlySpan<CommandSubmission> submissions)
    {
        foreach (CommandSubmission submission in submissions)
        {
            if (submission.Queue is null)
            {
                continue;
            }

            CommandQueue.Wait(submission.Queue.DirectX12().Fence, submission.Value).Success();
        }
    }

    protected override void SetResourceName(string name)
    {
        CommandQueue.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        Fence.Dispose();
        CommandQueue.Dispose();

        @event.Dispose();
    }
}
