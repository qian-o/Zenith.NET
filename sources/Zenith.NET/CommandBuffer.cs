using System.Numerics;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private Pipeline? currentPipeline;

    public CommandQueue Queue => queue;

    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> submissions)
    {
        queue.InsertWaits(submissions);
        queue.Submit(this);

        return queue.Signal();
    }

    public void Barrier(BarrierStages before, BarrierStages after)
    {
        BarrierImpl(before, after);
    }

    public void Transition(Texture texture, TextureSubresource subresource, TextureLayout layout)
    {
        if (layout is TextureLayout.Undefined || texture[subresource] == layout)
        {
            return;
        }

        TransitionImpl(texture, subresource, texture[subresource], layout);

        texture[subresource] = layout;
    }

    public void Upload(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        Buffer transferBuffer = Context.Uploader.Buffer(this, data.SizeInBytes, new()
        {
            Pointer = data.Pointer,
            RowSizeInBytes = data.SizeInBytes,
            SrcRowStrideInBytes = data.SizeInBytes,
            DstRowStrideInBytes = data.SizeInBytes,
            Rows = 1
        });

        CopyBuffer(transferBuffer, 0, buffer, offsetInBytes, data.SizeInBytes);
    }

    public void Download(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        Buffer transferBuffer = Context.Downloader.Buffer(this, data.SizeInBytes, new()
        {
            Pointer = data.Pointer,
            RowSizeInBytes = data.SizeInBytes,
            SrcRowStrideInBytes = data.SizeInBytes,
            DstRowStrideInBytes = data.SizeInBytes,
            Rows = 1
        });

        CopyBuffer(buffer, offsetInBytes, transferBuffer, 0, data.SizeInBytes);
    }

    public void Upload(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        const uint RowPitchAlignment = 256;
        const uint DepthPitchAlignment = 512;

        (_, _, uint blocksWide, uint blocksHigh) = ZenithHelper.BlockLayout(texture.Desc.Format, extent.Width, extent.Height);

        uint rowPitchInBytes = ZenithHelper.SizeInBytes(texture.Desc.Format) * blocksWide;

        uint alignedRowPitchInBytes = ZenithHelper.Align(rowPitchInBytes, RowPitchAlignment);
        uint alignedDepthPitchInBytes = ZenithHelper.Align(alignedRowPitchInBytes * blocksHigh, DepthPitchAlignment);

        Extent3D sliceExtent = extent with { Depth = 1 };

        for (uint i = 0; i < extent.Depth; i++)
        {
            Buffer transferBuffer = Context.Uploader.Buffer(this, alignedDepthPitchInBytes, new()
            {
                Pointer = (nint)(data.Pointer + (data.SliceStrideInBytes * i)),
                RowSizeInBytes = rowPitchInBytes,
                SrcRowStrideInBytes = data.RowStrideInBytes,
                DstRowStrideInBytes = alignedRowPitchInBytes,
                Rows = blocksHigh
            });

            CopyBufferToTexture(transferBuffer, 0, alignedRowPitchInBytes, alignedDepthPitchInBytes, texture, subresource, offset, sliceExtent);

            offset.Z++;
        }
    }

    public void Download(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        const uint RowPitchAlignment = 256;
        const uint DepthPitchAlignment = 512;

        (_, _, uint blocksWide, uint blocksHigh) = ZenithHelper.BlockLayout(texture.Desc.Format, extent.Width, extent.Height);

        uint rowPitchInBytes = ZenithHelper.SizeInBytes(texture.Desc.Format) * blocksWide;

        uint alignedRowPitchInBytes = ZenithHelper.Align(rowPitchInBytes, RowPitchAlignment);
        uint alignedDepthPitchInBytes = ZenithHelper.Align(alignedRowPitchInBytes * blocksHigh, DepthPitchAlignment);

        Extent3D sliceExtent = extent with { Depth = 1 };

        for (uint i = 0; i < extent.Depth; i++)
        {
            Buffer transferBuffer = Context.Downloader.Buffer(this, alignedDepthPitchInBytes, new()
            {
                Pointer = (nint)(data.Pointer + (data.SliceStrideInBytes * i)),
                RowSizeInBytes = rowPitchInBytes,
                SrcRowStrideInBytes = alignedRowPitchInBytes,
                DstRowStrideInBytes = data.RowStrideInBytes,
                Rows = blocksHigh
            });

            CopyTextureToBuffer(texture, subresource, offset, sliceExtent, transferBuffer, 0, alignedRowPitchInBytes, alignedDepthPitchInBytes);

            offset.Z++;
        }
    }

    public void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        CopyBufferImpl(src, srcOffsetInBytes, dst, dstOffsetInBytes, sizeInBytes);
    }

    public void CopyBufferToTexture(Buffer src, uint srcOffsetInBytes, uint srcRowStrideInBytes, uint srcSliceStrideInBytes, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        TextureLayout dstLayout = dst[dstSubresource];

        Transition(dst, dstSubresource, TextureLayout.CopyDst);

        CopyBufferToTextureImpl(src, srcOffsetInBytes, srcRowStrideInBytes, srcSliceStrideInBytes, dst, dstSubresource, dstOffset, dstExtent);

        Transition(dst, dstSubresource, dstLayout);
    }

    public void CopyTexture(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
        TextureLayout srcLayout = src[srcSubresource];
        TextureLayout dstLayout = dst[dstSubresource];

        Transition(src, srcSubresource, TextureLayout.CopySrc);
        Transition(dst, dstSubresource, TextureLayout.CopyDst);

        CopyTextureImpl(src, srcSubresource, srcOffset, dst, dstSubresource, dstOffset, extent);

        Transition(src, srcSubresource, srcLayout);
        Transition(dst, dstSubresource, dstLayout);
    }

    public void CopyTextureToBuffer(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, uint dstRowStrideInBytes, uint dstSliceStrideInBytes)
    {
        TextureLayout srcLayout = src[srcSubresource];

        Transition(src, srcSubresource, TextureLayout.CopySrc);

        CopyTextureToBufferImpl(src, srcSubresource, srcOffset, srcExtent, dst, dstOffsetInBytes, dstRowStrideInBytes, dstSliceStrideInBytes);

        Transition(src, srcSubresource, srcLayout);
    }

    public void ResolveTexture(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
        TextureLayout srcLayout = src[srcSubresource];
        TextureLayout dstLayout = dst[dstSubresource];

        Transition(src, srcSubresource, TextureLayout.ResolveSrc);
        Transition(dst, dstSubresource, TextureLayout.ResolveDst);

        ResolveTextureImpl(src, srcSubresource, dst, dstSubresource);

        Transition(src, srcSubresource, srcLayout);
        Transition(dst, dstSubresource, dstLayout);
    }

    public BottomLevelAccelerationStructure BuildAccelerationStructure(BottomLevelAccelerationStructureDesc desc)
    {
        return BuildAccelerationStructureImpl(desc);
    }

    public TopLevelAccelerationStructure BuildAccelerationStructure(TopLevelAccelerationStructureDesc desc)
    {
        return BuildAccelerationStructureImpl(desc);
    }

    public void UpdateAccelerationStructure(BottomLevelAccelerationStructure accelerationStructure, BottomLevelAccelerationStructureDesc newDesc)
    {
        UpdateAccelerationStructureImpl(accelerationStructure, newDesc);

        accelerationStructure.Refresh(newDesc);
    }

    public void UpdateAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        UpdateAccelerationStructureImpl(accelerationStructure, newDesc);

        accelerationStructure.Refresh(newDesc);
    }

    public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
        int attachmentCount = colorAttachments.Length > 0 ? colorAttachments.Length : depthStencilAttachment is null ? 0 : 1;

        Span<Scissor> scissors = stackalloc Scissor[attachmentCount];
        Span<Viewport> viewports = stackalloc Viewport[attachmentCount];

        if (colorAttachments.Length > 0)
        {
            for (int i = 0; i < colorAttachments.Length; i++)
            {
                ColorAttachment attachment = colorAttachments[i];

                ZenithHelper.MipDimensions(attachment.Texture.Desc.Width,
                                           attachment.Texture.Desc.Height,
                                           0,
                                           attachment.Subresource.MipLevel,
                                           out uint width,
                                           out uint height,
                                           out _);

                scissors[i] = new()
                {
                    Width = width,
                    Height = height
                };

                viewports[i] = new()
                {
                    Width = width,
                    Height = height,
                    MaxDepth = 1.0f
                };
            }
        }
        else if (depthStencilAttachment.HasValue)
        {
            DepthStencilAttachment attachment = depthStencilAttachment.Value;

            ZenithHelper.MipDimensions(attachment.Texture.Desc.Width,
                                       attachment.Texture.Desc.Height,
                                       0,
                                       attachment.Subresource.MipLevel,
                                       out uint width,
                                       out uint height,
                                       out _);

            scissors[0] = new()
            {
                Width = width,
                Height = height
            };

            viewports[0] = new()
            {
                Width = width,
                Height = height,
                MaxDepth = 1.0f
            };
        }

        SetViewportsImpl(viewports);
        SetScissorsImpl(scissors);
        SetBlendConstantImpl(Vector4.One);
        SetStencilReferenceImpl(0);
        BeginRenderPassImpl(colorAttachments, depthStencilAttachment);
    }

    public void EndRenderPass()
    {
        EndRenderPassImpl();
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

    public void SetViewports(ReadOnlySpan<Viewport> viewports)
    {
        SetViewportsImpl(viewports);
    }

    public void SetScissors(ReadOnlySpan<Scissor> scissors)
    {
        SetScissorsImpl(scissors);
    }

    public void SetBlendConstant(Vector4 blendConstant)
    {
        SetBlendConstantImpl(blendConstant);
    }

    public void SetStencilReference(uint stencilReference)
    {
        SetStencilReferenceImpl(stencilReference);
    }

    public void SetVertexBuffer(Buffer buffer, uint offsetInBytes, uint slot)
    {
        if (!TryGetCurrentPipeline(out GraphicsPipeline pipeline))
        {
            return;
        }

        SetVertexBufferImpl(pipeline, buffer, offsetInBytes, slot);
    }

    public void SetIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        if (!TryGetCurrentPipeline(out GraphicsPipeline pipeline))
        {
            return;
        }

        SetIndexBufferImpl(pipeline, buffer, offsetInBytes, indexFormat);
    }

    public void SetConstantBuffer(Buffer buffer, uint offsetInBytes)
    {
        if (!TryGetCurrentPipeline(out Pipeline pipeline))
        {
            return;
        }

        SetConstantBufferImpl(pipeline, buffer, offsetInBytes);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        if (!TryGetCurrentPipeline(out GraphicsPipeline pipeline))
        {
            return;
        }

        DrawImpl(pipeline, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (!TryGetCurrentPipeline(out GraphicsPipeline pipeline))
        {
            return;
        }

        DrawIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        if (!TryGetCurrentPipeline(out GraphicsPipeline pipeline))
        {
            return;
        }

        DrawIndexedImpl(pipeline, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void DrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (!TryGetCurrentPipeline(out GraphicsPipeline pipeline))
        {
            return;
        }

        DrawIndexedIndirectImpl(pipeline, indirectBuffer, offsetInBytes, drawCount);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (!TryGetCurrentPipeline(out ComputePipeline pipeline))
        {
            return;
        }

        DispatchImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchIndirect(Buffer indirectBuffer, uint offsetInBytes)
    {
        if (!TryGetCurrentPipeline(out ComputePipeline pipeline))
        {
            return;
        }

        DispatchIndirectImpl(pipeline, indirectBuffer, offsetInBytes);
    }

    public void DispatchMesh(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (!TryGetCurrentPipeline(out MeshShadingPipeline pipeline))
        {
            return;
        }

        DispatchMeshImpl(pipeline, groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchMeshIndirect(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        if (!TryGetCurrentPipeline(out MeshShadingPipeline pipeline))
        {
            return;
        }

        DispatchMeshIndirectImpl(pipeline, indirectBuffer, offsetInBytes, dispatchCount);
    }

    public void BeginQuery(QueryHeap queryHeap, uint index)
    {
        BeginQueryImpl(queryHeap, index);
    }

    public void EndQuery(QueryHeap queryHeap, uint index)
    {
        EndQueryImpl(queryHeap, index);
    }

    public void WriteTimestamp(QueryHeap queryHeap, uint index)
    {
        WriteTimestampImpl(queryHeap, index);
    }

    public void BeginDebugEvent(string label)
    {
        BeginDebugEventImpl(label);
    }

    public void EndDebugEvent()
    {
        EndDebugEventImpl();
    }

    public void InsertDebugMarker(string label)
    {
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

        currentPipeline = null;
    }

    protected override void Destroy()
    {
        Context.Uploader.Release(this);
        Context.Downloader.Release(this);

        currentPipeline = null;
    }

    private bool TryGetCurrentPipeline(out Pipeline pipeline)
    {
        if (currentPipeline is not null)
        {
            pipeline = currentPipeline;

            return true;
        }

        pipeline = null!;

        return false;
    }

    private bool TryGetCurrentPipeline<TPipeline>(out TPipeline pipeline) where TPipeline : Pipeline
    {
        if (currentPipeline is TPipeline typedPipeline)
        {
            pipeline = typedPipeline;

            return true;
        }

        pipeline = null!;

        return false;
    }

    protected abstract void BarrierImpl(BarrierStages before, BarrierStages after);

    protected abstract void TransitionImpl(Texture texture, TextureSubresource subresource, TextureLayout srcLayout, TextureLayout dstLayout);

    protected abstract void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes);

    protected abstract void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, uint srcRowStrideInBytes, uint srcSliceStrideInBytes, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent);

    protected abstract void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent);

    protected abstract void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, uint dstRowStrideInBytes, uint dstSliceStrideInBytes);

    protected abstract void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource);

    protected abstract BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc);

    protected abstract TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc);

    protected abstract void UpdateAccelerationStructureImpl(BottomLevelAccelerationStructure accelerationStructure, BottomLevelAccelerationStructureDesc newDesc);

    protected abstract void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    protected abstract void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment);

    protected abstract void EndRenderPassImpl();

    protected abstract void SetPipelineImpl(GraphicsPipeline pipeline);

    protected abstract void SetPipelineImpl(ComputePipeline pipeline);

    protected abstract void SetPipelineImpl(MeshShadingPipeline pipeline);

    protected abstract void SetViewportsImpl(ReadOnlySpan<Viewport> viewports);

    protected abstract void SetScissorsImpl(ReadOnlySpan<Scissor> scissors);

    protected abstract void SetBlendConstantImpl(Vector4 blendConstant);

    protected abstract void SetStencilReferenceImpl(uint stencilReference);

    protected abstract void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint slot);

    protected abstract void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat);

    protected abstract void SetConstantBufferImpl(Pipeline pipeline, Buffer buffer, uint offsetInBytes);

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
