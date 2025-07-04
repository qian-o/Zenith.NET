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
        Context.Validator?.ValidateBegin(this);

        BeginImpl();

        State = CommandBufferState.Recording;
    }

    public void End()
    {
        Context.Validator?.ValidateEnd(this);

        EndImpl();

        State = CommandBufferState.Completed;
    }

    public void Submit()
    {
        Context.Validator?.ValidateSubmit(this);

        Queue.Submit(this);

        State = CommandBufferState.Submitted;
    }

    public void UploadBuffer<T>(IBuffer buffer, uint offsetInBytes, ReadOnlySpan<T> data)
    {
        Context.Validator?.ValidateUploadBuffer(this, buffer, offsetInBytes, data);

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(0, data);

        CopyBufferImpl(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public void CopyBuffer(IBuffer src, uint srcOffsetInBytes, IBuffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        Context.Validator?.ValidateCopyBuffer(this, src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);

        CopyBufferImpl(src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);
    }

    public void UploadTexture<T>(ITexture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data)
    {
        Context.Validator?.ValidateUploadTexture(this, texture, slice, offset, extent, data);

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(0, data);

        CopyTextureImpl(temporary, 0, sizeInBytes, texture, slice, offset, extent);
    }

    public void CopyTexture(IBuffer src, uint srcOffsetInBytes, uint srcSizeInBytes, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent)
    {
        Context.Validator?.ValidateCopyTexture(this, src, srcOffsetInBytes, srcSizeInBytes, dest, destSlice, destOffset, destExtent);

        CopyTextureImpl(src, srcOffsetInBytes, srcSizeInBytes, dest, destSlice, destOffset, destExtent);
    }

    public void CopyTexture(ITexture src, TextureSlice srcSlice, TextureOffset srcOffset, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        Context.Validator?.ValidateCopyTexture(this, src, srcSlice, srcOffset, dest, destSlice, destOffset, extent);

        CopyTextureImpl(src, srcSlice, srcOffset, dest, destSlice, destOffset, extent);
    }

    public void ResolveTexture(ITexture src, TextureSlice srcSlice, ITexture dest, TextureSlice destSlice)
    {
        Context.Validator?.ValidateResolveTexture(this, src, srcSlice, dest, destSlice);

        ResolveTextureImpl(src, srcSlice, dest, destSlice);
    }

    public BottomLevelAccelerationStructure BuildBottomLevelAccelerationStructure(BottomLevelAccelerationStructureDesc desc)
    {
        Context.Validator?.ValidateBuildBottomLevelAccelerationStructure(this, desc);

        return BuildBottomLevelAccelerationStructureImpl(desc);
    }

    public TopLevelAccelerationStructure BuildTopLevelAccelerationStructure(TopLevelAccelerationStructureDesc desc)
    {
        Context.Validator?.ValidateBuildTopLevelAccelerationStructure(this, desc);

        return BuildTopLevelAccelerationStructureImpl(desc);
    }

    public void UpdateTopLevelAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        Context.Validator?.ValidateUpdateTopLevelAccelerationStructure(this, accelerationStructure, newDesc);

        UpdateTopLevelAccelerationStructureImpl(accelerationStructure, newDesc);
    }

    public void BeginRendering(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        Context.Validator?.ValidateBeginRendering(this, frameBuffer, clearValue);

        BeginRenderingImpl(frameBuffer, clearValue);

        CurrentFrameBuffer = frameBuffer;
    }

    public void EndRendering()
    {
        Context.Validator?.ValidateEndRendering(this);

        EndRenderingImpl();

        CurrentFrameBuffer = null;
    }

    public void SetScissors(Scissor[] scissors)
    {
        Context.Validator?.ValidateSetScissors(this, scissors);

        SetScissorsImpl(scissors);
    }

    public void SetViewports(Viewport[] viewports)
    {
        Context.Validator?.ValidateSetViewports(this, viewports);

        SetViewportsImpl(viewports);
    }

    public void SetGraphicsPipeline(GraphicsPipeline pipeline)
    {
        Context.Validator?.ValidateSetGraphicsPipeline(this, pipeline);

        SetGraphicsPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetComputePipeline(ComputePipeline pipeline)
    {
        Context.Validator?.ValidateSetComputePipeline(this, pipeline);

        SetComputePipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetRayTracingPipeline(RayTracingPipeline pipeline)
    {
        Context.Validator?.ValidateSetRayTracingPipeline(this, pipeline);

        SetRayTracingPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetVertexBuffer(IBuffer buffer, uint offsetInBytes, uint slot)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Vertex buffer validation is not implemented yet.");
        }

        SetVertexBufferImpl(buffer, offsetInBytes, slot);
    }

    public void SetIndexBuffer(IBuffer buffer, uint offsetInBytes, IndexFormat format)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Index buffer validation is not implemented yet.");
        }

        SetIndexBufferImpl(buffer, offsetInBytes, format);
    }

    public void PrepareResourceSets(ResourceSet[] sets)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Resource set preparation validation is not implemented yet.");
        }

        PrepareResourceSetsImpl(sets);
    }

    public void BindResourceSet(ResourceSet set, uint slot)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Resource set validation is not implemented yet.");
        }

        BindResourceSetImpl(set, slot);
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

    protected abstract void SetVertexBufferImpl(IBuffer buffer, uint offsetInBytes, uint slot);

    protected abstract void SetIndexBufferImpl(IBuffer buffer, uint offsetInBytes, IndexFormat format);

    protected abstract void PrepareResourceSetsImpl(ResourceSet[] sets);

    protected abstract void BindResourceSetImpl(ResourceSet set, uint slot);

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
