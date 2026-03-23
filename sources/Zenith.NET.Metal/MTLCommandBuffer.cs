using System.Numerics;
using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandBuffer : CommandBuffer
{
    public MTL4CommandAllocator CommandAllocator;

    public MTL4CommandBuffer CommandBuffer;

    public MTL4ArgumentTable ArgumentTable;

    public MTLCommandBuffer(MTLGraphicsContext context, CommandQueue queue) : base(context, queue)
    {
        CommandAllocator = context.Device.MakeCommandAllocator();
        CommandBuffer = context.Device.MakeCommandBuffer();

        MTL4ArgumentTableDescriptor descriptor = new()
        {
            MaxBufferBindCount = 16,
            MaxTextureBindCount = 16,
            MaxSamplerStateBindCount = 16,
            SupportAttributeStrides = true
        };

        ArgumentTable = context.Device.MakeArgumentTable(descriptor, out NSError error);
        error.Success();

        CommandEncoder = new(context, CommandBuffer, ArgumentTable);
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLCommandEncoder CommandEncoder { get; }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        MTLBuffer mtlSrc = src.Metal();
        MTLBuffer mtlDest = dest.Metal();

        CommandEncoder.Compute?.Copy(mtlSrc.Buffer, srcOffsetInBytes, mtlDest.Buffer, destOffsetInBytes, sizeInBytes);
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent)
    {
        MTLBuffer mtlSrc = src.Metal();
        MTLTexture mtlDest = dest.Metal();

        uint formatSizeInBytes = ZenithHelper.SizeInBytes(mtlDest.Desc.Format);
        uint sliceRowPitchInBytes = ZenithHelper.Align(formatSizeInBytes * destExtent.Width, GraphicsContext.TextureRowPitchAlignment);
        uint sliceDepthPitchInBytes = ZenithHelper.Align(sliceRowPitchInBytes * destExtent.Height, GraphicsContext.TextureDepthPitchAlignment);

        CommandEncoder.Compute?.Copy(mtlSrc.Buffer,
                                     srcOffsetInBytes,
                                     sliceRowPitchInBytes,
                                     sliceDepthPitchInBytes,
                                     new(destExtent.Width, destExtent.Height, destExtent.Depth),
                                     mtlDest.Texture,
                                     ZenithHelper.FlattenArrayLayerIndex(mtlDest.Desc, destSlice),
                                     destSlice.MipLevel,
                                     new(destOffset.X, destOffset.Y, destOffset.Z));

    }

    protected override void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        MTLTexture mtlSrc = src.Metal();
        MTLTexture mtlDest = dest.Metal();

        CommandEncoder.Compute?.Copy(mtlSrc.Texture,
                                     ZenithHelper.FlattenArrayLayerIndex(mtlSrc.Desc, srcSlice),
                                     srcSlice.MipLevel,
                                     new(srcOffset.X, srcOffset.Y, srcOffset.Z),
                                     new(extent.Width, extent.Height, extent.Depth),
                                     mtlDest.Texture,
                                     ZenithHelper.FlattenArrayLayerIndex(mtlDest.Desc, destSlice),
                                     destSlice.MipLevel,
                                     new(destOffset.X, destOffset.Y, destOffset.Z));
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, TextureExtent srcExtent, Buffer dest, uint destOffsetInBytes)
    {
        MTLTexture mtlSrc = src.Metal();
        MTLBuffer mtlDest = dest.Metal();

        uint formatSizeInBytes = ZenithHelper.SizeInBytes(mtlSrc.Desc.Format);
        uint sliceRowPitchInBytes = ZenithHelper.Align(formatSizeInBytes * srcExtent.Width, GraphicsContext.TextureRowPitchAlignment);
        uint sliceDepthPitchInBytes = ZenithHelper.Align(sliceRowPitchInBytes * srcExtent.Height, GraphicsContext.TextureDepthPitchAlignment);

        CommandEncoder.Compute?.Copy(mtlSrc.Texture,
                                     ZenithHelper.FlattenArrayLayerIndex(mtlSrc.Desc, srcSlice),
                                     srcSlice.MipLevel,
                                     new(srcOffset.X, srcOffset.Y, srcOffset.Z),
                                     new(srcExtent.Width, srcExtent.Height, srcExtent.Depth),
                                     mtlDest.Buffer,
                                     destOffsetInBytes,
                                     sliceRowPitchInBytes,
                                     sliceDepthPitchInBytes);
    }

    protected override void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice)
    {
        MTLTexture mtlSrc = src.Metal();
        MTLTexture mtlDest = dest.Metal();

        CommandEncoder.Compute?.Copy(mtlSrc.Texture,
                                     ZenithHelper.FlattenArrayLayerIndex(mtlSrc.Desc, srcSlice),
                                     srcSlice.MipLevel,
                                     mtlDest.Texture,
                                     ZenithHelper.FlattenArrayLayerIndex(mtlDest.Desc, destSlice),
                                     destSlice.MipLevel,
                                     1,
                                     1);
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
        MTLFrameBuffer mtlFrameBuffer = frameBuffer.Metal();

        bool clearColor = clearValue.Flags.HasFlag(ClearFlags.Color);
        bool clearDepth = clearValue.Flags.HasFlag(ClearFlags.Depth);
        bool clearStencil = clearValue.Flags.HasFlag(ClearFlags.Stencil);

        for (uint i = 0; i < mtlFrameBuffer.ColorAttachmentCount; i++)
        {
            MTLRenderPassColorAttachmentDescriptor colorAttachment = mtlFrameBuffer.Descriptor.ColorAttachments[i];

            colorAttachment.LoadAction = MTLLoadAction.Load;

            if (clearColor)
            {
                colorAttachment.LoadAction = MTLLoadAction.Clear;

                Vector4 color = clearValue.ColorValues[i];

                colorAttachment.ClearColor = new()
                {
                    Red = color.X,
                    Green = color.Y,
                    Blue = color.Z,
                    Alpha = color.W
                };
            }
        }

        if (mtlFrameBuffer.HasDepthStencilAttachment)
        {
            MTLRenderPassDepthAttachmentDescriptor depthAttachment = mtlFrameBuffer.Descriptor.DepthAttachment;

            if (!depthAttachment.Texture.IsNull)
            {
                depthAttachment.LoadAction = MTLLoadAction.Load;

                if (clearDepth)
                {
                    depthAttachment.LoadAction = MTLLoadAction.Clear;
                    depthAttachment.ClearDepth = clearValue.Depth;
                }
            }

            MTLRenderPassStencilAttachmentDescriptor stencilAttachment = mtlFrameBuffer.Descriptor.StencilAttachment;

            if (!stencilAttachment.Texture.IsNull)
            {
                stencilAttachment.LoadAction = MTLLoadAction.Load;

                if (clearStencil)
                {
                    stencilAttachment.LoadAction = MTLLoadAction.Clear;
                    stencilAttachment.ClearStencil = clearValue.Stencil;
                }
            }
        }

        CommandEncoder.BeginRenderPass(mtlFrameBuffer.Descriptor);
    }

    protected override void EndRenderPassImpl(FrameBuffer frameBuffer)
    {
        CommandEncoder.EndRenderPass();
    }

    protected override void SetScissorsImpl(Scissor[] scissors)
    {
        CommandEncoder.SetScissors(scissors);
    }

    protected override void SetViewportsImpl(Viewport[] viewports)
    {
        CommandEncoder.SetViewports(viewports);
    }

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
        CommandEncoder.SetPipeline(pipeline);
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        CommandEncoder.SetPipeline(pipeline);
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        CommandEncoder.SetPipeline(pipeline);
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index)
    {
        CommandEncoder.SetVertexBuffer(buffer, offsetInBytes, index);
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        CommandEncoder.SetIndexBuffer(buffer, offsetInBytes, format);
    }

    protected override void SetResourceTableImpl(Pipeline pipeline, ResourceTable resourceTable)
    {
        CommandEncoder.SetResourceTable(resourceTable);
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        CommandEncoder.Bind();
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        CommandEncoder.Bind();
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        CommandEncoder.Bind();
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        CommandEncoder.Bind();
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        CommandEncoder.Bind();
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        CommandEncoder.Bind();
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        CommandEncoder.Bind();
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        CommandEncoder.Bind();
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
        MTLQueryHeap mtlQueryHeap = queryHeap.Metal();

        CommandBuffer.WriteTimestamp(mtlQueryHeap.CounterHeap, index);
        CommandBuffer.ResolveCounterHeap(mtlQueryHeap.CounterHeap, new(index, 1), new(mtlQueryHeap.Buffer.GpuAddress + (sizeof(ulong) * index), sizeof(ulong)), MtlFence.Null, MtlFence.Null);
    }

    protected override void BeginDebugEventImpl(string label)
    {
        CommandEncoder.BeginDebugEvent(label);
    }

    protected override void EndDebugEventImpl()
    {
        CommandEncoder.EndDebugEvent();
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        CommandEncoder.InsertDebugMarker(label);
    }

    protected override void BeginImpl()
    {
        CommandBuffer.BeginCommandBuffer(CommandAllocator);

        CommandBuffer.UseResidencySet(Context.ResidencySet);

        CommandEncoder.Begin();
    }

    protected override void EndImpl()
    {
        CommandEncoder.End();

        CommandBuffer.EndCommandBuffer();
    }

    protected override void ResetImpl()
    {
        CommandAllocator.Reset();
    }

    protected override void SetResourceName(string name)
    {
        CommandBuffer.Label = name;
    }

    protected override void Destroy()
    {
        base.Destroy();

        CommandEncoder.Dispose();

        ArgumentTable.Dispose();
        CommandBuffer.Dispose();
        CommandAllocator.Dispose();
    }
}
