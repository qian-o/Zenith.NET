using System.Numerics;
using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLCommandBuffer : CommandBuffer
{
    public MTL4CommandAllocator CommandAllocator;

    public MTL4CommandBuffer CommandBuffer;

    public MTL4ArgumentTable ArgumentTable;

    public MTL4RenderCommandEncoder? Render;

    public MTL4ComputeCommandEncoder? Compute;

    private readonly Dictionary<VisibilityKey, uint> activeVisibilityIndices = [];
    private readonly List<VisibilityBinding> beginVisibilityBindings = [];
    private readonly List<VisibilityBinding> endVisibilityBindings = [];
    private readonly List<ResolveTimestamp> resolveTimestamps = [];

    private uint visibilityIndex;
    private IndexBinding indexBinding;

    private GraphicsPipeline? todoGraphicsPipeline;
    private ComputePipeline? todoComputePipeline;
    private MeshShadingPipeline? todoMeshShadingPipeline;
    private Viewport[]? todoViewports;
    private Scissor[]? todoScissors;
    private Vector4? todoBlendConstant;
    private uint? todoStencilReference;

    public MTLCommandBuffer(MTLGraphicsContext context, MTLCommandQueue queue) : base(context, queue)
    {
        CommandAllocator = context.Device.MakeCommandAllocator();
        CommandBuffer = NSAutorelease.Own(context.Device.MakeCommandBuffer);

        MTL4ArgumentTableDescriptor descriptor = new()
        {
            MaxBufferBindCount = 16,
            SupportAttributeStrides = true
        };

        ArgumentTable = context.Device.MakeArgumentTable(descriptor, out NSError error);
        error.Success();

        Visibility = new(context, new()
        {
            SizeInBytes = sizeof(ulong) * 1024,
            Residency = MemoryResidency.CpuReadOnly
        });
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public MTLBuffer Visibility { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void BarrierImpl(BarrierStages before, BarrierStages after)
    {
        Render?.BarrierAfterStages(MTLFormats.Metal(before), MTLFormats.Metal(after), MTL4VisibilityOptions.Device);
        Compute?.BarrierAfterStages(MTLFormats.Metal(before), MTLFormats.Metal(after), MTL4VisibilityOptions.Device);
    }

    protected override void TransitionImpl(Texture texture, TextureSubresource subresource, TextureLayout before, TextureLayout after)
    {
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        Compute?.Copy(src.Metal().Buffer, srcOffsetInBytes, dst.Metal().Buffer, dstOffsetInBytes, sizeInBytes);
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, uint srcRowStrideInBytes, uint srcSliceStrideInBytes, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        Compute?.Copy(src.Metal().Buffer,
                      srcOffsetInBytes,
                      srcRowStrideInBytes,
                      srcSliceStrideInBytes,
                      new(dstExtent.Width, dstExtent.Height, dstExtent.Depth),
                      dst.Metal().Texture,
                      dstSubresource.ArrayLayer,
                      dstSubresource.MipLevel,
                      new(dstOffset.X, dstOffset.Y, dstOffset.Z));
    }

    protected override void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
        Compute?.Copy(src.Metal().Texture,
                      srcSubresource.ArrayLayer,
                      srcSubresource.MipLevel,
                      new(srcOffset.X, srcOffset.Y, srcOffset.Z),
                      new(extent.Width, extent.Height, extent.Depth),
                      dst.Metal().Texture,
                      dstSubresource.ArrayLayer,
                      dstSubresource.MipLevel,
                      new(dstOffset.X, dstOffset.Y, dstOffset.Z));
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, uint dstRowStrideInBytes, uint dstSliceStrideInBytes)
    {
        Compute?.Copy(src.Metal().Texture,
                      srcSubresource.ArrayLayer,
                      srcSubresource.MipLevel,
                      new(srcOffset.X, srcOffset.Y, srcOffset.Z),
                      new(srcExtent.Width, srcExtent.Height, srcExtent.Depth),
                      dst.Metal().Buffer,
                      dstOffsetInBytes,
                      dstRowStrideInBytes,
                      dstSliceStrideInBytes);
    }

    protected override void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
        Compute?.Copy(src.Metal().Texture,
                      srcSubresource.ArrayLayer,
                      srcSubresource.MipLevel,
                      dst.Metal().Texture,
                      dstSubresource.ArrayLayer,
                      dstSubresource.MipLevel,
                      1,
                      1);
    }

    protected override BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc)
    {
        return new MTLBottomLevelAccelerationStructure(Context, this, desc);
    }

    protected override TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc)
    {
        return new MTLTopLevelAccelerationStructure(Context, this, desc);
    }

    protected override void UpdateAccelerationStructureImpl(BottomLevelAccelerationStructure accelerationStructure, BottomLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.Metal().Update(this, newDesc);
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.Metal().Update(this, newDesc);
    }

    protected override void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
        EndComputeEncoding();

        MTL4RenderPassDescriptor descriptor = new() { VisibilityResultBuffer = Visibility.Buffer };

        for (int i = 0; i < colorAttachments.Length; i++)
        {
            ColorAttachment attachment = colorAttachments[i];

            MTLTexture texture = attachment.Texture.Metal();

            descriptor.ColorAttachments[(uint)i] = new()
            {
                Texture = texture.Texture,
                Level = attachment.Subresource.MipLevel,
                Slice = attachment.Subresource.ArrayLayer,
                LoadAction = MTLFormats.Metal(attachment.LoadOp),
                StoreAction = MTLFormats.Metal(attachment.StoreOp),
                ClearColor = new(attachment.ClearColor.X, attachment.ClearColor.Y, attachment.ClearColor.Z, attachment.ClearColor.W)
            };
        }

        if (depthStencilAttachment.HasValue)
        {
            DepthStencilAttachment attachment = depthStencilAttachment.Value;

            MTLTexture texture = attachment.Texture.Metal();

            if (ZenithHelper.HasDepth(texture.Desc.Format))
            {
                descriptor.DepthAttachment = new()
                {
                    Texture = texture.Texture,
                    Level = attachment.Subresource.MipLevel,
                    Slice = attachment.Subresource.ArrayLayer,
                    LoadAction = MTLFormats.Metal(attachment.DepthLoadOp),
                    StoreAction = MTLFormats.Metal(attachment.DepthStoreOp),
                    ClearDepth = attachment.ClearDepth
                };
            }

            if (ZenithHelper.HasStencil(texture.Desc.Format))
            {
                descriptor.StencilAttachment = new()
                {
                    Texture = texture.Texture,
                    Level = attachment.Subresource.MipLevel,
                    Slice = attachment.Subresource.ArrayLayer,
                    LoadAction = MTLFormats.Metal(attachment.StencilLoadOp),
                    StoreAction = MTLFormats.Metal(attachment.StencilStoreOp),
                    ClearStencil = attachment.ClearStencil
                };
            }
        }

        BeginRenderEncoding(descriptor);
    }

    protected override void EndRenderPassImpl()
    {
        EndRenderEncoding();
        BeginComputeEncoding();
    }

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
        if (Render is null)
        {
            todoGraphicsPipeline = pipeline;
            todoComputePipeline = null;
            todoMeshShadingPipeline = null;
        }
        else
        {
            todoGraphicsPipeline = null;
            todoComputePipeline = null;
            todoMeshShadingPipeline = null;

            MTLGraphicsPipeline mtlPipeline = pipeline.Metal();

            Render.SetDepthStencilState(mtlPipeline.DepthStencilState);
            Render.SetRenderPipelineState(mtlPipeline.RenderPipelineState);
            Render.SetCullMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.CullMode));
            Render.SetFrontFacing(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FrontFace));
            Render.SetTriangleFillMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FillMode));
            Render.SetDepthClipMode(mtlPipeline.Desc.RenderState.Rasterizer.IsDepthClipEnabled ? MTLDepthClipMode.Clip : MTLDepthClipMode.Clamp);
            Render.SetDepthBias(mtlPipeline.Desc.RenderState.Rasterizer.DepthBias, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasSlopeScale, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasClamp);
        }
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        if (Compute is null)
        {
            todoGraphicsPipeline = null;
            todoComputePipeline = pipeline;
            todoMeshShadingPipeline = null;
        }
        else
        {
            todoGraphicsPipeline = null;
            todoComputePipeline = null;
            todoMeshShadingPipeline = null;

            Compute.SetComputePipelineState(pipeline.Metal().ComputePipelineState);
        }
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        if (Render is null)
        {
            todoGraphicsPipeline = null;
            todoComputePipeline = null;
            todoMeshShadingPipeline = pipeline;
        }
        else
        {
            todoGraphicsPipeline = null;
            todoComputePipeline = null;
            todoMeshShadingPipeline = null;

            MTLMeshShadingPipeline mtlPipeline = pipeline.Metal();

            Render.SetDepthStencilState(mtlPipeline.DepthStencilState);
            Render.SetRenderPipelineState(mtlPipeline.RenderPipelineState);
            Render.SetCullMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.CullMode));
            Render.SetFrontFacing(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FrontFace));
            Render.SetTriangleFillMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FillMode));
            Render.SetDepthClipMode(mtlPipeline.Desc.RenderState.Rasterizer.IsDepthClipEnabled ? MTLDepthClipMode.Clip : MTLDepthClipMode.Clamp);
            Render.SetDepthBias(mtlPipeline.Desc.RenderState.Rasterizer.DepthBias, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasSlopeScale, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasClamp);
        }
    }

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
        if (Render is null)
        {
            todoViewports = [.. viewports];
        }
        else
        {
            MTLViewport[] mtlViewports = new MTLViewport[viewports.Length];
            for (int i = 0; i < viewports.Length; i++)
            {
                Viewport viewport = viewports[i];

                mtlViewports[i] = new(viewport.X, viewport.Y, viewport.Width, viewport.Height, viewport.MinDepth, viewport.MaxDepth);
            }

            Render.SetViewports(mtlViewports);
        }
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
        if (Render is null)
        {
            todoScissors = [.. scissors];
        }
        else
        {
            MTLScissorRect[] mtlScissors = new MTLScissorRect[scissors.Length];
            for (int i = 0; i < scissors.Length; i++)
            {
                Scissor scissor = scissors[i];

                mtlScissors[i] = new((uint)scissor.X, (uint)scissor.Y, scissor.Width, scissor.Height);
            }

            Render.SetScissorRects(mtlScissors);
        }
    }

    protected override void SetBlendConstantImpl(Vector4 blendConstant)
    {
        if (Render is null)
        {
            todoBlendConstant = blendConstant;
        }
        else
        {
            Render.SetBlendColor(blendConstant.X, blendConstant.Y, blendConstant.Z, blendConstant.W);
        }
    }

    protected override void SetStencilReferenceImpl(uint stencilReference)
    {
        if (Render is null)
        {
            todoStencilReference = stencilReference;
        }
        else
        {
            Render.SetStencilReferenceValue(stencilReference);
        }
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint slot)
    {
        ArgumentTable.SetAddress(buffer.Metal().Buffer.GpuAddress + offsetInBytes, pipeline.Desc.InputLayouts[slot].StrideInBytes, 1 + slot);
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        indexBinding = new(MTLFormats.Metal(indexFormat),
                           buffer.Metal().Buffer.GpuAddress + offsetInBytes,
                           indexFormat is IndexFormat.UInt16 ? 2u : 4u,
                           buffer.Desc.SizeInBytes - offsetInBytes);
    }

    protected override void SetConstantBufferImpl(Pipeline pipeline, Buffer buffer, uint offsetInBytes)
    {
        ArgumentTable.SetAddress(buffer.Metal().Buffer.GpuAddress + offsetInBytes, 0);

        Render?.SetArgumentTable(ArgumentTable, MTLRenderStages.Vertex | MTLRenderStages.Fragment | MTLRenderStages.Mesh);
        Compute?.SetArgumentTable(ArgumentTable);
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        Render?.DrawPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type, firstVertex, vertexCount, instanceCount, firstInstance);
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        nuint address = indirectBuffer.Metal().Buffer.GpuAddress + offsetInBytes;

        for (uint i = 0; i < drawCount; i++)
        {
            Render?.DrawPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type, address + (uint)(sizeof(IndirectDrawArgs) * i));
        }
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        Render?.DrawIndexedPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type,
                                      indexCount,
                                      indexBinding.Type,
                                      indexBinding.Address + (indexBinding.SizeInBytes * firstIndex),
                                      indexBinding.LengthInBytes,
                                      instanceCount,
                                      vertexOffset,
                                      firstInstance);
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        nuint address = indirectBuffer.Metal().Buffer.GpuAddress + offsetInBytes;

        for (uint i = 0; i < drawCount; i++)
        {
            Render?.DrawIndexedPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type,
                                          indexBinding.Type,
                                          indexBinding.Address,
                                          indexBinding.LengthInBytes,
                                          address + (uint)(sizeof(IndirectDrawIndexedArgs) * i));
        }
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        Compute?.DispatchThreadgroups(new MTLSize(groupCountX, groupCountY, groupCountZ), pipeline.Metal().ComputePipelineState.RequiredThreadsPerThreadgroup);
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        Compute?.DispatchThreadgroups(indirectBuffer.Metal().Buffer.GpuAddress + offsetInBytes, pipeline.Metal().ComputePipelineState.RequiredThreadsPerThreadgroup);
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        Render?.DrawMeshThreadgroups(new MTLSize(groupCountX, groupCountY, groupCountZ),
                                     pipeline.Metal().RenderPipelineState.RequiredThreadsPerObjectThreadgroup,
                                     pipeline.Metal().RenderPipelineState.RequiredThreadsPerMeshThreadgroup);
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        nuint address = indirectBuffer.Metal().Buffer.GpuAddress + offsetInBytes;

        for (uint i = 0; i < dispatchCount; i++)
        {
            Render?.DrawMeshThreadgroups(address + (uint)(sizeof(IndirectDispatchMeshArgs) * i),
                                         pipeline.Metal().RenderPipelineState.RequiredThreadsPerObjectThreadgroup,
                                         pipeline.Metal().RenderPipelineState.RequiredThreadsPerMeshThreadgroup);
        }
    }

    protected override void BeginQueryImpl(QueryHeap queryHeap, uint index)
    {
        MTLQueryHeap mtlQueryHeap = queryHeap.Metal();

        uint scratchIndex = visibilityIndex++;

        activeVisibilityIndices.Add(new(mtlQueryHeap, index), scratchIndex);

        if (Render is null)
        {
            beginVisibilityBindings.Add(new(mtlQueryHeap, index, scratchIndex));
        }
        else
        {
            Render.SetVisibilityResultMode(MTLFormats.Metal(mtlQueryHeap.Desc.Type), sizeof(ulong) * scratchIndex);
        }
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
        MTLQueryHeap mtlQueryHeap = queryHeap.Metal();

        if (activeVisibilityIndices.Remove(new(mtlQueryHeap, index), out uint scratchIndex))
        {
            Render?.SetVisibilityResultMode(MTLVisibilityResultMode.Disabled, 0);

            endVisibilityBindings.Add(new(mtlQueryHeap, index, scratchIndex));
        }
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
        MTLQueryHeap mtlQueryHeap = queryHeap.Metal();

        Render?.WriteTimestamp(MTL4TimestampGranularity.Precise, MTLRenderStages.Fragment, mtlQueryHeap.CounterHeap, index);
        Compute?.WriteTimestamp(MTL4TimestampGranularity.Precise, mtlQueryHeap.CounterHeap, index);

        resolveTimestamps.Add(new(mtlQueryHeap, index));
    }

    protected override void BeginDebugEventImpl(string label)
    {
        Render?.PushDebugGroup(label);
        Compute?.PushDebugGroup(label);
    }

    protected override void EndDebugEventImpl()
    {
        Render?.PopDebugGroup();
        Compute?.PopDebugGroup();
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        Render?.InsertDebugSignpost(label);
        Compute?.InsertDebugSignpost(label);
    }

    protected override void BeginImpl()
    {
        CommandBuffer.BeginCommandBuffer(CommandAllocator);

        BeginComputeEncoding();
    }

    protected override void EndImpl()
    {
        EndRenderEncoding();
        EndComputeEncoding();

        CommandBuffer.EndCommandBuffer();
    }

    protected override void ResetImpl()
    {
        activeVisibilityIndices.Clear();
        beginVisibilityBindings.Clear();
        endVisibilityBindings.Clear();
        resolveTimestamps.Clear();

        visibilityIndex = 0;
        indexBinding = default;

        todoScissors = null;
        todoViewports = null;
        todoGraphicsPipeline = null;
        todoComputePipeline = null;
        todoMeshShadingPipeline = null;
        todoStencilReference = null;
        todoBlendConstant = null;

        CommandAllocator.Reset();
    }

    protected override void SetResourceName(string name)
    {
        CommandBuffer.Label = name;
    }

    protected override void Destroy()
    {
        base.Destroy();

        Visibility.Dispose();
        ArgumentTable.Dispose();
        CommandBuffer.Dispose();
        CommandAllocator.Dispose();
    }

    private void BeginRenderEncoding(MTL4RenderPassDescriptor descriptor)
    {
        Render = NSAutorelease.Own(CommandBuffer.MakeRenderCommandEncoder, descriptor);
        Render.SetArgumentTable(ArgumentTable, MTLRenderStages.Vertex | MTLRenderStages.Fragment | MTLRenderStages.Mesh);

        if (todoGraphicsPipeline is not null)
        {
            SetPipeline(todoGraphicsPipeline);

            todoGraphicsPipeline = null;
        }

        if (todoMeshShadingPipeline is not null)
        {
            SetPipeline(todoMeshShadingPipeline);

            todoMeshShadingPipeline = null;
        }

        if (todoViewports is not null)
        {
            SetViewports(todoViewports);

            todoViewports = null;
        }

        if (todoScissors is not null)
        {
            SetScissors(todoScissors);

            todoScissors = null;
        }

        if (todoBlendConstant is not null)
        {
            SetBlendConstant(todoBlendConstant.Value);

            todoBlendConstant = null;
        }

        if (todoStencilReference is not null)
        {
            SetStencilReference(todoStencilReference.Value);

            todoStencilReference = null;
        }

        foreach (VisibilityBinding visibilityBinding in beginVisibilityBindings)
        {
            Render.SetVisibilityResultMode(MTLFormats.Metal(visibilityBinding.QueryHeap.Desc.Type), sizeof(ulong) * visibilityBinding.ScratchIndex);
        }
        beginVisibilityBindings.Clear();
    }

    private void EndRenderEncoding()
    {
        ResolveTimestamps();

        Render?.BarrierAfterEncoderStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        Render?.EndEncoding();
        Render?.Dispose();
        Render = null;
    }

    private void BeginComputeEncoding()
    {
        Compute = NSAutorelease.Own(CommandBuffer.MakeComputeCommandEncoder);
        Compute.SetArgumentTable(ArgumentTable);

        if (todoComputePipeline is not null)
        {
            SetPipeline(todoComputePipeline);

            todoComputePipeline = null;
        }

        foreach (VisibilityBinding visibilityBinding in endVisibilityBindings)
        {
            CopyBuffer(Visibility, sizeof(ulong) * visibilityBinding.ScratchIndex, visibilityBinding.QueryHeap.Buffer, sizeof(ulong) * visibilityBinding.Index, sizeof(ulong));
        }
        endVisibilityBindings.Clear();
    }

    private void EndComputeEncoding()
    {
        ResolveTimestamps();

        Compute?.BarrierAfterEncoderStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        Compute?.EndEncoding();
        Compute?.Dispose();
        Compute = null;
    }

    private void ResolveTimestamps()
    {
        foreach (ResolveTimestamp resolveTimestamp in resolveTimestamps)
        {
            CommandBuffer.ResolveCounterHeap(resolveTimestamp.QueryHeap.CounterHeap,
                                             new(resolveTimestamp.Index, 1),
                                             new(resolveTimestamp.QueryHeap.Buffer.Buffer.GpuAddress + (sizeof(ulong) * resolveTimestamp.Index), sizeof(ulong)),
                                             MTLFence.Null,
                                             MTLFence.Null);
        }
        resolveTimestamps.Clear();
    }

    private struct IndexBinding(MTLIndexType type, nuint address, uint sizeInBytes, uint lengthInBytes)
    {
        public MTLIndexType Type = type;

        public nuint Address = address;

        public uint SizeInBytes = sizeInBytes;

        public uint LengthInBytes = lengthInBytes;
    }

    private struct VisibilityKey(MTLQueryHeap queryHeap, uint index)
    {
        public MTLQueryHeap QueryHeap = queryHeap;

        public uint Index = index;
    }

    private struct VisibilityBinding(MTLQueryHeap queryHeap, uint index, uint scratchIndex)
    {
        public MTLQueryHeap QueryHeap = queryHeap;

        public uint Index = index;

        public uint ScratchIndex = scratchIndex;
    }

    private struct ResolveTimestamp(MTLQueryHeap queryHeap, uint index)
    {
        public MTLQueryHeap QueryHeap = queryHeap;

        public uint Index = index;
    }
}