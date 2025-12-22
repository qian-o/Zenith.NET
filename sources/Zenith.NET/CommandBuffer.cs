using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private bool isRendering;
    private FrameBuffer? currentFrameBuffer;
    private ClearValue? currentClearValue;
    private Pipeline? currentPipeline;

    public void Submit()
    {
        queue.Submit(this);
    }

    public void Upload<T>(Buffer buffer, uint offsetInBytes, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.Length is 0)
        {
            return;
        }

        uint sizeInBytes = (uint)(Unsafe.SizeOf<T>() * data.Length);

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyBuffer(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public void Upload<T>(Texture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (data.Length is 0 || data.Length != extent.Width * extent.Height * extent.Depth)
        {
            return;
        }

        uint sliceSizeInTexels = extent.Width * extent.Height;
        uint sliceSizeInBytes = (uint)(Unsafe.SizeOf<T>() * sliceSizeInTexels);

        TextureExtent sliceExtent = extent with { Depth = 1 };

        for (uint i = 0; i < extent.Depth; i++)
        {
            Buffer temporary = Context.Uploader.Buffer(this, sliceSizeInBytes);
            temporary.Upload(data.Slice((int)(i * sliceSizeInTexels), (int)sliceSizeInTexels), 0);

            CopyBufferToTexture(temporary, 0, texture, slice, offset, sliceExtent);

            offset.Z++;
        }
    }

    public void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        EnsureRenderingEnded();

        CopyBufferImpl(src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);
    }

    public void CopyBufferToTexture(Buffer src, uint srcOffsetInBytes, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent)
    {
        EnsureRenderingEnded();

        CopyBufferToTextureImpl(src, srcOffsetInBytes, dest, destSlice, destOffset, destExtent);
    }

    public void CopyTexture(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        EnsureRenderingEnded();

        CopyTextureImpl(src, srcSlice, srcOffset, dest, destSlice, destOffset, extent);
    }

    public void CopyTextureToBuffer(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, TextureExtent srcExtent, Buffer dest, uint destOffsetInBytes)
    {
        EnsureRenderingEnded();

        CopyTextureToBufferImpl(src, srcSlice, srcOffset, srcExtent, dest, destOffsetInBytes);
    }

    public void ResolveTexture(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice)
    {
        EnsureRenderingEnded();

        ResolveTextureImpl(src, srcSlice, dest, destSlice);
    }

    public BottomLevelAccelerationStructure BuildAccelerationStructure(BottomLevelAccelerationStructureDesc desc)
    {
        Context.ValidationLayer?.ValidateDesc(desc);

        EnsureRenderingEnded();

        return BuildAccelerationStructureImpl(desc);
    }

    public TopLevelAccelerationStructure BuildAccelerationStructure(TopLevelAccelerationStructureDesc desc)
    {
        Context.ValidationLayer?.ValidateDesc(desc);

        EnsureRenderingEnded();

        return BuildAccelerationStructureImpl(desc);
    }

    public void UpdateAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        Context.ValidationLayer?.ValidateDesc(accelerationStructure.Desc, newDesc);

        EnsureRenderingEnded();

        UpdateAccelerationStructureImpl(accelerationStructure, newDesc);

        accelerationStructure.Refresh(newDesc);
    }

    public void PreprocessResourceSets(ResourceSet[] resourceSets)
    {
        EnsureRenderingEnded();

        PreprocessResourceSetsImpl(resourceSets);
    }

    public void BindFrameBuffer(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        EnsureRenderingEnded();

        if (frameBuffer.ColorAttachmentCount is not 0)
        {
            Scissor[] scissors = new Scissor[frameBuffer.ColorAttachmentCount];
            Viewport[] viewports = new Viewport[frameBuffer.ColorAttachmentCount];

            Array.Fill(scissors, new() { Width = frameBuffer.Width, Height = frameBuffer.Height });
            Array.Fill(viewports, new() { Width = frameBuffer.Width, Height = frameBuffer.Height, MaxDepth = 1 });

            SetScissorsImpl(scissors);
            SetViewportsImpl(viewports);
        }

        currentFrameBuffer = frameBuffer;
        currentClearValue = clearValue;
    }

    public void SetScissors(Scissor[] scissors)
    {
        if (scissors.Length is 0 || currentFrameBuffer is null)
        {
            return;
        }

        SetScissorsImpl(scissors);
    }

    public void SetViewports(Viewport[] viewports)
    {
        if (viewports.Length is 0 || currentFrameBuffer is null)
        {
            return;
        }

        SetViewportsImpl(viewports);
    }

    public void BindPipeline(GraphicsPipeline pipeline)
    {
        BindPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void BindPipeline(ComputePipeline pipeline)
    {
        BindPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void BindPipeline(RayTracingPipeline pipeline)
    {
        BindPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void BindPipeline(MeshShadingPipeline pipeline)
    {
        BindPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void BindVertexBuffer(Buffer buffer, uint offsetInBytes, uint index)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        BindVertexBufferImpl(pipeline, buffer, offsetInBytes, index);
    }

    public void BindIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        BindIndexBufferImpl(pipeline, buffer, offsetInBytes, format);
    }

    public void BindResourceSet(ResourceSet resourceSet, uint index)
    {
        if (currentPipeline is null)
        {
            return;
        }

        BindResourceSetImpl(currentPipeline, resourceSet, index);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawImpl(pipeline, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndexedImpl(pipeline, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void DrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndexedIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (currentPipeline is not ComputePipeline pipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchIndirect(Buffer indirectBuffer, uint offsetInBytes)
    {
        if (currentPipeline is not ComputePipeline pipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchIndirectImpl(pipeline, indirectBuffer, offsetInBytes);
    }

    public void DispatchRays(uint width, uint height, uint depth)
    {
        if (currentPipeline is not RayTracingPipeline pipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchRaysImpl(pipeline, width, height, depth);
    }

    public void DispatchMesh(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (currentPipeline is not MeshShadingPipeline pipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DispatchMeshImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchMeshIndirect(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        if (currentPipeline is not MeshShadingPipeline pipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DispatchMeshIndirectImpl(pipeline, indirectBuffer, offsetInBytes, dispatchCount);
    }

    public void BeginQuery(QueryHeap queryHeap, uint index)
    {
        if (queryHeap.Desc.Type is QueryType.Timestamp)
        {
            return;
        }

        EnsureRenderingEnded();

        BeginQueryImpl(queryHeap, index);
    }

    public void EndQuery(QueryHeap queryHeap, uint index)
    {
        if (queryHeap.Desc.Type is QueryType.Timestamp)
        {
            return;
        }

        EnsureRenderingEnded();

        EndQueryImpl(queryHeap, index);
    }

    public void WriteTimestamp(QueryHeap queryHeap, uint index)
    {
        if (queryHeap.Desc.Type is not QueryType.Timestamp)
        {
            return;
        }

        EnsureRenderingEnded();

        WriteTimestampImpl(queryHeap, index);
    }

    public void BeginDebugEvent(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        BeginDebugEventImpl(label);
    }

    public void EndDebugEvent()
    {
        EndDebugEventImpl();
    }

    public void InsertDebugMarker(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        InsertDebugMarkerImpl(label);
    }

    internal void Begin()
    {
        BeginImpl();
    }

    internal void End()
    {
        if (currentClearValue.HasValue)
        {
            EnsureRenderingBegan();
        }

        EnsureRenderingEnded();

        EndImpl();
    }

    internal void Reset()
    {
        ResetImpl();

        Context.Uploader.Release(this);

        currentFrameBuffer = null;
        currentClearValue = null;
        currentPipeline = null;
    }

    protected override void Destroy()
    {
        Context.Uploader.Release(this);

        currentFrameBuffer = null;
        currentClearValue = null;
        currentPipeline = null;
    }

    protected abstract void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes);

    protected abstract void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent);

    protected abstract void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent);

    protected abstract void CopyTextureToBufferImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, TextureExtent srcExtent, Buffer dest, uint destOffsetInBytes);

    protected abstract void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice);

    protected abstract BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc);

    protected abstract TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc);

    protected abstract void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    protected abstract void PreprocessResourceSetsImpl(ResourceSet[] resourceSets);

    protected abstract void SetScissorsImpl(Scissor[] scissors);

    protected abstract void SetViewportsImpl(Viewport[] viewports);

    protected abstract void BindPipelineImpl(GraphicsPipeline pipeline);

    protected abstract void BindPipelineImpl(ComputePipeline pipeline);

    protected abstract void BindPipelineImpl(RayTracingPipeline pipeline);

    protected abstract void BindPipelineImpl(MeshShadingPipeline pipeline);

    protected abstract void BindVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index);

    protected abstract void BindIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format);

    protected abstract void BindResourceSetImpl(Pipeline pipeline, ResourceSet resourceSet, uint index);

    protected abstract void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    protected abstract void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    protected abstract void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ);

    protected abstract void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes);

    protected abstract void DispatchRaysImpl(RayTracingPipeline pipeline, uint width, uint height, uint depth);

    protected abstract void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ);

    protected abstract void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount);

    protected abstract void BeginQueryImpl(QueryHeap queryHeap, uint index);

    protected abstract void EndQueryImpl(QueryHeap queryHeap, uint index);

    protected abstract void WriteTimestampImpl(QueryHeap queryHeap, uint index);

    protected abstract void BeginDebugEventImpl(string label);

    protected abstract void EndDebugEventImpl();

    protected abstract void InsertDebugMarkerImpl(string label);

    protected abstract void BeginImpl();

    protected abstract void EndImpl();

    protected abstract void ResetImpl();

    protected abstract void BeginRenderingImpl(FrameBuffer frameBuffer, ClearValue? clearValue);

    protected abstract void EndRenderingImpl(FrameBuffer frameBuffer);

    private void EnsureRenderingBegan()
    {
        if (!isRendering && currentFrameBuffer is not null)
        {
            BeginRenderingImpl(currentFrameBuffer, currentClearValue);

            currentClearValue = null;

            isRendering = true;
        }
    }

    private void EnsureRenderingEnded()
    {
        if (isRendering && currentFrameBuffer is not null)
        {
            EndRenderingImpl(currentFrameBuffer);

            isRendering = false;
        }
    }
}
