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
        (PipelineStageFlags2 srcStage, AccessFlags2 srcAccess) = VKFormats.Vulkan(before);
        (PipelineStageFlags2 dstStage, AccessFlags2 dstAccess) = VKFormats.Vulkan(after);

        MemoryBarrier2 memoryBarrier = new()
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = srcStage,
            SrcAccessMask = srcAccess,
            DstStageMask = dstStage,
            DstAccessMask = dstAccess
        };

        DependencyInfo dependencyInfo = new()
        {
            SType = StructureType.DependencyInfo,
            MemoryBarrierCount = 1,
            PMemoryBarriers = &memoryBarrier
        };

        Context.Vk.CmdPipelineBarrier2(CommandBuffer, &dependencyInfo);
    }

    protected override void TransitionImpl(Texture texture, TextureSubresource subresource, TextureLayout before, TextureLayout after)
    {
        VKTexture vkTexture = texture.Vulkan();

        (PipelineStageFlags2 srcStage, AccessFlags2 srcAccess, ImageLayout oldLayout) = VKFormats.Vulkan(before);
        (PipelineStageFlags2 dstStage, AccessFlags2 dstAccess, ImageLayout newLayout) = VKFormats.Vulkan(after);

        ImageMemoryBarrier2 imageMemoryBarrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = srcStage,
            SrcAccessMask = srcAccess,
            DstStageMask = dstStage,
            DstAccessMask = dstAccess,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = vkTexture.Image,
            SubresourceRange = new()
            {
                AspectMask = VKFormats.Vulkan(vkTexture.Desc.Format).AspectFlags,
                BaseMipLevel = subresource.MipLevel,
                LevelCount = 1,
                BaseArrayLayer = subresource.ArrayLayer,
                LayerCount = 1
            }
        };

        DependencyInfo dependencyInfo = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &imageMemoryBarrier
        };

        Context.Vk.CmdPipelineBarrier2(CommandBuffer, &dependencyInfo);
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        VKBuffer vkSrc = src.Vulkan();
        VKBuffer vkDst = dst.Vulkan();

        BufferCopy2 region = new()
        {
            SType = StructureType.BufferCopy2,
            SrcOffset = srcOffsetInBytes,
            DstOffset = dstOffsetInBytes,
            Size = sizeInBytes
        };

        CopyBufferInfo2 copyBufferInfo = new()
        {
            SType = StructureType.CopyBufferInfo2,
            SrcBuffer = vkSrc.Buffer,
            DstBuffer = vkDst.Buffer,
            RegionCount = 1,
            PRegions = &region
        };

        Context.Vk.CmdCopyBuffer2(CommandBuffer, &copyBufferInfo);
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, uint srcRowStrideInBytes, uint srcSliceStrideInBytes, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        VKBuffer vkSrc = src.Vulkan();
        VKTexture vkDst = dst.Vulkan();

        (uint blockWidth, uint blockHeight, _, _) = ZenithHelper.BlockLayout(vkDst.Desc.Format, dstExtent.Width, dstExtent.Height);

        BufferImageCopy2 region = new()
        {
            SType = StructureType.BufferImageCopy2,
            BufferOffset = srcOffsetInBytes,
            BufferRowLength = srcRowStrideInBytes / ZenithHelper.SizeInBytes(vkDst.Desc.Format) * blockWidth,
            BufferImageHeight = srcSliceStrideInBytes / srcRowStrideInBytes * blockHeight,
            ImageSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkDst.Desc.Format).AspectFlags,
                MipLevel = dstSubresource.MipLevel,
                BaseArrayLayer = dstSubresource.ArrayLayer,
                LayerCount = 1
            },
            ImageOffset = new()
            {
                X = (int)dstOffset.X,
                Y = (int)dstOffset.Y,
                Z = (int)dstOffset.Z
            },
            ImageExtent = new()
            {
                Width = dstExtent.Width,
                Height = dstExtent.Height,
                Depth = dstExtent.Depth
            }
        };

        CopyBufferToImageInfo2 copyBufferToImageInfo = new()
        {
            SType = StructureType.CopyBufferToImageInfo2,
            SrcBuffer = vkSrc.Buffer,
            DstImage = vkDst.Image,
            DstImageLayout = ImageLayout.TransferDstOptimal,
            RegionCount = 1,
            PRegions = &region
        };

        Context.Vk.CmdCopyBufferToImage2(CommandBuffer, &copyBufferToImageInfo);
    }

    protected override void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
        VKTexture vkSrc = src.Vulkan();
        VKTexture vkDst = dst.Vulkan();

        ImageCopy2 region = new()
        {
            SType = StructureType.ImageCopy2,
            SrcSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Format).AspectFlags,
                MipLevel = srcSubresource.MipLevel,
                BaseArrayLayer = srcSubresource.ArrayLayer,
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
                AspectMask = VKFormats.Vulkan(vkDst.Desc.Format).AspectFlags,
                MipLevel = dstSubresource.MipLevel,
                BaseArrayLayer = dstSubresource.ArrayLayer,
                LayerCount = 1
            },
            DstOffset = new()
            {
                X = (int)dstOffset.X,
                Y = (int)dstOffset.Y,
                Z = (int)dstOffset.Z
            },
            Extent = new()
            {
                Width = extent.Width,
                Height = extent.Height,
                Depth = extent.Depth
            }
        };

        CopyImageInfo2 copyImageInfo = new()
        {
            SType = StructureType.CopyImageInfo2,
            SrcImage = vkSrc.Image,
            SrcImageLayout = ImageLayout.TransferSrcOptimal,
            DstImage = vkDst.Image,
            DstImageLayout = ImageLayout.TransferDstOptimal,
            RegionCount = 1,
            PRegions = &region
        };

        Context.Vk.CmdCopyImage2(CommandBuffer, &copyImageInfo);
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, uint dstRowStrideInBytes, uint dstSliceStrideInBytes)
    {
        VKTexture vkSrc = src.Vulkan();
        VKBuffer vkDst = dst.Vulkan();

        (uint blockWidth, uint blockHeight, _, _) = ZenithHelper.BlockLayout(vkSrc.Desc.Format, srcExtent.Width, srcExtent.Height);

        BufferImageCopy2 region = new()
        {
            SType = StructureType.BufferImageCopy2,
            BufferOffset = dstOffsetInBytes,
            BufferRowLength = dstRowStrideInBytes / ZenithHelper.SizeInBytes(vkSrc.Desc.Format) * blockWidth,
            BufferImageHeight = dstSliceStrideInBytes / dstRowStrideInBytes * blockHeight,
            ImageSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Format).AspectFlags,
                MipLevel = srcSubresource.MipLevel,
                BaseArrayLayer = srcSubresource.ArrayLayer,
                LayerCount = 1
            },
            ImageOffset = new()
            {
                X = (int)srcOffset.X,
                Y = (int)srcOffset.Y,
                Z = (int)srcOffset.Z
            },
            ImageExtent = new()
            {
                Width = srcExtent.Width,
                Height = srcExtent.Height,
                Depth = srcExtent.Depth
            }
        };

        CopyImageToBufferInfo2 copyImageToBufferInfo = new()
        {
            SType = StructureType.CopyImageToBufferInfo2,
            SrcImage = vkSrc.Image,
            SrcImageLayout = ImageLayout.TransferSrcOptimal,
            DstBuffer = vkDst.Buffer,
            RegionCount = 1,
            PRegions = &region
        };

        Context.Vk.CmdCopyImageToBuffer2(CommandBuffer, &copyImageToBufferInfo);
    }

    protected override void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
        VKTexture vkSrc = src.Vulkan();
        VKTexture vkDst = dst.Vulkan();

        ZenithHelper.MipDimensions(vkSrc.Desc.Width, vkSrc.Desc.Height, vkSrc.Desc.Depth, srcSubresource.MipLevel, out uint mipWidth, out uint mipHeight, out uint mipDepth);

        ImageResolve2 region = new()
        {
            SType = StructureType.ImageResolve2,
            SrcSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Format).AspectFlags,
                MipLevel = srcSubresource.MipLevel,
                BaseArrayLayer = srcSubresource.ArrayLayer,
                LayerCount = 1
            },
            DstSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkDst.Desc.Format).AspectFlags,
                MipLevel = dstSubresource.MipLevel,
                BaseArrayLayer = dstSubresource.ArrayLayer,
                LayerCount = 1
            },
            Extent = new()
            {
                Width = mipWidth,
                Height = mipHeight,
                Depth = mipDepth
            }
        };

        ResolveImageInfo2 resolveImageInfo = new()
        {
            SType = StructureType.ResolveImageInfo2,
            SrcImage = vkSrc.Image,
            SrcImageLayout = ImageLayout.TransferSrcOptimal,
            DstImage = vkDst.Image,
            DstImageLayout = ImageLayout.TransferDstOptimal,
            RegionCount = 1,
            PRegions = &region
        };

        Context.Vk.CmdResolveImage2(CommandBuffer, &resolveImageInfo);
    }

    protected override BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc)
    {
        return new VKBottomLevelAccelerationStructure(Context, this, desc);
    }

    protected override TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc)
    {
        return new VKTopLevelAccelerationStructure(Context, this, desc);
    }

    protected override void UpdateAccelerationStructureImpl(BottomLevelAccelerationStructure accelerationStructure, BottomLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.Vulkan().Update(this, newDesc);
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.Vulkan().Update(this, newDesc);
    }

    protected override void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
        uint width = 0;
        uint height = 0;

        RenderingAttachmentInfo* pColorAttachments = stackalloc RenderingAttachmentInfo[colorAttachments.Length];
        for (int i = 0; i < colorAttachments.Length; i++)
        {
            ColorAttachment attachment = colorAttachments[i];

            VKTexture texture = attachment.Texture.Vulkan();

            ZenithHelper.MipDimensions(texture.Desc.Width, texture.Desc.Height, texture.Desc.Depth, attachment.Subresource.MipLevel, out width, out height, out _);

            pColorAttachments[i] = new()
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = texture.GetAttachmentView(attachment.Subresource),
                ImageLayout = ImageLayout.ColorAttachmentOptimal,
                LoadOp = VKFormats.Vulkan(attachment.LoadOp),
                StoreOp = VKFormats.Vulkan(attachment.StoreOp),
                ClearValue = new()
                {
                    Color = new()
                    {
                        Float32_0 = attachment.ClearColor.X,
                        Float32_1 = attachment.ClearColor.Y,
                        Float32_2 = attachment.ClearColor.Z,
                        Float32_3 = attachment.ClearColor.W
                    }
                }
            };
        }

        RenderingAttachmentInfo* pDepthAttachment = stackalloc RenderingAttachmentInfo[depthStencilAttachment.HasValue ? 1 : 0];
        RenderingAttachmentInfo* pStencilAttachment = stackalloc RenderingAttachmentInfo[depthStencilAttachment.HasValue ? 1 : 0];
        if (depthStencilAttachment.HasValue)
        {
            DepthStencilAttachment attachment = depthStencilAttachment.Value;

            VKTexture texture = attachment.Texture.Vulkan();

            ZenithHelper.MipDimensions(texture.Desc.Width, texture.Desc.Height, texture.Desc.Depth, attachment.Subresource.MipLevel, out width, out height, out _);

            if (ZenithHelper.HasDepth(texture.Desc.Format))
            {
                pDepthAttachment[0] = new()
                {
                    SType = StructureType.RenderingAttachmentInfo,
                    ImageView = texture.GetAttachmentView(attachment.Subresource),
                    ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                    LoadOp = VKFormats.Vulkan(attachment.DepthLoadOp),
                    StoreOp = VKFormats.Vulkan(attachment.DepthStoreOp),
                    ClearValue = new()
                    {
                        DepthStencil = new()
                        {
                            Depth = attachment.ClearDepth,
                            Stencil = attachment.ClearStencil
                        }
                    }
                };
            }
            else
            {
                pDepthAttachment = null;
            }

            if (ZenithHelper.HasStencil(texture.Desc.Format))
            {
                pStencilAttachment[0] = new()
                {
                    SType = StructureType.RenderingAttachmentInfo,
                    ImageView = texture.GetAttachmentView(attachment.Subresource),
                    ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                    LoadOp = VKFormats.Vulkan(attachment.StencilLoadOp),
                    StoreOp = VKFormats.Vulkan(attachment.StencilStoreOp),
                    ClearValue = new()
                    {
                        DepthStencil = new()
                        {
                            Depth = attachment.ClearDepth,
                            Stencil = attachment.ClearStencil
                        }
                    }
                };
            }
            else
            {
                pStencilAttachment = null;
            }
        }

        RenderingInfo renderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new()
            {
                Extent = new()
                {
                    Width = width,
                    Height = height
                }
            },
            LayerCount = 1,
            ColorAttachmentCount = (uint)colorAttachments.Length,
            PColorAttachments = pColorAttachments,
            PDepthAttachment = pDepthAttachment,
            PStencilAttachment = pStencilAttachment
        };

        Context.Vk.CmdBeginRendering(CommandBuffer, &renderingInfo);
    }

    protected override void EndRenderPassImpl()
    {
        Context.Vk.CmdEndRendering(CommandBuffer);
    }

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline.Vulkan().Pipeline);
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Compute, pipeline.Vulkan().Pipeline);
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        Context.Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline.Vulkan().Pipeline);
    }

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
        VkViewport* pViewports = stackalloc VkViewport[viewports.Length];
        for (int i = 0; i < viewports.Length; i++)
        {
            Viewport viewport = viewports[i];

            pViewports[i] = new()
            {
                X = viewport.X,
                Y = viewport.Y + viewport.Height,
                Width = viewport.Width,
                Height = -viewport.Height,
                MinDepth = viewport.MinDepth,
                MaxDepth = viewport.MaxDepth
            };
        }

        Context.Vk.CmdSetViewport(CommandBuffer, 0, (uint)viewports.Length, pViewports);
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
        Rect2D* pScissors = stackalloc Rect2D[scissors.Length];
        for (int i = 0; i < scissors.Length; i++)
        {
            Scissor scissor = scissors[i];

            pScissors[i] = new()
            {
                Offset = new()
                {
                    X = scissor.X,
                    Y = scissor.Y
                },
                Extent = new()
                {
                    Width = scissor.Width,
                    Height = scissor.Height
                }
            };
        }

        Context.Vk.CmdSetScissor(CommandBuffer, 0, (uint)scissors.Length, pScissors);
    }

    protected override void SetBlendConstantImpl(Vector4 blendConstant)
    {
        Context.Vk.CmdSetBlendConstants(CommandBuffer, &blendConstant.X);
    }

    protected override void SetStencilReferenceImpl(uint stencilReference)
    {
        Context.Vk.CmdSetStencilReference(CommandBuffer, StencilFaceFlags.FrontAndBack, stencilReference);
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint slot)
    {
        Context.Vk.CmdBindVertexBuffers(CommandBuffer, slot, 1, [buffer.Vulkan().Buffer], [offsetInBytes]);
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        Context.Vk.CmdBindIndexBuffer(CommandBuffer, buffer.Vulkan().Buffer, offsetInBytes, VKFormats.Vulkan(indexFormat));
    }

    protected override void SetConstantBufferImpl(Pipeline pipeline, Buffer buffer, uint offsetInBytes)
    {
        ulong address = buffer.Vulkan().DeviceAddress + offsetInBytes;

        PushDataInfoEXT pushDataInfo = new()
        {
            SType = StructureType.PushDataInfoExt(),
            Data = new()
            {
                Address = &address,
                Size = sizeof(ulong)
            }
        };

        Context.DescriptorHeap?.CmdPushData(CommandBuffer, &pushDataInfo);
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
        VKQueryHeap vkQueryHeap = queryHeap.Vulkan();

        Context.Vk.CmdResetQueryPool(CommandBuffer, vkQueryHeap.QueryPool, index, 1);
        Context.Vk.CmdBeginQuery(CommandBuffer, vkQueryHeap.QueryPool, index, queryHeap.Desc.Type is QueryType.Occlusion ? QueryControlFlags.PreciseBit : QueryControlFlags.None);
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
        Context.Vk.CmdEndQuery(CommandBuffer, queryHeap.Vulkan().QueryPool, index);
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
        VKQueryHeap vkQueryHeap = queryHeap.Vulkan();

        Context.Vk.CmdResetQueryPool(CommandBuffer, vkQueryHeap.QueryPool, index, 1);
        Context.Vk.CmdWriteTimestamp2(CommandBuffer, PipelineStageFlags2.BottomOfPipeBit, vkQueryHeap.QueryPool, index);
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

        if (Queue.Type is CommandQueueType.Transfer)
        {
            return;
        }

        BindHeapInfoEXT resourceBindInfo = new()
        {
            SType = StructureType.BindHeapInfoExt(),
            HeapRange = Context.ResourceHeap.Range,
            ReservedRangeSize = Context.ResourceHeap.ReservedBytes
        };

        Context.DescriptorHeap?.CmdBindResourceHeap(CommandBuffer, &resourceBindInfo);

        BindHeapInfoEXT samplerBindInfo = new()
        {
            SType = StructureType.BindHeapInfoExt(),
            HeapRange = Context.SamplerHeap.Range,
            ReservedRangeSize = Context.SamplerHeap.ReservedBytes
        };

        Context.DescriptorHeap?.CmdBindSamplerHeap(CommandBuffer, &samplerBindInfo);
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
