using System.Numerics;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private Pipeline? currentPipeline;

    public CommandQueue Queue => queue;

    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits)
    {
        return queue.Submit(this, waits);
    }

    public void MemoryBarrier()
    {
        MemoryBarrierImpl();
    }

    public void MemoryBarrier(Buffer buffer)
    {
        if (Context.ValidationLayer?.ValidateResource("MemoryBarrier.buffer", buffer) is false)
        {
            return;
        }

        MemoryBarrierImpl(buffer);
    }

    public void MemoryBarrier(Texture texture)
    {
        if (Context.ValidationLayer?.ValidateResource("MemoryBarrier.texture", texture) is false)
        {
            return;
        }

        MemoryBarrierImpl(texture);
    }

    public void MemoryBarrier(ReadOnlySpan<GraphicsResource> resources)
    {
        for (int i = 0; i < resources.Length; i++)
        {
            if (Context.ValidationLayer?.ValidateResource($"MemoryBarrier.resources[{i}]", resources[i]) is false)
            {
                return;
            }
        }

        MemoryBarrierImpl(resources);
    }

    public void AliasingBarrier(Buffer buffer)
    {
        if (Context.ValidationLayer?.ValidateResource("AliasingBarrier.buffer", buffer) is false)
        {
            return;
        }

        AliasingBarrierImpl(buffer);
    }

    public void AliasingBarrier(Texture texture)
    {
        if (Context.ValidationLayer?.ValidateResource("AliasingBarrier.texture", texture) is false)
        {
            return;
        }

        AliasingBarrierImpl(texture);
    }

    public void AliasingBarrier(ReadOnlySpan<GraphicsResource> resources)
    {
        for (int i = 0; i < resources.Length; i++)
        {
            if (Context.ValidationLayer?.ValidateResource($"AliasingBarrier.resources[{i}]", resources[i]) is false)
            {
                return;
            }
        }

        AliasingBarrierImpl(resources);
    }

    public void Transition(Texture texture, TextureSubresource subresource, TextureState state)
    {
        if (Context.ValidationLayer?.ValidateTransition(texture, subresource, state) is false)
        {
            return;
        }

        TransitionImpl(texture, subresource, state);
    }

    public void Upload(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        if (Context.ValidationLayer?.ValidateUpload(buffer, offsetInBytes, data) is false)
        {
            return;
        }

        Buffer stagingBuffer = Context.Uploader.Buffer(this, data.SizeInBytes);
        stagingBuffer.Upload(0, data);

        CopyBuffer(stagingBuffer, 0, buffer, offsetInBytes, data.SizeInBytes);
    }

    public void Download(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        if (Context.ValidationLayer?.ValidateDownload(buffer, offsetInBytes, data) is false)
        {
            return;
        }

        Buffer stagingBuffer = Context.Downloader.Buffer(this, data);

        CopyBuffer(buffer, offsetInBytes, stagingBuffer, 0, data.SizeInBytes);
    }

    public void Upload(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        if (Context.ValidationLayer?.ValidateUpload(texture, subresource, offset, extent, data) is false)
        {
            return;
        }

        Buffer stagingBuffer = Context.Uploader.Buffer(this, data.Layout.SizeInBytes);
        stagingBuffer.Upload(0, new() { Pointer = data.Pointer, SizeInBytes = data.Layout.SizeInBytes });

        CopyBufferToTexture(stagingBuffer, 0, data.Layout, texture, subresource, offset, extent);
    }

    public void Download(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        if (Context.ValidationLayer?.ValidateDownload(texture, subresource, offset, extent, data) is false)
        {
            return;
        }

        Buffer stagingBuffer = Context.Downloader.Buffer(this, new() { Pointer = data.Pointer, SizeInBytes = data.Layout.SizeInBytes });

        CopyTextureToBuffer(texture, subresource, offset, extent, stagingBuffer, 0, data.Layout);
    }

    public void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        if (Context.ValidationLayer?.ValidateCopyBuffer(src, srcOffsetInBytes, dst, dstOffsetInBytes, sizeInBytes) is false)
        {
            return;
        }

        CopyBufferImpl(src, srcOffsetInBytes, dst, dstOffsetInBytes, sizeInBytes);
    }

    public void CopyBufferToTexture(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        if (Context.ValidationLayer?.ValidateCopyBufferToTexture(src, srcOffsetInBytes, srcLayout, dst, dstSubresource, dstOffset, dstExtent) is false)
        {
            return;
        }

        CopyBufferToTextureImpl(src, srcOffsetInBytes, srcLayout, dst, dstSubresource, dstOffset, dstExtent);
    }

    public void CopyTexture(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
        if (Context.ValidationLayer?.ValidateCopyTexture(src, srcSubresource, srcOffset, dst, dstSubresource, dstOffset, extent) is false)
        {
            return;
        }

        CopyTextureImpl(src, srcSubresource, srcOffset, dst, dstSubresource, dstOffset, extent);
    }

    public void CopyTextureToBuffer(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, TextureDataLayout dstLayout)
    {
        if (Context.ValidationLayer?.ValidateCopyTextureToBuffer(src, srcSubresource, srcOffset, srcExtent, dst, dstOffsetInBytes, dstLayout) is false)
        {
            return;
        }

        CopyTextureToBufferImpl(src, srcSubresource, srcOffset, srcExtent, dst, dstOffsetInBytes, dstLayout);
    }

    public void ResolveTexture(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
        if (Context.ValidationLayer?.ValidateResolveTexture(src, srcSubresource, dst, dstSubresource) is false)
        {
            return;
        }

        ResolveTextureImpl(src, srcSubresource, dst, dstSubresource);
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
        if (Context.ValidationLayer?.ValidateResource("UpdateAccelerationStructure.accelerationStructure", accelerationStructure) is false)
        {
            return;
        }

        Context.ValidationLayer?.ValidateDesc(accelerationStructure.Desc, newDesc);

        UpdateAccelerationStructureImpl(accelerationStructure, newDesc);

        accelerationStructure.Refresh(newDesc);
    }

    public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
        if (Context.ValidationLayer?.ValidateRenderPass(colorAttachments, depthStencilAttachment) is false)
        {
            return;
        }

        int attachmentCount = colorAttachments.Length > 0 ? colorAttachments.Length : depthStencilAttachment is null ? 0 : 1;
        Span<Scissor> scissors = stackalloc Scissor[attachmentCount];
        Span<Viewport> viewports = stackalloc Viewport[attachmentCount];

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
        if (Context.ValidationLayer?.ValidateScissors(scissors) is false)
        {
            return;
        }

        SetScissorsImpl(scissors);
    }

    public void SetViewports(ReadOnlySpan<Viewport> viewports)
    {
        if (Context.ValidationLayer?.ValidateViewports(viewports) is false)
        {
            return;
        }

        SetViewportsImpl(viewports);
    }

    public void SetPipeline(GraphicsPipeline pipeline)
    {
        if (Context.ValidationLayer?.ValidateResource("SetPipeline.pipeline", pipeline) is false)
        {
            return;
        }

        SetPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void SetPipeline(ComputePipeline pipeline)
    {
        if (Context.ValidationLayer?.ValidateResource("SetPipeline.pipeline", pipeline) is false)
        {
            return;
        }

        SetPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void SetPipeline(MeshShadingPipeline pipeline)
    {
        if (Context.ValidationLayer?.ValidateResource("SetPipeline.pipeline", pipeline) is false)
        {
            return;
        }

        SetPipelineImpl(pipeline);

        currentPipeline = pipeline;
    }

    public void SetStencilReference(uint stencilReference)
    {
        if (Context.ValidationLayer?.ValidateSetStencilReference(currentPipeline) is false)
        {
            return;
        }

        SetStencilReferenceImpl(stencilReference);
    }

    public void SetBlendConstant(Vector4 blendConstant)
    {
        if (Context.ValidationLayer?.ValidateSetBlendConstant(currentPipeline, blendConstant) is false)
        {
            return;
        }

        SetBlendConstantImpl(blendConstant);
    }

    public void SetVertexBuffer(Buffer buffer, uint offsetInBytes, uint slot)
    {
        if (!TryGetCurrentPipeline(nameof(SetVertexBuffer), out GraphicsPipeline pipeline))
        {
            return;
        }

        if (Context.ValidationLayer?.ValidateSetVertexBuffer(buffer, offsetInBytes, slot, pipeline) is false)
        {
            return;
        }

        SetVertexBufferImpl(pipeline, buffer, offsetInBytes, slot);
    }

    public void SetIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        if (!TryGetCurrentPipeline(nameof(SetIndexBuffer), out GraphicsPipeline pipeline))
        {
            return;
        }

        if (Context.ValidationLayer?.ValidateSetIndexBuffer(buffer, offsetInBytes, indexFormat) is false)
        {
            return;
        }

        SetIndexBufferImpl(pipeline, buffer, offsetInBytes, indexFormat);
    }

    public void SetConstants<T>(T data) where T : unmanaged, IConstantsLayout<T>
    {
        if (!TryGetCurrentPipeline(nameof(SetConstants), out Pipeline pipeline))
        {
            return;
        }

        if (Context.ValidationLayer?.ValidateSetConstants<T>() is false)
        {
            return;
        }

        SetConstantsImpl(pipeline, Context.Constants.Buffer(this, data));
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        if (!TryGetCurrentPipeline(nameof(Draw), out GraphicsPipeline pipeline))
        {
            return;
        }

        DrawImpl(pipeline, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (!TryGetCurrentPipeline(nameof(DrawIndirect), out GraphicsPipeline pipeline))
        {
            return;
        }

        if (Context.ValidationLayer?.ValidateDrawIndirect(indirectBuffer, offsetInBytes, drawCount) is false)
        {
            return;
        }

        DrawIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        if (!TryGetCurrentPipeline(nameof(DrawIndexed), out GraphicsPipeline pipeline))
        {
            return;
        }

        DrawIndexedImpl(pipeline, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void DrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (!TryGetCurrentPipeline(nameof(DrawIndexedIndirect), out GraphicsPipeline pipeline))
        {
            return;
        }

        if (Context.ValidationLayer?.ValidateDrawIndexedIndirect(indirectBuffer, offsetInBytes, drawCount) is false)
        {
            return;
        }

        DrawIndexedIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (!TryGetCurrentPipeline(nameof(Dispatch), out ComputePipeline pipeline))
        {
            return;
        }

        DispatchImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchIndirect(Buffer indirectBuffer, uint offsetInBytes)
    {
        if (!TryGetCurrentPipeline(nameof(DispatchIndirect), out ComputePipeline pipeline))
        {
            return;
        }

        if (Context.ValidationLayer?.ValidateDispatchIndirect(indirectBuffer, offsetInBytes) is false)
        {
            return;
        }

        DispatchIndirectImpl(pipeline, indirectBuffer, offsetInBytes);
    }

    public void DispatchMesh(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (!TryGetCurrentPipeline(nameof(DispatchMesh), out MeshShadingPipeline pipeline))
        {
            return;
        }

        DispatchMeshImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchMeshIndirect(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        if (!TryGetCurrentPipeline(nameof(DispatchMeshIndirect), out MeshShadingPipeline pipeline))
        {
            return;
        }

        if (Context.ValidationLayer?.ValidateDispatchMeshIndirect(indirectBuffer, offsetInBytes, dispatchCount) is false)
        {
            return;
        }

        DispatchMeshIndirectImpl(pipeline, indirectBuffer, offsetInBytes, dispatchCount);
    }

    public void BeginQuery(QueryHeap queryHeap, uint index)
    {
        if (Context.ValidationLayer?.ValidateBeginQuery(queryHeap, index) is false)
        {
            return;
        }

        if (queryHeap.Desc.Type is QueryType.Timestamp)
        {
            return;
        }

        BeginQueryImpl(queryHeap, index);
    }

    public void EndQuery(QueryHeap queryHeap, uint index)
    {
        if (Context.ValidationLayer?.ValidateEndQuery(queryHeap, index) is false)
        {
            return;
        }

        if (queryHeap.Desc.Type is QueryType.Timestamp)
        {
            return;
        }

        EndQueryImpl(queryHeap, index);
    }

    public void WriteTimestamp(QueryHeap queryHeap, uint index)
    {
        if (Context.ValidationLayer?.ValidateWriteTimestamp(queryHeap, index) is false)
        {
            return;
        }

        if (queryHeap.Desc.Type is not QueryType.Timestamp)
        {
            return;
        }

        WriteTimestampImpl(queryHeap, index);
    }

    public void BeginDebugEvent(string label)
    {
        if (Context.ValidationLayer?.ValidateDebugLabel(nameof(BeginDebugEvent), label) is false)
        {
            return;
        }

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
        if (Context.ValidationLayer?.ValidateDebugLabel(nameof(InsertDebugMarker), label) is false)
        {
            return;
        }

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
        Context.Downloader.Release(this);
        Context.Constants.Release(this);

        currentPipeline = null;
    }

    protected override void Destroy()
    {
        Context.Uploader.Release(this);
        Context.Downloader.Release(this);
        Context.Constants.Release(this);

        currentPipeline = null;
    }

    private bool TryGetCurrentPipeline(string commandName, out Pipeline pipeline)
    {
        if (currentPipeline is not null)
        {
            if (Context.ValidationLayer?.ValidateResource($"{commandName}.pipeline", currentPipeline) is false)
            {
                pipeline = null!;

                return false;
            }

            pipeline = currentPipeline;

            return true;
        }

        Context.ValidationLayer?.ValidateCurrentPipeline(commandName);
        pipeline = null!;

        return false;
    }

    private bool TryGetCurrentPipeline<TPipeline>(string commandName, out TPipeline pipeline) where TPipeline : Pipeline
    {
        if (currentPipeline is TPipeline typedPipeline)
        {
            if (Context.ValidationLayer?.ValidateResource($"{commandName}.pipeline", typedPipeline) is false)
            {
                pipeline = null!;

                return false;
            }

            pipeline = typedPipeline;

            return true;
        }

        Context.ValidationLayer?.ValidateCurrentPipeline<TPipeline>(commandName, currentPipeline);
        pipeline = null!;

        return false;
    }

    protected abstract void MemoryBarrierImpl();

    protected abstract void MemoryBarrierImpl(Buffer buffer);

    protected abstract void MemoryBarrierImpl(Texture texture);

    protected abstract void MemoryBarrierImpl(ReadOnlySpan<GraphicsResource> resources);

    protected abstract void AliasingBarrierImpl(Buffer buffer);

    protected abstract void AliasingBarrierImpl(Texture texture);

    protected abstract void AliasingBarrierImpl(ReadOnlySpan<GraphicsResource> resources);

    protected abstract void TransitionImpl(Texture texture, TextureSubresource subresource, TextureState state);

    protected abstract void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes);

    protected abstract void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent);

    protected abstract void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent);

    protected abstract void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, TextureDataLayout dstLayout);

    protected abstract void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource);

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

    protected abstract void SetStencilReferenceImpl(uint stencilReference);

    protected abstract void SetBlendConstantImpl(Vector4 blendConstant);

    protected abstract void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint slot);

    protected abstract void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat);

    protected abstract void SetConstantsImpl(Pipeline pipeline, Buffer buffer);

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
