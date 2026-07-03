using System.Numerics;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKCommandBuffer : CommandBuffer
{
    public CommandPool CommandPool;

    public VkCommandBuffer CommandBuffer;

    public VKCommandBuffer(VKGraphicsContext context, VKCommandQueue queue) : base(context, queue)
    {
        CommandPoolCreateInfo createInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queue.QueueFamilyIndex
        };

        context.Vk.CreateCommandPool(context.Device, &createInfo, default, out CommandPool).Success();

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            CommandBufferCount = 1
        };

        context.Vk.AllocateCommandBuffers(context.Device, &allocateInfo, out CommandBuffer).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void BarrierImpl(BarrierStages before, BarrierStages after)
    {
        throw new NotImplementedException();
    }

    protected override void TransitionImpl(Texture texture, TextureSubresource subresource, TextureLayout srcLayout, TextureLayout dstLayout)
    {
        throw new NotImplementedException();
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        throw new NotImplementedException();
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, uint srcRowStrideInBytes, uint srcSliceStrideInBytes, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        throw new NotImplementedException();
    }

    protected override void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
        throw new NotImplementedException();
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, uint dstRowStrideInBytes, uint dstSliceStrideInBytes)
    {
        throw new NotImplementedException();
    }

    protected override void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    protected override void EndRenderPassImpl()
    {
        throw new NotImplementedException();
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

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
        throw new NotImplementedException();
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
        throw new NotImplementedException();
    }

    protected override void SetBlendConstantImpl(Vector4 blendConstant)
    {
        throw new NotImplementedException();
    }

    protected override void SetStencilReferenceImpl(uint stencilReference)
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
        throw new NotImplementedException();
    }

    protected override void BeginDebugEventImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsLabelEXT labelInfo = new()
        {
            SType = StructureType.DebugUtilsLabelExt,
            PLabelName = (byte*)ZenithMarshal.StringToPointer(scope, label, StringEncoding.UTF8)
        };

        Context.DebugUtils?.CmdBeginDebugUtilsLabel(CommandBuffer, &labelInfo);
    }

    protected override void EndDebugEventImpl()
    {
        Context.DebugUtils?.CmdEndDebugUtilsLabel(CommandBuffer);
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsLabelEXT labelInfo = new()
        {
            SType = StructureType.DebugUtilsLabelExt,
            PLabelName = (byte*)ZenithMarshal.StringToPointer(scope, label, StringEncoding.UTF8)
        };

        Context.DebugUtils?.CmdInsertDebugUtilsLabel(CommandBuffer, &labelInfo);
    }

    protected override void BeginImpl()
    {
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        Context.Vk.BeginCommandBuffer(CommandBuffer, &beginInfo).Success();
    }

    protected override void EndImpl()
    {
        Context.Vk.EndCommandBuffer(CommandBuffer).Success();
    }

    protected override void ResetImpl()
    {
        Context.Vk.ResetCommandBuffer(CommandBuffer, CommandBufferResetFlags.None).Success();
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.CommandBuffer,
            ObjectHandle = (ulong)CommandBuffer.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        Context.Vk.DestroyCommandPool(Context.Device, CommandPool, default);
    }
}
