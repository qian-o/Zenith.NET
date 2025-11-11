using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

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

        context.Vk.CreateCommandPool(context.Device, &createInfo, null, (CommandPool*)Unsafe.AsPointer(ref CommandPool)).Success();

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        context.Vk.AllocateCommandBuffers(context.Device, &allocateInfo, (VkCommandBuffer*)Unsafe.AsPointer(ref CommandBuffer)).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
    }

    protected override void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
    }

    protected override void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice)
    {
    }

    protected override BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc)
    {
        return new VKBottomLevelAccelerationStructure(Context, desc, this);
    }

    protected override TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc)
    {
        return new VKTopLevelAccelerationStructure(Context, desc, this);
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.Vulkan().Update(this, newDesc);
    }

    protected override void BindFrameBufferImpl(FrameBuffer frameBuffer, ClearValue clearValue)
    {
    }

    protected override void BindPipelineImpl(GraphicsPipeline pipeline)
    {
    }

    protected override void BindPipelineImpl(ComputePipeline pipeline)
    {
    }

    protected override void BindPipelineImpl(RayTracingPipeline pipeline)
    {
    }

    protected override void BindPipelineImpl(MeshShadingPipeline pipeline)
    {
    }

    protected override void BindResourceSetsImpl(ResourceSet[] sets)
    {
    }

    protected override void BindVertexBuffersImpl(Buffer[] buffers, uint[] offsetsInBytes)
    {
    }

    protected override void BindIndexBufferImpl(Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
    }

    protected override void SetScissorsImpl(Scissor[] scissors)
    {
    }

    protected override void SetViewportsImpl(Viewport[] viewports)
    {
    }

    protected override void DrawImpl(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
    }

    protected override void DrawIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
    }

    protected override void DrawIndexedImpl(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
    }

    protected override void DrawIndexedIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
    }

    protected override void DispatchImpl(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
    }

    protected override void DispatchIndirectImpl(Buffer indirectBuffer, uint offsetInBytes)
    {
    }

    protected override void DispatchRaysImpl(uint width, uint height, uint depth)
    {
    }

    protected override void DispatchMeshImpl(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
    }

    protected override void DispatchMeshIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
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

    protected override void BeginRenderingImpl(FrameBuffer frameBuffer)
    {
        frameBuffer.Vulkan().TransitionLayout(this);

        Context.Vk.CmdBeginRendering(CommandBuffer, (RenderingInfo*)Unsafe.AsPointer(ref frameBuffer.Vulkan().RenderingInfo));
    }

    protected override void EndRenderingImpl()
    {
        Context.Vk.CmdEndRendering(CommandBuffer);
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

        Context.Vk.DestroyCommandPool(Context.Device, CommandPool, null);
    }
}
