using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandBuffer : CommandBuffer
{
    private readonly DXDescriptorTable cbvSrvUavTable;
    private readonly DXDescriptorTable samplerTable;

    public ComPtr<ID3D12CommandAllocator> CommandAllocator;

    public ComPtr<ID3D12CommandList> CommandList;

    public ComPtr<ID3D12GraphicsCommandList> GraphicsCommandList;

    public ComPtr<ID3D12GraphicsCommandList4>? GraphicsCommandList4;

    public ComPtr<ID3D12GraphicsCommandList6>? GraphicsCommandList6;

    public DXCommandBuffer(DXGraphicsContext context, DXCommandQueue queue) : base(context, queue)
    {
        cbvSrvUavTable = new(context, DescriptorHeapType.CbvSrvUav, 4096);
        samplerTable = new(context, DescriptorHeapType.Sampler, 2048);

        context.Device.CreateCommandAllocator(DXFormats.DirectX12(queue.Type), out CommandAllocator).Success();

        context.Device.CreateCommandList(0, DXFormats.DirectX12(queue.Type), CommandAllocator, (ComPtr<ID3D12PipelineState>)null, out CommandList).Success();

        CommandList.QueryInterface(out GraphicsCommandList).Success();

        if (CommandList.QueryInterface(out ComPtr<ID3D12GraphicsCommandList4> graphicsCommandList4).IsSuccess())
        {
            GraphicsCommandList4 = graphicsCommandList4;
        }

        if (CommandList.QueryInterface(out ComPtr<ID3D12GraphicsCommandList6> graphicsCommandList6).IsSuccess())
        {
            GraphicsCommandList6 = graphicsCommandList6;
        }
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        throw new NotImplementedException();
    }

    protected override void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        throw new NotImplementedException();
    }

    protected override void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice)
    {
        throw new NotImplementedException();
    }

    protected override BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
    }

    protected override void PreprocessResourceSetsImpl(ResourceSet[] resourceSets)
    {
        throw new NotImplementedException();
    }

    protected override void SetScissorsImpl(Scissor[] scissors)
    {
        throw new NotImplementedException();
    }

    protected override void SetViewportsImpl(Viewport[] viewports)
    {
        throw new NotImplementedException();
    }

    protected override void BindPipelineImpl(GraphicsPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void BindPipelineImpl(ComputePipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void BindPipelineImpl(RayTracingPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void BindPipelineImpl(MeshShadingPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void BindVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void BindIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        throw new NotImplementedException();
    }

    protected override void BindResourceSetImpl(Pipeline pipeline, ResourceSet resourceSet, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        throw new NotImplementedException();
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchRaysImpl(RayTracingPipeline pipeline, uint width, uint height, uint depth)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        throw new NotImplementedException();
    }

    protected override void BeginQueryImpl(QueryHeap queryHeap, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void BeginDebugEventImpl(string label)
    {
        throw new NotImplementedException();
    }

    protected override void EndDebugEventImpl()
    {
        throw new NotImplementedException();
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        throw new NotImplementedException();
    }

    protected override void BeginImpl()
    {
        throw new NotImplementedException();
    }

    protected override void EndImpl()
    {
        throw new NotImplementedException();
    }

    protected override void ResetImpl()
    {
        throw new NotImplementedException();
    }

    protected override void BeginRenderingImpl(FrameBuffer frameBuffer, ClearValue? clearValue)
    {
        throw new NotImplementedException();
    }

    protected override void EndRenderingImpl(FrameBuffer frameBuffer)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        base.Destroy();

        GraphicsCommandList6?.Dispose();
        GraphicsCommandList4?.Dispose();
        GraphicsCommandList.Dispose();

        CommandList.Dispose();

        CommandAllocator.Dispose();

        cbvSrvUavTable.Dispose();
        samplerTable.Dispose();
    }
}
