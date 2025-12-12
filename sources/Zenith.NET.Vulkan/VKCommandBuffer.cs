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
        VKBuffer vkSrc = src.Vulkan();
        VKBuffer vkDest = dest.Vulkan();

        BufferCopy copyRegion = new()
        {
            SrcOffset = srcOffsetInBytes,
            DstOffset = destOffsetInBytes,
            Size = sizeInBytes
        };

        Context.Vk.CmdCopyBuffer(CommandBuffer, vkSrc.Buffer, vkDest.Buffer, 1, &copyRegion);

        MemoryBarrier barrier = new()
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = AccessFlags.TransferReadBit
        };

        Context.Vk.CmdPipelineBarrier(CommandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit, 0, 1, &barrier, 0, null, 0, null);
    }

    protected override void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        VKTexture vkSrc = src.Vulkan();
        VKTexture vkDest = dest.Vulkan();

        ImageLayout srcOldLayout = vkSrc.Layouts[ZenithHelper.SubresourceIndex(vkSrc.Desc, srcSlice)];
        ImageLayout destOldLayout = vkDest.Layouts[ZenithHelper.SubresourceIndex(vkDest.Desc, destSlice)];

        vkSrc.TransitionLayout(this, srcSlice.MipLevel, 1, srcSlice.ArrayLayer, 1, srcSlice.Face, 1, ImageLayout.TransferSrcOptimal);
        vkDest.TransitionLayout(this, destSlice.MipLevel, 1, destSlice.ArrayLayer, 1, destSlice.Face, 1, ImageLayout.TransferDstOptimal);

        ImageCopy imageCopy = new()
        {
            SrcSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Flags).ImageAspectFlags,
                MipLevel = srcSlice.MipLevel,
                BaseArrayLayer = ZenithHelper.FlattenArrayLayerIndex(vkSrc.Desc, srcSlice),
                LayerCount = 1
            },
            SrcOffset = new()
            {
                X = (int)srcOffset.X,
                Y = (int)srcOffset.Y,
                Z = (int)srcOffset.Z
            },
            DstSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkDest.Desc.Flags).ImageAspectFlags,
                MipLevel = destSlice.MipLevel,
                BaseArrayLayer = ZenithHelper.FlattenArrayLayerIndex(vkDest.Desc, destSlice),
                LayerCount = 1
            },
            DstOffset = new()
            {
                X = (int)destOffset.X,
                Y = (int)destOffset.Y,
                Z = (int)destOffset.Z
            },
            Extent = new()
            {
                Width = extent.Width,
                Height = extent.Height,
                Depth = extent.Depth
            }
        };

        Context.Vk.CmdCopyImage(CommandBuffer, vkSrc.Image, ImageLayout.TransferSrcOptimal, vkDest.Image, ImageLayout.TransferDstOptimal, 1, &imageCopy);

        vkSrc.TransitionLayout(this, srcSlice.MipLevel, 1, srcSlice.ArrayLayer, 1, srcSlice.Face, 1, srcOldLayout);
        vkDest.TransitionLayout(this, destSlice.MipLevel, 1, destSlice.ArrayLayer, 1, destSlice.Face, 1, destOldLayout);
    }

    protected override void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice)
    {
        VKTexture vkSrc = src.Vulkan();
        VKTexture vkDest = dest.Vulkan();

        ImageLayout srcOldLayout = vkSrc.Layouts[ZenithHelper.SubresourceIndex(vkSrc.Desc, srcSlice)];
        ImageLayout destOldLayout = vkDest.Layouts[ZenithHelper.SubresourceIndex(vkDest.Desc, destSlice)];

        vkSrc.TransitionLayout(this, srcSlice.MipLevel, 1, srcSlice.ArrayLayer, 1, srcSlice.Face, 1, ImageLayout.TransferSrcOptimal);
        vkDest.TransitionLayout(this, destSlice.MipLevel, 1, destSlice.ArrayLayer, 1, destSlice.Face, 1, ImageLayout.TransferDstOptimal);

        ZenithHelper.MipDimensions(vkDest.Desc.Width, vkDest.Desc.Height, vkDest.Desc.Depth, destSlice.MipLevel, out uint mipWidth, out uint mipHeight, out uint mipDepth);

        ImageResolve imageResolve = new()
        {
            SrcSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Flags).ImageAspectFlags,
                MipLevel = srcSlice.MipLevel,
                BaseArrayLayer = ZenithHelper.FlattenArrayLayerIndex(vkSrc.Desc, srcSlice),
                LayerCount = 1
            },
            DstSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkDest.Desc.Flags).ImageAspectFlags,
                MipLevel = destSlice.MipLevel,
                BaseArrayLayer = ZenithHelper.FlattenArrayLayerIndex(vkDest.Desc, destSlice),
                LayerCount = 1
            },
            Extent = new()
            {
                Width = mipWidth,
                Height = mipHeight,
                Depth = mipDepth
            }
        };

        Context.Vk.CmdResolveImage(CommandBuffer, vkSrc.Image, ImageLayout.TransferSrcOptimal, vkDest.Image, ImageLayout.TransferDstOptimal, 1, &imageResolve);

        vkSrc.TransitionLayout(this, srcSlice.MipLevel, 1, srcSlice.ArrayLayer, 1, srcSlice.Face, 1, srcOldLayout);
        vkDest.TransitionLayout(this, destSlice.MipLevel, 1, destSlice.ArrayLayer, 1, destSlice.Face, 1, destOldLayout);
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

    protected override void PreprocessResourceSetsImpl(ResourceSet[] resourceSets)
    {
        foreach (ResourceSet resourceSet in resourceSets)
        {
            resourceSet.Vulkan().TransitionLayout(this);
        }
    }

    protected override void SetScissorsImpl(Scissor[] scissors)
    {
        Rect2D[] vkScissors = [.. scissors.Select(static item => new Rect2D(new(item.X, item.Y), new(item.Width, item.Height)))];

        Context.Vk.CmdSetScissor(CommandBuffer, 0, (uint)vkScissors.Length, ref vkScissors[0]);
    }

    protected override void SetViewportsImpl(Viewport[] viewports)
    {
        VkViewport[] vkViewports = [.. viewports.Select(static item => new VkViewport(item.X, item.Y + item.Height, item.Width, -item.Height, item.MinDepth, item.MaxDepth))];

        Context.Vk.CmdSetViewport(CommandBuffer, 0, (uint)vkViewports.Length, ref vkViewports[0]);
    }

    protected override void BindPipelineImpl(GraphicsPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline.Vulkan().Pipeline);
    }

    protected override void BindPipelineImpl(ComputePipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Compute, pipeline.Vulkan().Pipeline);
    }

    protected override void BindPipelineImpl(RayTracingPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.RayTracingKhr, pipeline.Vulkan().Pipeline);
    }

    protected override void BindPipelineImpl(MeshShadingPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline.Vulkan().Pipeline);
    }

    protected override void BindVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index)
    {
        VkBuffer vkBuffer = buffer.Vulkan().Buffer;
        ulong vkOffset = offsetInBytes;

        Context.Vk.CmdBindVertexBuffers(CommandBuffer, index, 1, ref vkBuffer, ref vkOffset);
    }

    protected override void BindIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        Context.Vk.CmdBindIndexBuffer(CommandBuffer, buffer.Vulkan().Buffer, offsetInBytes, VKFormats.Vulkan(format));
    }

    protected override void BindResourceSetImpl(Pipeline pipeline, ResourceSet resourceSet, uint index)
    {
        (PipelineBindPoint pipelineBindPoint, PipelineLayout pipelineLayout) = pipeline switch
        {
            GraphicsPipeline graphicsPipeline => (PipelineBindPoint.Graphics, graphicsPipeline.Vulkan().PipelineLayout),
            ComputePipeline computePipeline => (PipelineBindPoint.Compute, computePipeline.Vulkan().PipelineLayout),
            RayTracingPipeline rayTracingPipeline => (PipelineBindPoint.RayTracingKhr, rayTracingPipeline.Vulkan().PipelineLayout),
            MeshShadingPipeline meshShadingPipeline => (PipelineBindPoint.Graphics, meshShadingPipeline.Vulkan().PipelineLayout),
            _ => (PipelineBindPoint.Graphics, default)
        };

        Context.Vk.CmdBindDescriptorSets(CommandBuffer, pipelineBindPoint, pipelineLayout, index, 1, ref resourceSet.Vulkan().DescriptorToken.Set, 0, null);
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

        StridedDeviceAddressRegionKHR callableRegion = new();

        Context.RayTracingPipeline?.CmdTraceRays(CommandBuffer,
                                                 ref vkPipeline.RayGenerationRegion,
                                                 ref vkPipeline.MissRegion,
                                                 ref vkPipeline.HitGroupsRegion,
                                                 &callableRegion,
                                                 width,
                                                 height,
                                                 depth);
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

        vkFrameBuffer.PrepareAttachmentsForRendering(this);

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
                for (int i = 0; i < vkFrameBuffer.ColorAttachmentCount; i++)
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

            if ((clearDepth || clearStencil) && vkFrameBuffer.HasDepthStencilAttachment)
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

    protected override void EndRenderingImpl(FrameBuffer frameBuffer)
    {
        Context.Vk.CmdEndRendering(CommandBuffer);

        frameBuffer.Vulkan().FinalizeColorAttachmentsForPresent(this);
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
