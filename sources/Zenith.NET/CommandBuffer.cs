using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandQueue Queue { get; } = queue;

    public CommandBufferState State { get; private set; } = CommandBufferState.Idle;

    public FrameBuffer? CurrentFrameBuffer { get; private set; }

    public GraphicsResource? CurrentPipeline { get; private set; }

    public void Begin()
    {
        Context.Validator?.Begin(this);

        BeginImpl();

        State = CommandBufferState.Recording;
    }

    public void End()
    {
        Context.Validator?.End(this);

        EndImpl();

        State = CommandBufferState.Completed;
    }

    public void Submit()
    {
        Context.Validator?.Submit(this);

        Queue.Submit(this);

        State = CommandBufferState.Submitted;
    }

    public void UploadBuffer<T>(IBuffer buffer, uint offsetInBytes, ReadOnlySpan<T> data)
    {
        Context.Validator?.UploadBuffer(this, buffer, offsetInBytes, data);

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(0, data);

        CopyBufferImpl(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public void CopyBuffer(IBuffer src, uint srcOffsetInBytes, IBuffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        Context.Validator?.CopyBuffer(this, src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);

        CopyBufferImpl(src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);
    }

    public void UploadTexture<T>(ITexture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data)
    {
        Context.Validator?.UploadTexture(this, texture, slice, offset, extent, data);

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(0, data);

        CopyTextureImpl(temporary, 0, sizeInBytes, texture, slice, offset, extent);
    }

    public void CopyTexture(IBuffer src, uint srcOffsetInBytes, uint srcSizeInBytes, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent)
    {
        Context.Validator?.CopyTexture(this, src, srcOffsetInBytes, srcSizeInBytes, dest, destSlice, destOffset, destExtent);

        CopyTextureImpl(src, srcOffsetInBytes, srcSizeInBytes, dest, destSlice, destOffset, destExtent);
    }

    public void CopyTexture(ITexture src, TextureSlice srcSlice, TextureOffset srcOffset, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        Context.Validator?.CopyTexture(this, src, srcSlice, srcOffset, dest, destSlice, destOffset, extent);

        CopyTextureImpl(src, srcSlice, srcOffset, dest, destSlice, destOffset, extent);
    }

    public void ResolveTexture(ITexture src, TextureSlice srcSlice, ITexture dest, TextureSlice destSlice)
    {
        Context.Validator?.ResolveTexture(this, src, srcSlice, dest, destSlice);

        ResolveTextureImpl(src, srcSlice, dest, destSlice);
    }

    public BottomLevelAccelerationStructure BuildBottomLevelAccelerationStructure(BottomLevelAccelerationStructureDesc desc)
    {
        Context.Validator?.BuildBottomLevelAccelerationStructure(this, desc);

        return BuildBottomLevelAccelerationStructureImpl(desc);
    }

    public TopLevelAccelerationStructure BuildTopLevelAccelerationStructure(TopLevelAccelerationStructureDesc desc)
    {
        Context.Validator?.BuildTopLevelAccelerationStructure(this, desc);

        return BuildTopLevelAccelerationStructureImpl(desc);
    }

    public void UpdateTopLevelAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        Context.Validator?.UpdateTopLevelAccelerationStructure(this, accelerationStructure, newDesc);

        UpdateTopLevelAccelerationStructureImpl(accelerationStructure, newDesc);
    }

    public void BeginRendering(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        Context.Validator?.BeginRendering(this, frameBuffer, clearValue);

        BeginRenderingImpl(frameBuffer, clearValue);

        CurrentFrameBuffer = frameBuffer;
    }

    public void EndRendering()
    {
        Context.Validator?.EndRendering(this);

        EndRenderingImpl();

        CurrentFrameBuffer = null;
    }

    public void SetScissors(Scissor[] scissors)
    {
        Context.Validator?.SetScissors(this, scissors);

        SetScissorsImpl(scissors);
    }

    public void SetViewports(Viewport[] viewports)
    {
        Context.Validator?.SetViewports(this, viewports);

        SetViewportsImpl(viewports);
    }

    public void SetGraphicsPipeline(GraphicsPipeline pipeline)
    {
        Context.Validator?.SetGraphicsPipeline(this, pipeline);

        SetGraphicsPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetComputePipeline(ComputePipeline pipeline)
    {
        Context.Validator?.SetComputePipeline(this, pipeline);

        SetComputePipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetRayTracingPipeline(RayTracingPipeline pipeline)
    {
        Context.Validator?.SetRayTracingPipeline(this, pipeline);

        SetRayTracingPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetIndexBuffer(IBuffer buffer, uint offsetInBytes, IndexFormat format)
    {
        Context.Validator?.SetIndexBuffer(this, buffer, offsetInBytes, format);

        SetIndexBufferImpl(buffer, offsetInBytes, format);
    }

    public void SetVertexBuffers(IBuffer[] buffers, uint[] offsetsInBytes)
    {
        Context.Validator?.SetVertexBuffers(this, buffers, offsetsInBytes);

        SetVertexBuffersImpl(buffers, offsetsInBytes);
    }

    public void PrepareResourceSets(ResourceSet[] sets)
    {
        Context.Validator?.PrepareResourceSets(this, sets);

        PrepareResourceSetsImpl(sets);
    }

    public void BindResourceSets(ResourceSet[] sets)
    {
        Context.Validator?.BindResourceSets(this, sets);

        BindResourceSetsImpl(sets);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Draw validation is not implemented yet.");
        }

        DrawImpl(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndirect(IBuffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Indirect draw validation is not implemented yet.");
        }

        DrawIndirectImpl(indirectBuffer, offsetInBytes, drawCount);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Indexed draw validation is not implemented yet.");
        }

        DrawIndexedImpl(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void DrawIndexedIndirect(IBuffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Indirect indexed draw validation is not implemented yet.");
        }

        DrawIndexedIndirectImpl(indirectBuffer, offsetInBytes, drawCount);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Dispatch validation is not implemented yet.");
        }

        DispatchImpl(groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchIndirect(IBuffer indirectBuffer, uint offsetInBytes)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Indirect dispatch validation is not implemented yet.");
        }

        DispatchIndirectImpl(indirectBuffer, offsetInBytes);
    }

    public void DispatchRays(uint width, uint height, uint depth)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Ray dispatch validation is not implemented yet.");
        }

        DispatchRaysImpl(width, height, depth);
    }

    public void BeginDebugEvent(string label)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Debug event validation is not implemented yet.");
        }

        BeginDebugEventImpl(label);
    }

    public void EndDebugEvent()
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Debug event validation is not implemented yet.");
        }

        EndDebugEventImpl();
    }

    public void InsertDebugMarker(string label)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Debug marker validation is not implemented yet.");
        }

        InsertDebugMarkerImpl(label);
    }

    internal void Reset()
    {
        Context.Uploader.Release(this);

        ResetImpl();

        State = CommandBufferState.Idle;
        CurrentFrameBuffer = null;
        CurrentPipeline = null;
    }

    protected override void Destroy()
    {
        Context.Uploader.Release(this);
    }

    protected abstract void BeginImpl();

    protected abstract void EndImpl();

    protected abstract void CopyBufferImpl(IBuffer src, uint srcOffsetInBytes, IBuffer dest, uint destOffsetInBytes, uint sizeInBytes);

    protected abstract void CopyTextureImpl(IBuffer src, uint srcOffsetInBytes, uint srcSizeInBytes, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent);

    protected abstract void CopyTextureImpl(ITexture src, TextureSlice srcSlice, TextureOffset srcOffset, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent);

    protected abstract void ResolveTextureImpl(ITexture src, TextureSlice srcSlice, ITexture dest, TextureSlice destSlice);

    protected abstract BottomLevelAccelerationStructure BuildBottomLevelAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc);

    protected abstract TopLevelAccelerationStructure BuildTopLevelAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc);

    protected abstract void UpdateTopLevelAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    protected abstract void BeginRenderingImpl(FrameBuffer frameBuffer, ClearValue clearValue);

    protected abstract void EndRenderingImpl();

    protected abstract void SetScissorsImpl(Scissor[] scissors);

    protected abstract void SetViewportsImpl(Viewport[] viewports);

    protected abstract void SetGraphicsPipelineImpl(GraphicsPipeline pipeline);

    protected abstract void SetComputePipelineImpl(ComputePipeline pipeline);

    protected abstract void SetRayTracingPipelineImpl(RayTracingPipeline pipeline);

    protected abstract void SetIndexBufferImpl(IBuffer buffer, uint offsetInBytes, IndexFormat format);

    protected abstract void SetVertexBuffersImpl(IBuffer[] buffers, uint[] offsetsInBytes);

    protected abstract void PrepareResourceSetsImpl(ResourceSet[] sets);

    protected abstract void BindResourceSetsImpl(ResourceSet[] sets);

    protected abstract void DrawImpl(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    protected abstract void DrawIndirectImpl(IBuffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DrawIndexedImpl(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    protected abstract void DrawIndexedIndirectImpl(IBuffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DispatchImpl(uint groupCountX, uint groupCountY, uint groupCountZ);

    protected abstract void DispatchIndirectImpl(IBuffer indirectBuffer, uint offsetInBytes);

    protected abstract void DispatchRaysImpl(uint width, uint height, uint depth);

    protected abstract void BeginDebugEventImpl(string label);

    protected abstract void EndDebugEventImpl();

    protected abstract void InsertDebugMarkerImpl(string label);

    protected abstract void ResetImpl();
}
