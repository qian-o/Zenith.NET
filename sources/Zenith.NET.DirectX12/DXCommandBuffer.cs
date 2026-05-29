using System.Numerics;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandBuffer : CommandBuffer
{
    public ComPtr<ID3D12CommandAllocator> CommandAllocator;

    public ComPtr<ID3D12CommandList> CommandList;

    public DXCommandBuffer(DXGraphicsContext context, CommandQueue queue) : base(context, queue)
    {
        context.Device14.CreateCommandAllocator(DXFormats.DirectX12(queue.Type), SilkMarshal.GuidPtrOf<ID3D12CommandAllocator>(), (void**)CommandAllocator.GetAddressOf()).Success();

        context.Device14.CreateCommandList(0, DXFormats.DirectX12(queue.Type), CommandAllocator, default(ID3D12PipelineState*), SilkMarshal.GuidPtrOf<ID3D12CommandList>(), (void**)CommandList.GetAddressOf()).Success();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void BarrierImpl(ReadOnlySpan<MemoryBarrier> memoryBarriers, ReadOnlySpan<BufferBarrier> bufferBarriers, ReadOnlySpan<TextureBarrier> textureBarriers)
    {
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
    }

    protected override void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, TextureDataLayout dstLayout)
    {
    }

    protected override void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
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
    }

    protected override void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
    }

    protected override void EndRenderPassImpl()
    {
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
    }

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
    }

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
    }

    protected override void SetStencilReferenceImpl(uint stencilReference)
    {
    }

    protected override void SetBlendConstantImpl(Vector4 blendConstant)
    {
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint slot)
    {
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
    }

    protected override void SetConstantBufferImpl(Pipeline pipeline, Buffer buffer, uint offsetInBytes)
    {
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
    }

    protected override void BeginQueryImpl(QueryHeap queryHeap, uint index)
    {
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
    }

    protected override void BeginDebugEventImpl(string label)
    {
    }

    protected override void EndDebugEventImpl()
    {
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
    }

    protected override void BeginImpl()
    {
    }

    protected override void EndImpl()
    {
    }

    protected override void ResetImpl()
    {
    }

    protected override void SetResourceName(string name)
    {
        CommandList.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        CommandList.Dispose();
        CommandAllocator.Dispose();
    }
}
