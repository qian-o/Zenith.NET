using System.Numerics;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKCommandBuffer : CommandBuffer
{
    private readonly List<ImageView> renderPassImageViews = [];

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

    protected override void CopyBufferToTextureImpl(Buffer src,
                                                    uint srcOffsetInBytes,
                                                    TextureDataLayout srcLayout,
                                                    Texture dest,
                                                    TextureSubresource destSubresource,
                                                    Offset3D destOffset,
                                                    Extent3D destExtent)
    {
        VKBuffer vkSrc = src.Vulkan();
        VKTexture vkDest = dest.Vulkan();

        (uint blockWidth, uint blockHeight, _, _) = ZenithHelper.BlockLayout(vkDest.Desc.Format, destExtent.Width, destExtent.Height);

        uint formatSizeInBytes = ZenithHelper.SizeInBytes(vkDest.Desc.Format);

        BufferImageCopy bufferImageCopy = new()
        {
            BufferOffset = srcOffsetInBytes,
            BufferRowLength = srcLayout.RowPitchInBytes / formatSizeInBytes * blockWidth,
            BufferImageHeight = srcLayout.SlicePitchInBytes / srcLayout.RowPitchInBytes * blockHeight,
            ImageSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkDest.Desc.Format, vkDest.Desc.Flags).AspectFlags,
                MipLevel = destSubresource.MipLevel,
                BaseArrayLayer = destSubresource.ArrayLayer,
                LayerCount = 1
            },
            ImageOffset = new()
            {
                X = (int)destOffset.X,
                Y = (int)destOffset.Y,
                Z = (int)destOffset.Z
            },
            ImageExtent = new()
            {
                Width = destExtent.Width,
                Height = destExtent.Height,
                Depth = destExtent.Depth
            }
        };

        Context.Vk.CmdCopyBufferToImage(CommandBuffer, vkSrc.Buffer, vkDest.Image, ImageLayout.TransferDstOptimal, 1, &bufferImageCopy);
    }

    protected override void CopyTextureImpl(Texture src,
                                            TextureSubresource srcSubresource,
                                            Offset3D srcOffset,
                                            Texture dest,
                                            TextureSubresource destSubresource,
                                            Offset3D destOffset,
                                            Extent3D extent)
    {
        VKTexture vkSrc = src.Vulkan();
        VKTexture vkDest = dest.Vulkan();

        ImageCopy imageCopy = new()
        {
            SrcSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Format, vkSrc.Desc.Flags).AspectFlags,
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
                AspectMask = VKFormats.Vulkan(vkDest.Desc.Format, vkDest.Desc.Flags).AspectFlags,
                MipLevel = destSubresource.MipLevel,
                BaseArrayLayer = destSubresource.ArrayLayer,
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
    }

    protected override void CopyTextureToBufferImpl(Texture src,
                                                    TextureSubresource srcSubresource,
                                                    Offset3D srcOffset,
                                                    Extent3D srcExtent,
                                                    Buffer dest,
                                                    uint destOffsetInBytes,
                                                    TextureDataLayout destLayout)
    {
        VKTexture vkSrc = src.Vulkan();

        (uint blockWidth, uint blockHeight, _, _) = ZenithHelper.BlockLayout(vkSrc.Desc.Format, srcExtent.Width, srcExtent.Height);

        uint formatSizeInBytes = ZenithHelper.SizeInBytes(vkSrc.Desc.Format);

        BufferImageCopy bufferImageCopy = new()
        {
            BufferOffset = destOffsetInBytes,
            BufferRowLength = destLayout.RowPitchInBytes / formatSizeInBytes * blockWidth,
            BufferImageHeight = destLayout.SlicePitchInBytes / destLayout.RowPitchInBytes * blockHeight,
            ImageSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Format, vkSrc.Desc.Flags).AspectFlags,
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

        Context.Vk.CmdCopyImageToBuffer(CommandBuffer, vkSrc.Image, ImageLayout.TransferSrcOptimal, dest.Vulkan().Buffer, 1, &bufferImageCopy);
    }

    protected override TextureDataLayout GetTextureCopyLayout(PixelFormat format, Extent3D extent)
    {
        uint rowPitchInBytes = ZenithHelper.RowPitchInBytes(format, extent.Width, extent.Height);
        uint slicePitchInBytes = ZenithHelper.SlicePitchInBytes(format, extent.Width, extent.Height);

        return new()
        {
            SizeInBytes = slicePitchInBytes * extent.Depth,
            RowPitchInBytes = rowPitchInBytes,
            SlicePitchInBytes = slicePitchInBytes
        };
    }

    protected override void ResolveTextureImpl(Texture src,
                                               TextureSubresource srcSubresource,
                                               Texture dest,
                                               TextureSubresource destSubresource)
    {
        VKTexture vkSrc = src.Vulkan();
        VKTexture vkDest = dest.Vulkan();

        ZenithHelper.MipDimensions(vkDest.Desc.Width, vkDest.Desc.Height, vkDest.Desc.Depth, destSubresource.MipLevel, out uint mipWidth, out uint mipHeight, out uint mipDepth);

        ImageResolve imageResolve = new()
        {
            SrcSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkSrc.Desc.Format, vkSrc.Desc.Flags).AspectFlags,
                MipLevel = srcSubresource.MipLevel,
                BaseArrayLayer = srcSubresource.ArrayLayer,
                LayerCount = 1
            },
            DstSubresource = new()
            {
                AspectMask = VKFormats.Vulkan(vkDest.Desc.Format, vkDest.Desc.Flags).AspectFlags,
                MipLevel = destSubresource.MipLevel,
                BaseArrayLayer = destSubresource.ArrayLayer,
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

    protected override void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments,
                                                DepthStencilAttachment? depthStencilAttachment)
    {
        using ZenithMarshal.Scope scope = new();

        uint colorAttachmentCount = (uint)colorAttachments.Length;
        RenderingAttachmentInfo* colorAttachmentInfos = (RenderingAttachmentInfo*)ZenithMarshal.Allocate<RenderingAttachmentInfo>(scope, colorAttachmentCount);

        uint width = 0;
        uint height = 0;

        for (uint i = 0; i < colorAttachmentCount; i++)
        {
            ColorAttachment attachment = colorAttachments[(int)i];

            if (i is 0)
            {
                ZenithHelper.MipDimensions(attachment.Texture.Desc.Width,
                                           attachment.Texture.Desc.Height,
                                           0,
                                           attachment.Subresource.MipLevel,
                                           out width,
                                           out height,
                                           out _);
            }

            ImageView imageView = attachment.Texture.Vulkan().CreateAttachmentView(attachment.Subresource);
            renderPassImageViews.Add(imageView);

            colorAttachmentInfos[i] = new()
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = imageView,
                ImageLayout = ImageLayout.AttachmentOptimal,
                LoadOp = attachment.LoadOp switch
                {
                    LoadOp.Clear => AttachmentLoadOp.Clear,
                    LoadOp.DontCare => AttachmentLoadOp.DontCare,
                    _ => AttachmentLoadOp.Load
                },
                StoreOp = attachment.StoreOp is StoreOp.Store ? AttachmentStoreOp.Store : AttachmentStoreOp.DontCare
            };

            if (attachment.LoadOp is LoadOp.Clear)
            {
                Vector4 color = attachment.ClearColor;

                colorAttachmentInfos[i].ClearValue.Color = new()
                {
                    Float32_0 = color.X,
                    Float32_1 = color.Y,
                    Float32_2 = color.Z,
                    Float32_3 = color.W
                };
            }
        }

        RenderingAttachmentInfo* depthAttachment = null;
        RenderingAttachmentInfo* stencilAttachment = null;

        if (depthStencilAttachment is { } depthStencilRenderTarget)
        {
            if (colorAttachmentCount is 0)
            {
                ZenithHelper.MipDimensions(depthStencilRenderTarget.Texture.Desc.Width,
                                           depthStencilRenderTarget.Texture.Desc.Height,
                                           0,
                                           depthStencilRenderTarget.Subresource.MipLevel,
                                           out width,
                                           out height,
                                           out _);
            }

            ImageView imageView = depthStencilRenderTarget.Texture.Vulkan().CreateAttachmentView(depthStencilRenderTarget.Subresource);
            renderPassImageViews.Add(imageView);

            if (ZenithHelper.HasDepth(depthStencilRenderTarget.Texture.Desc.Format))
            {
                depthAttachment = (RenderingAttachmentInfo*)ZenithMarshal.Allocate<RenderingAttachmentInfo>(scope, 1);
                depthAttachment[0] = new()
                {
                    SType = StructureType.RenderingAttachmentInfo,
                    ImageView = imageView,
                    ImageLayout = ImageLayout.AttachmentOptimal,
                    LoadOp = depthStencilRenderTarget.DepthLoadOp switch
                    {
                        LoadOp.Clear => AttachmentLoadOp.Clear,
                        LoadOp.DontCare => AttachmentLoadOp.DontCare,
                        _ => AttachmentLoadOp.Load
                    },
                    StoreOp = depthStencilRenderTarget.DepthStoreOp is StoreOp.Store ? AttachmentStoreOp.Store : AttachmentStoreOp.DontCare
                };

                if (depthStencilRenderTarget.DepthLoadOp is LoadOp.Clear)
                {
                    depthAttachment[0].ClearValue.DepthStencil.Depth = depthStencilRenderTarget.ClearDepth;
                }
            }

            if (ZenithHelper.HasStencil(depthStencilRenderTarget.Texture.Desc.Format))
            {
                stencilAttachment = (RenderingAttachmentInfo*)ZenithMarshal.Allocate<RenderingAttachmentInfo>(scope, 1);
                stencilAttachment[0] = new()
                {
                    SType = StructureType.RenderingAttachmentInfo,
                    ImageView = imageView,
                    ImageLayout = ImageLayout.AttachmentOptimal,
                    LoadOp = depthStencilRenderTarget.StencilLoadOp switch
                    {
                        LoadOp.Clear => AttachmentLoadOp.Clear,
                        LoadOp.DontCare => AttachmentLoadOp.DontCare,
                        _ => AttachmentLoadOp.Load
                    },
                    StoreOp = depthStencilRenderTarget.StencilStoreOp is StoreOp.Store ? AttachmentStoreOp.Store : AttachmentStoreOp.DontCare
                };

                if (depthStencilRenderTarget.StencilLoadOp is LoadOp.Clear)
                {
                    stencilAttachment[0].ClearValue.DepthStencil.Stencil = depthStencilRenderTarget.ClearStencil;
                }
            }
        }

        RenderingInfo renderingInfo = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new() { Extent = new() { Width = width, Height = height } },
            LayerCount = 1,
            ColorAttachmentCount = colorAttachmentCount,
            PColorAttachments = colorAttachmentInfos,
            PDepthAttachment = depthAttachment,
            PStencilAttachment = stencilAttachment
        };

        Context.Vk.CmdBeginRendering(CommandBuffer, ref renderingInfo);
    }

    protected override void EndRenderPassImpl()
    {
        Context.Vk.CmdEndRendering(CommandBuffer);
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
        Span<Rect2D> vkScissors = scissors.Length <= 8 ? stackalloc Rect2D[scissors.Length] : new Rect2D[scissors.Length];

        for (int i = 0; i < scissors.Length; i++)
        {
            Scissor scissor = scissors[i];

            vkScissors[i] = new(new(scissor.X, scissor.Y), new(scissor.Width, scissor.Height));
        }

        Context.Vk.CmdSetScissor(CommandBuffer, 0, (uint)vkScissors.Length, ref vkScissors[0]);
    }

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
        Span<VkViewport> vkViewports = viewports.Length <= 8 ? stackalloc VkViewport[viewports.Length] : new VkViewport[viewports.Length];

        for (int i = 0; i < viewports.Length; i++)
        {
            Viewport viewport = viewports[i];

            vkViewports[i] = new(viewport.X, viewport.Y + viewport.Height, viewport.Width, -viewport.Height, viewport.MinDepth, viewport.MaxDepth);
        }

        Context.Vk.CmdSetViewport(CommandBuffer, 0, (uint)vkViewports.Length, ref vkViewports[0]);
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

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index)
    {
        VkBuffer vkBuffer = buffer.Vulkan().Buffer;
        ulong vkOffset = offsetInBytes;

        Context.Vk.CmdBindVertexBuffers(CommandBuffer, index, 1, ref vkBuffer, ref vkOffset);
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        Context.Vk.CmdBindIndexBuffer(CommandBuffer, buffer.Vulkan().Buffer, offsetInBytes, VKFormats.Vulkan(format));
    }

    protected override void PushResourceTableImpl(Pipeline pipeline, ResourceTable resourceTable)
    {
        (PipelineBindPoint pipelineBindPoint, PipelineLayout pipelineLayout, uint descriptorWriteCount) = pipeline switch
        {
            GraphicsPipeline graphicsPipeline => (PipelineBindPoint.Graphics, graphicsPipeline.Vulkan().PipelineLayout, (uint)graphicsPipeline.Desc.ResourceBindings.Length),
            ComputePipeline computePipeline => (PipelineBindPoint.Compute, computePipeline.Vulkan().PipelineLayout, (uint)computePipeline.Desc.ResourceBindings.Length),
            MeshShadingPipeline meshShadingPipeline => (PipelineBindPoint.Graphics, meshShadingPipeline.Vulkan().PipelineLayout, (uint)meshShadingPipeline.Desc.ResourceBindings.Length),
            _ => (PipelineBindPoint.Graphics, default, 0)
        };

        Context.Vk.CmdPushDescriptorSet(CommandBuffer, pipelineBindPoint, pipelineLayout, 0, descriptorWriteCount, resourceTable.Vulkan().Sets);
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
        ReleaseRenderPassImageViews();

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
        ReleaseRenderPassImageViews();

        base.Destroy();

        Context.Vk.DestroyCommandPool(Context.Device, CommandPool, null);
    }

    private void ReleaseRenderPassImageViews()
    {
        foreach (ImageView imageView in renderPassImageViews)
        {
            Context.Vk.DestroyImageView(Context.Device, imageView, null);
        }

        renderPassImageViews.Clear();
    }
}
