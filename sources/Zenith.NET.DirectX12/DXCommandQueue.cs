using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandQueue : CommandQueue
{
    public ComPtr<ID3D12CommandQueue> CommandQueue;

    public DXCommandQueue(DXGraphicsContext context, CommandQueueType type) : base(context, type)
    {
        CommandQueueDesc commandQueueDesc = new() { Type = DXFormats.DirectX12(type) };

        context.Device.CreateCommandQueue(&commandQueueDesc, SilkMarshal.GuidPtrOf<ID3D12CommandQueue>(), (void**)CommandQueue.GetAddressOf()).Success();

        Timeline = new DXTimeline(context, this);
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override Timeline Timeline { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return type switch
        {
            NativeObjectType.D3D12CommandQueue => (nint)CommandQueue.Handle,
            _ => default
        };
    }

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new DXCommandBuffer(Context, this);
    }

    protected override double GetTimestampPeriod(out uint validBits)
    {
        validBits = 64;

        ulong frequency = 0;
        CommandQueue.GetTimestampFrequency(&frequency).Success();

        return 1_000_000_000.0 / frequency;
    }

    protected override void SubmitImpl(ReadOnlySpan<TimelineValue> waits, CommandBuffer commandBuffer)
    {
        foreach (TimelineValue wait in waits)
        {
            CommandQueue.Wait(wait.Timeline.DirectX12().Fence, wait.Value).Success();
        }

        CommandQueue.ExecuteCommandLists(1, (ID3D12CommandList**)commandBuffer.DirectX12().CommandList.GetAddressOf());
    }

    protected override void SetResourceName(string name)
    {
        CommandQueue.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        CommandQueue.Dispose();
    }
}
