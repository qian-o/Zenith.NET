using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandQueue(DXGraphicsContext context, CommandQueueType type, ComPtr<ID3D12CommandQueue> queue) : CommandQueue(context, type)
{
    private readonly DXFence fence = new(context);

    protected override CommandBuffer CreateCommandBuffer()
    {
        return new DXCommandBuffer(context, this);
    }

    protected override void WaitIdleImpl()
    {
        fence.Wait(queue);
    }

    protected override void SubmitImpl(CommandBuffer commandBuffer)
    {
        queue.ExecuteCommandLists(1, commandBuffer.DirectX12().CommandList.GetAddressOf());
    }

    protected override void SetResourceName(string name)
    {
        queue.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        fence.Dispose();
    }
}
