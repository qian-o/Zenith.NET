using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandBuffer : CommandBuffer
{
    public MTL4CommandAllocator CommandAllocator;

    public MTL4CommandBuffer CommandBuffer;

    public MTLCommandBuffer(MTLGraphicsContext context, CommandQueue queue) : base(context, queue)
    {
        CommandAllocator = context.Device.MakeCommandAllocator();
        CommandBuffer = context.Device.MakeCommandBuffer();
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        throw new NotImplementedException();
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent)
    {
        throw new NotImplementedException();
    }

    protected override void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        throw new NotImplementedException();
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, TextureExtent srcExtent, Buffer dest, uint destOffsetInBytes)
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

    protected override void BeginRenderPassImpl(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        throw new NotImplementedException();
    }

    protected override void EndRenderPassImpl(FrameBuffer frameBuffer)
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

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceTableImpl(Pipeline pipeline, ResourceTable resourceTable)
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

    protected override void SetResourceName(string name)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        base.Destroy();

        throw new NotImplementedException();
    }
}
