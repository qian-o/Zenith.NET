namespace Zenith.NET;

internal unsafe class VKCommandBuffer : CommandBuffer
{
    public VKCommandBuffer(VKGraphicsContext context, VKCommandQueue queue) : base(context, queue)
    {
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
    }

    protected override void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
    }

    protected override void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice)
    {
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
    }

    protected override void BindFrameBufferImpl(FrameBuffer frameBuffer, ClearValue clearValue)
    {
    }

    protected override void BindPipelineImpl(GraphicsPipeline pipeline)
    {
    }

    protected override void BindPipelineImpl(ComputePipeline pipeline)
    {
    }

    protected override void BindPipelineImpl(RayTracingPipeline pipeline)
    {
    }

    protected override void BindPipelineImpl(MeshShadingPipeline pipeline)
    {
    }

    protected override void BindResourceSetsImpl(ResourceSet[] sets)
    {
    }

    protected override void BindVertexBuffersImpl(Buffer[] buffers, uint[] offsetsInBytes)
    {
    }

    protected override void BindIndexBufferImpl(Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
    }

    protected override void SetScissorsImpl(Scissor[] scissors)
    {
    }

    protected override void SetViewportsImpl(Viewport[] viewports)
    {
    }

    protected override void DrawImpl(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
    }

    protected override void DrawIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
    }

    protected override void DrawIndexedImpl(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
    }

    protected override void DrawIndexedIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
    }

    protected override void DispatchImpl(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
    }

    protected override void DispatchIndirectImpl(Buffer indirectBuffer, uint offsetInBytes)
    {
    }

    protected override void DispatchRaysImpl(uint width, uint height, uint depth)
    {
    }

    protected override void DispatchMeshImpl(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
    }

    protected override void DispatchMeshIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
    }

    protected override void BeginQueryImpl(QueryHeap queryHeap, uint index)
    {
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
    }

    protected override void BeginDebugEventImpl(string label)
    {
    }

    protected override void EndDebugEventImpl()
    {
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
    }

    protected override void BeginImpl()
    {
    }

    protected override void EndImpl()
    {
    }

    protected override void ResetImpl()
    {
    }

    protected override void BeginRenderingImpl()
    {
    }

    protected override void EndRenderingImpl()
    {
    }

    protected override void SetResourceName(string name)
    {
    }
}
