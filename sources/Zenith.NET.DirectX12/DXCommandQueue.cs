using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXCommandQueue(DXGraphicsContext context, CommandQueueType type, ComPtr<ID3D12CommandQueue> commandQueue) : CommandQueue(context, type)
{
    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override ulong GetCompletedValue()
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        commandQueue.SetName(name).Success();
    }
}
