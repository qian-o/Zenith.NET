using System.Numerics;
using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCommandBuffer : CommandBuffer
{
    public MTL4CommandAllocator CommandAllocator;

    public MTL4CommandBuffer CommandBuffer;

    public MTL4ArgumentTable ArgumentTable;

    private MTL4RenderCommandEncoder? render;
    private MTL4ComputeCommandEncoder? compute;

    private Scissor[]? todoScissors;
    private Viewport[]? todoViewports;

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
                      src.Metal().Texture,
                      dstSubresource.ArrayLayer,
                      dstSubresource.MipLevel,
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

    protected override void UpdateAccelerationStructureImpl(BottomLevelAccelerationStructure accelerationStructure, BottomLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void SetStencilReferenceImpl(uint stencilReference)
    {
        throw new NotImplementedException();
    }

    protected override void SetBlendConstantImpl(Vector4 blendConstant)
    {
        throw new NotImplementedException();
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint slot)
    {
        throw new NotImplementedException();
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        throw new NotImplementedException();
    }

    protected override void SetConstantBufferImpl(Pipeline pipeline, Buffer buffer, uint offsetInBytes)
    {
        throw new NotImplementedException();
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        throw new NotImplementedException();
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        throw new NotImplementedException();
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        throw new NotImplementedException();
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
    }

    private void EndComputeEncoding()
    {
        compute?.BarrierAfterEncoderStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
        compute?.EndEncoding();
        compute?.Dispose();
        compute = null;
    }
}
