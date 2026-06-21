using System.Numerics;
using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLCommandBuffer : CommandBuffer
{
    public MTL4CommandAllocator CommandAllocator;

    public MTL4CommandBuffer CommandBuffer;

    public MTL4ArgumentTable ArgumentTable;

    private MTL4RenderCommandEncoder? render;
    private MTL4ComputeCommandEncoder? compute;

    private Scissor[]? todoScissors;
    private Viewport[]? todoViewports;
    private GraphicsPipeline? todoGraphicsPipeline;
    private ComputePipeline? todoComputePipeline;
    private MeshShadingPipeline? todoMeshShadingPipeline;
    private uint? todoStencilReference;
    private Vector4? todoBlendConstant;

    private IndexBinding indexBinding;

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
    }

    public new MTLGraphicsContext Context => (MTLGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void BarrierImpl(BarrierStages before, BarrierStages after)
    {
        render?.BarrierAfterStages(MTLFormats.Metal(after), MTLFormats.Metal(before), MTL4VisibilityOptions.Device);
        compute?.BarrierAfterStages(MTLFormats.Metal(after), MTLFormats.Metal(before), MTL4VisibilityOptions.Device);
    }

    protected override void TransitionImpl(Texture texture, TextureSubresource subresource, TextureLayout srcLayout, TextureLayout dstLayout)
    {
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        compute?.Copy(src.Metal().Buffer, srcOffsetInBytes, dst.Metal().Buffer, dstOffsetInBytes, sizeInBytes);
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, uint srcRowStrideInBytes, uint srcSliceStrideInBytes, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        compute?.Copy(src.Metal().Buffer,
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
        compute?.Copy(src.Metal().Texture,
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
        compute?.Copy(src.Metal().Texture,
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
        compute?.Copy(src.Metal().Texture,
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
        BeginRenderEncoding(MTL4RenderPassDescriptor.Null);
    }

    protected override void EndRenderPassImpl()
    {
        EndRenderEncoding();
        BeginComputeEncoding();
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
        if (render is null)
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

            render.SetScissorRects(mtlScissors);
        }
    }

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
        if (render is null)
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

            render.SetViewports(mtlViewports);
        }
    }

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
        if (render is null)
        {
            todoGraphicsPipeline = pipeline;
            todoComputePipeline = null;
            todoMeshShadingPipeline = null;
        }
        else
        {
            MTLGraphicsPipeline mtlPipeline = pipeline.Metal();

            render.SetDepthStencilState(mtlPipeline.DepthStencilState);
            render.SetRenderPipelineState(mtlPipeline.RenderPipelineState);
            render.SetCullMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.CullMode));
            render.SetFrontFacing(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FrontFace));
            render.SetTriangleFillMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FillMode));
            render.SetDepthClipMode(mtlPipeline.Desc.RenderState.Rasterizer.IsDepthClipEnabled ? MTLDepthClipMode.Clip : MTLDepthClipMode.Clamp);
            render.SetDepthBias(mtlPipeline.Desc.RenderState.Rasterizer.DepthBias, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasSlopeScale, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasClamp);
        }
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        if (compute is null)
        {
            todoGraphicsPipeline = null;
            todoComputePipeline = pipeline;
            todoMeshShadingPipeline = null;
        }
        else
        {
            compute.SetComputePipelineState(pipeline.Metal().ComputePipelineState);
        }
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        if (render is null)
        {
            todoGraphicsPipeline = null;
            todoComputePipeline = null;
            todoMeshShadingPipeline = pipeline;
        }
        else
        {
            MTLMeshShadingPipeline mtlPipeline = pipeline.Metal();

            render.SetDepthStencilState(mtlPipeline.DepthStencilState);
            render.SetRenderPipelineState(mtlPipeline.RenderPipelineState);
            render.SetCullMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.CullMode));
            render.SetFrontFacing(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FrontFace));
            render.SetTriangleFillMode(MTLFormats.Metal(mtlPipeline.Desc.RenderState.Rasterizer.FillMode));
            render.SetDepthClipMode(mtlPipeline.Desc.RenderState.Rasterizer.IsDepthClipEnabled ? MTLDepthClipMode.Clip : MTLDepthClipMode.Clamp);
            render.SetDepthBias(mtlPipeline.Desc.RenderState.Rasterizer.DepthBias, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasSlopeScale, mtlPipeline.Desc.RenderState.Rasterizer.DepthBiasClamp);
        }
    }

    protected override void SetStencilReferenceImpl(uint stencilReference)
    {
        if (render is null)
        {
            todoStencilReference = stencilReference;
        }
        else
        {
            render.SetStencilReferenceValue(stencilReference);
        }
    }

    protected override void SetBlendConstantImpl(Vector4 blendConstant)
    {
        if (render is null)
        {
            todoBlendConstant = blendConstant;
        }
        else
        {
            render.SetBlendColor(blendConstant.X, blendConstant.Y, blendConstant.Z, blendConstant.W);
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

        render?.SetArgumentTable(ArgumentTable, MTLRenderStages.Vertex | MTLRenderStages.Fragment | MTLRenderStages.Mesh);
        compute?.SetArgumentTable(ArgumentTable);
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        render?.DrawPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type, firstVertex, vertexCount, instanceCount, firstInstance);
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        nuint address = indirectBuffer.Metal().Buffer.GpuAddress + offsetInBytes;

        for (uint i = 0; i < drawCount; i++)
        {
            render?.DrawPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type, address + (uint)(sizeof(IndirectDrawArgs) * i));
        }
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        render?.DrawIndexedPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type,
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
            render?.DrawIndexedPrimitives(MTLFormats.Metal(pipeline.Desc.PrimitiveTopology).Type,
                                          indexBinding.Type,
                                          indexBinding.Address,
                                          indexBinding.LengthInBytes,
                                          address + (uint)(sizeof(IndirectDrawIndexedArgs) * i));
        }
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        compute?.DispatchThreadgroups(new MTLSize(groupCountX, groupCountY, groupCountZ), pipeline.Metal().ComputePipelineState.RequiredThreadsPerThreadgroup);
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        compute?.DispatchThreadgroups(indirectBuffer.Metal().Buffer.GpuAddress + offsetInBytes, pipeline.Metal().ComputePipelineState.RequiredThreadsPerThreadgroup);
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        render?.DrawMeshThreadgroups(new MTLSize(groupCountX, groupCountY, groupCountZ),
                                     pipeline.Metal().RenderPipelineState.RequiredThreadsPerObjectThreadgroup,
                                     pipeline.Metal().RenderPipelineState.RequiredThreadsPerMeshThreadgroup);
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        nuint address = indirectBuffer.Metal().Buffer.GpuAddress + offsetInBytes;

        for (uint i = 0; i < dispatchCount; i++)
        {
            render?.DrawMeshThreadgroups(address + (uint)(sizeof(IndirectDispatchMeshArgs) * i),
                                         pipeline.Metal().RenderPipelineState.RequiredThreadsPerObjectThreadgroup,
                                         pipeline.Metal().RenderPipelineState.RequiredThreadsPerMeshThreadgroup);
        }
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
        CommandBuffer.ResolveCounterHeap(mtlQueryHeap.CounterHeap, new(index, 1), new(mtlQueryHeap.Buffer.Buffer.GpuAddress + (sizeof(ulong) * index), sizeof(ulong)), MTLFence.Null, MTLFence.Null);
    }

    protected override void BeginDebugEventImpl(string label)
    {
        render?.PushDebugGroup(label);
        compute?.PushDebugGroup(label);
    }

    protected override void EndDebugEventImpl()
    {
        render?.PopDebugGroup();
        compute?.PopDebugGroup();
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        render?.InsertDebugSignpost(label);
        compute?.InsertDebugSignpost(label);
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

        ArgumentTable.Dispose();
        CommandBuffer.Dispose();
        CommandAllocator.Dispose();
    }

    private void BeginRenderEncoding(MTL4RenderPassDescriptor descriptor)
    {
        render = NSAutorelease.Own(CommandBuffer.MakeRenderCommandEncoder, descriptor);
        render.SetArgumentTable(ArgumentTable, MTLRenderStages.Vertex | MTLRenderStages.Fragment | MTLRenderStages.Mesh);

        if (todoScissors is not null)
        {
            SetScissors(todoScissors);

            todoScissors = null;
        }

        if (todoViewports is not null)
        {
            SetViewports(todoViewports);

            todoViewports = null;
        }

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

        if (todoStencilReference is not null)
        {
            SetStencilReference(todoStencilReference.Value);

            todoStencilReference = null;
        }

        if (todoBlendConstant is not null)
        {
            SetBlendConstant(todoBlendConstant.Value);

            todoBlendConstant = null;
        }
    }

    private void EndRenderEncoding()
    {
        render?.BarrierAfterEncoderStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        render?.EndEncoding();
        render?.Dispose();
        render = null;
    }

    private void BeginComputeEncoding()
    {
        compute = NSAutorelease.Own(CommandBuffer.MakeComputeCommandEncoder);
        compute.SetArgumentTable(ArgumentTable);

        if (todoComputePipeline is not null)
        {
            SetPipeline(todoComputePipeline);

            todoComputePipeline = null;
        }
    }

    private void EndComputeEncoding()
    {
        compute?.BarrierAfterEncoderStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        compute?.EndEncoding();
        compute?.Dispose();
        compute = null;
    }

    private struct IndexBinding(MTLIndexType type, nuint address, uint sizeInBytes, uint lengthInBytes)
    {
        public MTLIndexType Type = type;

        public nuint Address = address;

        public uint SizeInBytes = sizeInBytes;

        public uint LengthInBytes = lengthInBytes;
    }
}