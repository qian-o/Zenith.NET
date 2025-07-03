using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    protected CommandQueue Queue { get; } = queue;

    protected CommandBufferState State { get; private set; } = CommandBufferState.Idle;

    public void Begin()
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Command buffer validation is not implemented yet.");
        }

        BeginImpl();

        State = CommandBufferState.Recording;
    }

    public void End()
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Command buffer validation is not implemented yet.");
        }

        EndImpl();

        State = CommandBufferState.Completed;
    }

    public void Submit()
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Command buffer submission validation is not implemented yet.");
        }

        Queue.Submit(this);

        State = CommandBufferState.Submitted;
    }

    public void Reset()
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Command buffer reset validation is not implemented yet.");
        }

        Context.Uploader.Release(this);

        ResetImpl();

        State = CommandBufferState.Idle;
    }

    public void UploadBuffer<T>(ReadOnlySpan<T> data, IBuffer buffer, uint offsetInBytes)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Buffer upload validation is not implemented yet.");
        }

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyBufferImpl(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public void CopyBuffer(IBuffer src, uint srcOffsetInBytes, IBuffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Buffer copy validation is not implemented yet.");
        }

        CopyBufferImpl(src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);
    }

    public void UploadTexture<T>(ReadOnlySpan<T> data, ITexture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture upload validation is not implemented yet.");
        }

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyTextureImpl(temporary, 0, sizeInBytes, texture, slice, offset, extent);
    }

    public void CopyTexture(IBuffer buffer, uint offsetInBytes, uint sizeInBytes, ITexture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture copy validation is not implemented yet.");
        }

        CopyTextureImpl(buffer, offsetInBytes, sizeInBytes, texture, slice, offset, extent);
    }

    public void CopyTexture(ITexture src, TextureOffset srcOffset, ITexture dest, TextureOffset destOffset, TextureExtent extent)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture copy validation is not implemented yet.");
        }

        CopyTextureImpl(src, srcOffset, dest, destOffset, extent);
    }

    public void ResolveTexture(ITexture src, TextureOffset srcOffset, ITexture dest, TextureOffset destOffset)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture resolve validation is not implemented yet.");
        }

        ResolveTextureImpl(src, srcOffset, dest, destOffset);
    }

    public BottomLevelAccelerationStructure BuildBottomLevelAccelerationStructure(BottomLevelAccelerationStructureDesc desc)
    {
        return Context.UseDebugLayer
            ? throw new NotImplementedException("Acceleration structure validation is not implemented yet.")
            : BuildBottomLevelAccelerationStructureImpl(desc);
    }

    public TopLevelAccelerationStructure BuildTopLevelAccelerationStructure(TopLevelAccelerationStructureDesc desc)
    {
        return Context.UseDebugLayer
            ? throw new NotImplementedException("Acceleration structure validation is not implemented yet.")
            : BuildTopLevelAccelerationStructureImpl(desc);
    }

    public void UpdateTopLevelAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Acceleration structure update validation is not implemented yet.");
        }

        UpdateTopLevelAccelerationStructureImpl(accelerationStructure, newDesc);
    }

    public void BeginRendering(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Rendering validation is not implemented yet.");
        }

        BeginRenderingImpl(frameBuffer, clearValue);
    }

    public void EndRendering()
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Rendering validation is not implemented yet.");
        }

        EndRenderingImpl();
    }

    public void SetScissors(Scissor[] scissors)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Scissor validation is not implemented yet.");
        }

        SetScissorsImpl(scissors);
    }

    public void SetViewports(Viewport[] viewports)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Viewport validation is not implemented yet.");
        }

        SetViewportsImpl(viewports);
    }

    public void SetGraphicsPipeline(GraphicsPipeline pipeline)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Graphics pipeline validation is not implemented yet.");
        }

        SetGraphicsPipelineImpl(pipeline);
    }

    public void SetComputePipeline(ComputePipeline pipeline)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Compute pipeline validation is not implemented yet.");
        }

        SetComputePipelineImpl(pipeline);
    }

    public void SetRayTracingPipeline(RayTracingPipeline pipeline)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Ray tracing pipeline validation is not implemented yet.");
        }

        SetRayTracingPipelineImpl(pipeline);
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

    protected override void Destroy()
    {
        Context.Uploader.Release(this);
    }

    protected abstract void BeginImpl();

    protected abstract void EndImpl();

    protected abstract void ResetImpl();

    protected abstract void CopyBufferImpl(IBuffer src, uint srcOffsetInBytes, IBuffer dest, uint destOffsetInBytes, uint sizeInBytes);

    protected abstract void CopyTextureImpl(IBuffer buffer, uint offsetInBytes, uint sizeInBytes, ITexture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent);

    protected abstract void CopyTextureImpl(ITexture src, TextureOffset srcOffset, ITexture dest, TextureOffset destOffset, TextureExtent extent);

    protected abstract void ResolveTextureImpl(ITexture src, TextureOffset srcOffset, ITexture dest, TextureOffset destOffset);

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
}
