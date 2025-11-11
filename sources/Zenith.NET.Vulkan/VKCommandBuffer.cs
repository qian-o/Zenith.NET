using System.Numerics;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKCommandBuffer : CommandBuffer
{
    public CommandPool CommandPool;

    public VkCommandBuffer CommandBuffer;

    private PipelineBindPoint currentPipelineBindPoint;
    private PipelineLayout currentPipelineLayout;

    public VKCommandBuffer(VKGraphicsContext context, VKCommandQueue queue) : base(context, queue)
    {
        CommandPoolCreateInfo createInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queue.QueueFamilyIndex
        };

        context.Vk.CreateCommandPool(context.Device, &createInfo, null, out CommandPool).Success();

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = CommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };

        context.Vk.AllocateCommandBuffers(context.Device, &allocateInfo, out CommandBuffer).Success();
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

    protected override void BindPipelineImpl(GraphicsPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline.Vulkan().Pipeline);

        currentPipelineBindPoint = PipelineBindPoint.Graphics;
        currentPipelineLayout = pipeline.Vulkan().PipelineLayout;
    }

    protected override void BindPipelineImpl(ComputePipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Compute, pipeline.Vulkan().Pipeline);

        currentPipelineBindPoint = PipelineBindPoint.Compute;
        currentPipelineLayout = pipeline.Vulkan().PipelineLayout;
    }

    protected override void BindPipelineImpl(RayTracingPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.RayTracingKhr, pipeline.Vulkan().Pipeline);

        currentPipelineBindPoint = PipelineBindPoint.RayTracingKhr;
        currentPipelineLayout = pipeline.Vulkan().PipelineLayout;
    }

    protected override void BindPipelineImpl(MeshShadingPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline.Vulkan().Pipeline);

        currentPipelineBindPoint = PipelineBindPoint.Graphics;
        currentPipelineLayout = pipeline.Vulkan().PipelineLayout;
    }

    protected override void BindResourceSetsImpl(Pipeline pipeline, ResourceSet[] sets)
    {
        VKResourceSet[] vkSets = [.. sets.Select(static item => item.Vulkan())];
        DescriptorSet[] vkDescriptorSets = [.. vkSets.Select(static item => item.DescriptorToken.DescriptorSet)];

        foreach (VKResourceSet vkSet in vkSets)
        {
            vkSet.TransitionLayout(this);
        }

        Context.Vk.CmdBindDescriptorSets(CommandBuffer, currentPipelineBindPoint, currentPipelineLayout, 0, (uint)vkDescriptorSets.Length, ref vkDescriptorSets[0], 0, null);
    }

    protected override void BindVertexBuffersImpl(GraphicsPipeline pipeline, Buffer[] buffers, uint[] offsetsInBytes)
    {
        VkBuffer[] vkBuffers = [.. buffers.Select(static item => item.Vulkan().Buffer)];
        ulong[] vkOffsets = [.. offsetsInBytes.Select(static item => (ulong)item)];

        Context.Vk.CmdBindVertexBuffers(CommandBuffer, 0, (uint)vkBuffers.Length, ref vkBuffers[0], ref vkOffsets[0]);
    }

    protected override void BindIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        Context.Vk.CmdBindIndexBuffer(CommandBuffer, buffer.Vulkan().Buffer, offsetInBytes, VKFormats.Vulkan(format));
    }

    protected override void SetScissorsImpl(Scissor[] scissors)
    {
        Rect2D[] vkScissors = [.. scissors.Select(static item => new Rect2D(new(item.X, item.Y), new(item.Width, item.Height)))];

        Context.Vk.CmdSetScissor(CommandBuffer, 0, (uint)vkScissors.Length, ref vkScissors[0]);
    }

    protected override void SetViewportsImpl(Viewport[] viewports)
    {
        VkViewport[] vkViewports = [.. viewports.Select(static item => new VkViewport(item.X, item.Y, item.Width, -item.Height, item.MinDepth, item.MaxDepth))];

        Context.Vk.CmdSetViewport(CommandBuffer, 0, (uint)vkViewports.Length, ref vkViewports[0]);
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        Context.Vk.CmdDraw(CommandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        Context.Vk.CmdDrawIndirect(CommandBuffer, indirectBuffer.Vulkan().Buffer, offsetInBytes, drawCount, (uint)sizeof(IndirectDrawArgs));
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        Context.Vk.CmdDrawIndexed(CommandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        Context.Vk.CmdDrawIndexedIndirect(CommandBuffer, indirectBuffer.Vulkan().Buffer, offsetInBytes, drawCount, (uint)sizeof(IndirectDrawIndexedArgs));
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        Context.Vk.CmdDispatch(CommandBuffer, groupCountX, groupCountY, groupCountZ);
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        Context.Vk.CmdDispatchIndirect(CommandBuffer, indirectBuffer.Vulkan().Buffer, offsetInBytes);
    }

    protected override void DispatchRaysImpl(RayTracingPipeline pipeline, uint width, uint height, uint depth)
    {
        VKRayTracingPipeline vkPipeline = pipeline.Vulkan();

        StridedDeviceAddressRegionKHR rayGenerationRegion = vkPipeline.RayGenerationRegion;
        StridedDeviceAddressRegionKHR missRegion = vkPipeline.MissRegion;
        StridedDeviceAddressRegionKHR hitGroupsRegion = vkPipeline.HitGroupsRegion;
        StridedDeviceAddressRegionKHR callableRegion = new();

        Context.RayTracingPipeline?.CmdTraceRays(CommandBuffer, &rayGenerationRegion, &missRegion, &hitGroupsRegion, &callableRegion, width, height, depth);
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        Context.MeshShader?.CmdDrawMeshTask(CommandBuffer, groupCountX, groupCountY, groupCountZ);
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        Context.MeshShader?.CmdDrawMeshTasksIndirect(CommandBuffer, indirectBuffer.Vulkan().Buffer, offsetInBytes, dispatchCount, (uint)sizeof(IndirectDispatchMeshArgs));
    }

    protected override void BeginQueryImpl(QueryHeap queryHeap, uint index)
    {
        Context.Vk.CmdResetQueryPool(CommandBuffer, queryHeap.Vulkan().QueryPool, index, 1);

        Context.Vk.CmdBeginQuery(CommandBuffer, queryHeap.Vulkan().QueryPool, index, queryHeap.Desc.Type is QueryType.Occlusion ? QueryControlFlags.PreciseBit : QueryControlFlags.None);
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
        Context.Vk.CmdEndQuery(CommandBuffer, queryHeap.Vulkan().QueryPool, index);
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
        Context.Vk.CmdResetQueryPool(CommandBuffer, queryHeap.Vulkan().QueryPool, index, 1);

        Context.Vk.CmdWriteTimestamp(CommandBuffer, PipelineStageFlags.BottomOfPipeBit, queryHeap.Vulkan().QueryPool, index);
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

    protected override void BeginRenderingImpl(FrameBuffer frameBuffer, ClearValue? clearValue)
    {
        VKFrameBuffer vkFrameBuffer = frameBuffer.Vulkan();

        vkFrameBuffer.TransitionLayout(this);

        Context.Vk.CmdBeginRendering(CommandBuffer, ref vkFrameBuffer.RenderingInfo);

        if (clearValue.HasValue)
        {
            bool clearColor = clearValue.Value.Flags.HasFlag(ClearFlags.Color);
            bool clearDepth = clearValue.Value.Flags.HasFlag(ClearFlags.Depth);
            bool clearStencil = clearValue.Value.Flags.HasFlag(ClearFlags.Stencil);

            ClearRect rect = new()
            {
                Rect = new()
                {
                    Extent = new()
                    {
                        Width = vkFrameBuffer.Width,
                        Height = vkFrameBuffer.Height
                    }
                },
                LayerCount = 1
            };

            if (clearColor)
            {
                for (int i = 0; i < clearValue.Value.ColorValues.Length; i++)
                {
                    Vector4 color = clearValue.Value.ColorValues[i];

                    ClearAttachment attachment = new()
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        ColorAttachment = (uint)i,
                        ClearValue = new()
                        {
                            Color = new()
                            {
                                Float32_0 = color.X,
                                Float32_1 = color.Y,
                                Float32_2 = color.Z,
                                Float32_3 = color.W
                            }
                        }
                    };

                    Context.Vk.CmdClearAttachments(CommandBuffer, 1, &attachment, 1, &rect);
                }
            }

            if (clearDepth || clearStencil)
            {
                ClearAttachment attachment = new()
                {
                    AspectMask = (clearDepth ? ImageAspectFlags.DepthBit : 0) | (clearStencil ? ImageAspectFlags.StencilBit : 0),
                    ClearValue = new()
                    {
                        DepthStencil = new()
                        {
                            Depth = clearValue.Value.Depth,
                            Stencil = clearValue.Value.Stencil
                        }
                    }
                };

                Context.Vk.CmdClearAttachments(CommandBuffer, 1, &attachment, 1, &rect);
            }
        }
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
