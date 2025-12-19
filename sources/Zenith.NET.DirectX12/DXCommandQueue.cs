using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXCommandQueue(DXGraphicsContext context, CommandQueueType type, ComPtr<ID3D12CommandQueue> queue) : CommandQueue(context, type)
{
    protected override CommandBuffer CreateCommandBuffer()
    {
        throw new NotImplementedException();
    }

    protected override void WaitIdleImpl()
    {
        throw new NotImplementedException();
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        queue.SetName(name).Success();
    }
}
