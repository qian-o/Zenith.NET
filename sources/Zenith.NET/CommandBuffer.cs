namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private Pipeline? currentPipeline;

    public CommandQueue Queue => queue;

    public Submission Submit(params ReadOnlySpan<Submission> waits)
    {
        return queue.Submit(this, waits);
    }

    public void MemoryBarrier()
    {
        MemoryBarrierImpl();
    }

    public void MemoryBarrier(Texture texture)
    {
        MemoryBarrierImpl(texture);
    }

    public void MemoryBarrier(Buffer buffer)
    {
        MemoryBarrierImpl(buffer);
    }

    public void Upload(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        Buffer temporary = Context.Uploader.Buffer(this, data.SizeInBytes);
        temporary.Upload(0, data);

        CopyBuffer(temporary, 0, buffer, offsetInBytes, data.SizeInBytes);
    }

    public void Download(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        throw new NotImplementedException();
    }

    public void Upload(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        Buffer temporary = Context.Uploader.Buffer(this, data.Layout.SizeInBytes);
        temporary.Upload(0, new() { Pointer = data.Pointer, SizeInBytes = data.Layout.SizeInBytes });

        CopyBufferToTexture(temporary, 0, data.Layout, texture, subresource, offset, extent);
    }

    public void Download(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        throw new NotImplementedException();
    }

    public void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        CopyBufferImpl(src, srcOffsetInBytes, dest, destOffsetInBytes, sizeInBytes);
    }

    public void CopyBufferToTexture(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D destExtent)
    {
        CopyBufferToTextureImpl(src, srcOffsetInBytes, srcLayout, dest, destSubresource, destOffset, destExtent);
    }

    public void CopyTexture(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D extent)
    {
        CopyTextureImpl(src, srcSubresource, srcOffset, dest, destSubresource, destOffset, extent);
    }

    public void CopyTextureToBuffer(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dest, uint destOffsetInBytes, TextureDataLayout destLayout)
    {
        CopyTextureToBufferImpl(src, srcSubresource, srcOffset, srcExtent, dest, destOffsetInBytes, destLayout);
    }

    public void ResolveTexture(Texture src, TextureSubresource srcSubresource, Texture dest, TextureSubresource destSubresource)
    {
        ResolveTextureImpl(src, srcSubresource, dest, destSubresource);
    }

    public BottomLevelAccelerationStructure BuildAccelerationStructure(BottomLevelAccelerationStructureDesc desc)
    {
        Context.ValidationLayer?.ValidateDesc(desc);

        return BuildAccelerationStructureImpl(desc);
    }

    public TopLevelAccelerationStructure BuildAccelerationStructure(TopLevelAccelerationStructureDesc desc)
    {
        Context.ValidationLayer?.ValidateDesc(desc);

        return BuildAccelerationStructureImpl(desc);
    }

    public void UpdateAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        Context.ValidationLayer?.ValidateDesc(accelerationStructure.Desc, newDesc);

        UpdateAccelerationStructureImpl(accelerationStructure, newDesc);

        accelerationStructure.Refresh(newDesc);
    }

    public void Transition(Texture texture, TextureSubresource subresource, TextureLayout layout)
    {
        throw new NotImplementedException();
    }

    public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
        Span<Scissor> scissors = stackalloc Scissor[8];
        Span<Viewport> viewports = stackalloc Viewport[8];

        if (colorAttachments.Length > 0)
        {
            for (int i = 0; i < colorAttachments.Length; i++)
            {
                ZenithHelper.MipDimensions(colorAttachments[i].Texture.Desc.Width,
                                           colorAttachments[i].Texture.Desc.Height,
                                           0,
                                           colorAttachments[i].Subresource.MipLevel,
                                           out uint width,
                                           out uint height,
                                           out _);

                scissors[i] = new() { Width = width, Height = height };
                viewports[i] = new() { Width = width, Height = height, MaxDepth = 1.0f };
            }
        }
        else if (depthStencilAttachment is { } attachment)
        {
            ZenithHelper.MipDimensions(attachment.Texture.Desc.Width,
                                       attachment.Texture.Desc.Height,
                                       0,
                                       attachment.Subresource.MipLevel,
                                       out uint width,
                                       out uint height,
                                       out _);

            scissors[0] = new() { Width = width, Height = height };
            viewports[0] = new() { Width = width, Height = height, MaxDepth = 1.0f };
        }

        SetScissorsImpl(scissors);
        SetViewportsImpl(viewports);
        BeginRenderPassImpl(colorAttachments, depthStencilAttachment);
    }

    public void EndRenderPass()
    {
        EndRenderPassImpl();
    }

    public void SetScissors(ReadOnlySpan<Scissor> scissors)
    {
        SetScissorsImpl(scissors);
    }

    public void SetViewports(ReadOnlySpan<Viewport> viewports)
    {
        SetViewportsImpl(viewports);
    }

    public void SetPipeline(GraphicsPipeline pipeline)
    {
        SetPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void SetPipeline(ComputePipeline pipeline)
    {
        SetPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void SetPipeline(MeshShadingPipeline pipeline)
    {
        SetPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void SetVertexBuffer(Buffer buffer, uint offsetInBytes, uint index)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        SetVertexBufferImpl(pipeline, buffer, offsetInBytes, index);
    }

    public void SetIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        SetIndexBufferImpl(pipeline, buffer, offsetInBytes, indexFormat);
    }

    public void PushResourceTable(ResourceTable resourceTable)
    {
        if (currentPipeline is null)
        {
            return;
        }

        PushResourceTableImpl(currentPipeline, resourceTable);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        DrawImpl(pipeline, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        DrawIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        DrawIndexedImpl(pipeline, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void DrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (currentPipeline is not GraphicsPipeline pipeline)
        {
            return;
        }

        DrawIndexedIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (currentPipeline is not ComputePipeline pipeline)
        {
            return;
        }

        DispatchImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchIndirect(Buffer indirectBuffer, uint offsetInBytes)
    {
        if (currentPipeline is not ComputePipeline pipeline)
        {
            return;
        }

        DispatchIndirectImpl(pipeline, indirectBuffer, offsetInBytes);
    }

    public void DispatchMesh(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (currentPipeline is not MeshShadingPipeline pipeline)
        {
            return;
        }

        DispatchMeshImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchMeshIndirect(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        if (currentPipeline is not MeshShadingPipeline pipeline)
        {
            return;
        }

        DispatchMeshIndirectImpl(pipeline, indirectBuffer, offsetInBytes, dispatchCount);
    }

    public void BeginQuery(QueryHeap queryHeap, uint index)
    {
        if (queryHeap.Desc.Type is QueryType.Timestamp)
        {
            return;
        }

        BeginQueryImpl(queryHeap, index);
    }

    public void EndQuery(QueryHeap queryHeap, uint index)
    {
        if (queryHeap.Desc.Type is QueryType.Timestamp)
        {
            return;
        }

        EndQueryImpl(queryHeap, index);
    }

    public void WriteTimestamp(QueryHeap queryHeap, uint index)
    {
        if (queryHeap.Desc.Type is not QueryType.Timestamp)
        {
            return;
        }

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
        EndImpl();
    }

    internal void Reset()
    {
        ResetImpl();

        Context.Uploader.Release(this);

        currentPipeline = null;
    }

    protected override void Destroy()
    {
        Context.Uploader.Release(this);

        currentPipeline = null;
    }

    protected abstract void MemoryBarrierImpl();

    protected abstract void MemoryBarrierImpl(Texture texture);

    protected abstract void MemoryBarrierImpl(Buffer buffer);

    protected abstract void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes);

    protected abstract void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D destExtent);

    protected abstract void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D extent);

    protected abstract void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dest, uint destOffsetInBytes, TextureDataLayout destLayout);

    protected abstract void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dest, TextureSubresource destSubresource);

    protected abstract BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc);

    protected abstract TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc);

    protected abstract void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    protected abstract void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment);

    protected abstract void EndRenderPassImpl();

    protected abstract void SetScissorsImpl(ReadOnlySpan<Scissor> scissors);

    protected abstract void SetViewportsImpl(ReadOnlySpan<Viewport> viewports);

    protected abstract void SetPipelineImpl(GraphicsPipeline pipeline);

    protected abstract void SetPipelineImpl(ComputePipeline pipeline);

    protected abstract void SetPipelineImpl(MeshShadingPipeline pipeline);

    protected abstract void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index);

    protected abstract void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat);

    protected abstract void PushResourceTableImpl(Pipeline pipeline, ResourceTable resourceTable);

    protected abstract void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    protected abstract void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    protected abstract void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ);

    protected abstract void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes);

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
}
