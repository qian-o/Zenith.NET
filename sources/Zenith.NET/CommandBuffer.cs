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
        uint sizeInBytes = (uint)(Unsafe.SizeOf<T>() * data.Length);

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyBuffer(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public void Upload<T>(Texture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data) where T : unmanaged
    {
        TextureExtent uploadExtent = new() { Width = extent.Width, Height = extent.Height, Depth = 1 };

        for (uint i = 0; i < extent.Depth; i++)
        {
            Texture temporary = Context.Uploader.Texture(this, texture.Desc.Format, extent.Width, extent.Height);
            temporary.Upload(data, default, default, uploadExtent);

            CopyTexture(temporary, default, default, texture, slice, offset, uploadExtent);

            offset.Z++;
        }
    }

    public void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        EnsureRenderingEnded();

        CopyBufferImpl(src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);
    }

    public void CopyTexture(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        EnsureRenderingEnded();

        CopyTextureImpl(src, srcSlice, srcOffset, dest, destSlice, destOffset, extent);
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

    public void BindFrameBuffer(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        EnsureRenderingEnded();

        Scissor[] scissors = new Scissor[frameBuffer.ColorAttachmentCount];
        Viewport[] viewports = new Viewport[frameBuffer.ColorAttachmentCount];

        Array.Fill(scissors, new() { Width = frameBuffer.Width, Height = frameBuffer.Height });
        Array.Fill(viewports, new() { Width = frameBuffer.Width, Height = frameBuffer.Height, MaxDepth = 1 });

        SetScissorsImpl(scissors);
        SetViewportsImpl(viewports);

        currentFrameBuffer = frameBuffer;
        currentClearValue = clearValue;
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

    public void BindResourceSets(ResourceSet[] sets)
    {
        if (currentPipeline is null)
        {
            return;
        }

        EnsureRenderingEnded();

        BindResourceSetsImpl(sets);
    }

    public void BindVertexBuffers(Buffer[] buffers, uint[] offsetsInBytes)
    {
        if (currentPipeline is not GraphicsPipeline)
        {
            return;
        }

        BindVertexBuffersImpl(buffers, offsetsInBytes);
    }

    public void BindIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        if (currentPipeline is not GraphicsPipeline)
        {
            return;
        }

        BindIndexBufferImpl(buffer, offsetInBytes, format);
    }

    public void SetScissors(Scissor[] scissors)
    {
        if (currentFrameBuffer is null)
        {
            return;
        }

        SetScissorsImpl(scissors);
    }

    public void SetViewports(Viewport[] viewports)
    {
        if (currentFrameBuffer is null)
        {
            return;
        }

        SetViewportsImpl(viewports);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        if (currentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawImpl(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (currentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndirectImpl(indirectBuffer, offsetInBytes, drawCount);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        if (currentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndexedImpl(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void DrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (currentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndexedIndirectImpl(indirectBuffer, offsetInBytes, drawCount);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (currentPipeline is not ComputePipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchImpl(groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchIndirect(Buffer indirectBuffer, uint offsetInBytes)
    {
        if (currentPipeline is not ComputePipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchIndirectImpl(indirectBuffer, offsetInBytes);
    }

    public void DispatchRays(uint width, uint height, uint depth)
    {
        if (currentPipeline is not RayTracingPipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchRaysImpl(width, height, depth);
    }

    public void DispatchMesh(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (currentPipeline is not MeshShadingPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DispatchMeshImpl(groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchMeshIndirect(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        if (currentPipeline is not MeshShadingPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DispatchMeshIndirectImpl(indirectBuffer, offsetInBytes, dispatchCount);
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

    protected abstract void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent);

    protected abstract void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice);

    protected abstract BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc);

    protected abstract TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc);

    protected abstract void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    protected abstract void BindPipelineImpl(GraphicsPipeline pipeline);

    protected abstract void BindPipelineImpl(ComputePipeline pipeline);

    protected abstract void BindPipelineImpl(RayTracingPipeline pipeline);

    protected abstract void BindPipelineImpl(MeshShadingPipeline pipeline);

    protected abstract void BindResourceSetsImpl(ResourceSet[] sets);

    protected abstract void BindVertexBuffersImpl(Buffer[] buffers, uint[] offsetsInBytes);

    protected abstract void BindIndexBufferImpl(Buffer buffer, uint offsetInBytes, IndexFormat format);

    protected abstract void SetScissorsImpl(Scissor[] scissors);

    protected abstract void SetViewportsImpl(Viewport[] viewports);

    protected abstract void DrawImpl(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    protected abstract void DrawIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DrawIndexedImpl(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    protected abstract void DrawIndexedIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DispatchImpl(uint groupCountX, uint groupCountY, uint groupCountZ);

    protected abstract void DispatchIndirectImpl(Buffer indirectBuffer, uint offsetInBytes);

    protected abstract void DispatchRaysImpl(uint width, uint height, uint depth);

    protected abstract void DispatchMeshImpl(uint groupCountX, uint groupCountY, uint groupCountZ);

    protected abstract void DispatchMeshIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount);

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

    protected abstract void EndRenderingImpl();

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
        if (isRendering)
        {
            EndRenderingImpl();

            isRendering = false;
        }
    }
}
