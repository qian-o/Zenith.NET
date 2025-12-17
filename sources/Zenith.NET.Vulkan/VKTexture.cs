using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKTexture : Texture
{
    public Image Image;

    public VKTexture(VKGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        (SharingMode sharingMode, uint queueFamilyIndexCount, nint pQueueFamilyIndices) = context.GetSharingModeInfo(scope);

        ImageCreateInfo createInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            Flags = desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? ImageCreateFlags.CreateCubeCompatibleBit : ImageCreateFlags.None,
            ImageType = VKFormats.Vulkan(desc.Type).ImageType,
            Format = VKFormats.Vulkan(desc.Format),
            Extent = new()
            {
                Width = desc.Width,
                Height = desc.Height,
                Depth = desc.Type is TextureType.Texture3D ? desc.Depth : 1
            },
            MipLevels = desc.MipLevels,
            ArrayLayers = ZenithHelper.FlattenArrayLayerCount(desc),
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Tiling = desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? ImageTiling.Linear : ImageTiling.Optimal,
            Usage = VKFormats.Vulkan(desc.Flags).ImageUsageFlags,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        context.Vk.CreateImage(context.Device, &createInfo, null, out Image).Success();

        DeviceMemory = new(context, this);

        View = new(context, new()
        {
            Texture = this,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels,
            FirstArrayLayer = 0,
            ArrayLayerCount = desc.ArrayLayers
        });

        Layouts = new ImageLayout[ZenithHelper.SubresourceCount(desc)];
        Array.Fill(Layouts, ImageLayout.Undefined);
    }

    public VKTexture(VKGraphicsContext context, TextureDesc desc, Image image) : base(context, desc)
    {
        Image = image;

        View = new(context, new()
        {
            Texture = this,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels,
            FirstArrayLayer = 0,
            ArrayLayerCount = desc.ArrayLayers
        });

        Layouts = new ImageLayout[ZenithHelper.SubresourceCount(desc)];
        Array.Fill(Layouts, ImageLayout.Undefined);
    }

    public VKTexture(VKGraphicsContext context, TextureDesc desc, ExternalMemoryHandleTypeFlags handleTypes, nint handle) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        (SharingMode sharingMode, uint queueFamilyIndexCount, nint pQueueFamilyIndices) = context.GetSharingModeInfo(scope);

        ImageCreateInfo createInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            Flags = desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? ImageCreateFlags.CreateCubeCompatibleBit : ImageCreateFlags.None,
            ImageType = VKFormats.Vulkan(desc.Type).ImageType,
            Format = VKFormats.Vulkan(desc.Format),
            Extent = new()
            {
                Width = desc.Width,
                Height = desc.Height,
                Depth = desc.Type is TextureType.Texture3D ? desc.Depth : 1
            },
            MipLevels = desc.MipLevels,
            ArrayLayers = ZenithHelper.FlattenArrayLayerCount(desc),
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Tiling = desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? ImageTiling.Linear : ImageTiling.Optimal,
            Usage = VKFormats.Vulkan(desc.Flags).ImageUsageFlags,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        createInfo.AddNext(out ExternalMemoryImageCreateInfo externalMemoryImageCreateInfo);
        externalMemoryImageCreateInfo.HandleTypes = handleTypes;

        context.Vk.CreateImage(context.Device, &createInfo, null, out Image).Success();

        DeviceMemory = new(context, this, handleTypes, handle);

        View = new(context, new()
        {
            Texture = this,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels,
            FirstArrayLayer = 0,
            ArrayLayerCount = desc.ArrayLayers
        });

        Layouts = new ImageLayout[ZenithHelper.SubresourceCount(desc)];
        Array.Fill(Layouts, ImageLayout.Undefined);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKDeviceMemory? DeviceMemory { get; }

    public VKTextureView View { get; }

    public ImageLayout[] Layouts { get; }

    public override MappedMemory Map(TextureSlice slice)
    {
        ImageSubresource subresource = new()
        {
            AspectMask = VKFormats.Vulkan(Desc.Flags).ImageAspectFlags,
            MipLevel = slice.MipLevel,
            ArrayLayer = ZenithHelper.FlattenArrayLayerIndex(Desc, slice)
        };

        SubresourceLayout layout = default;
        Context.Vk.GetImageSubresourceLayout(Context.Device, Image, &subresource, &layout);

        void* pointer;
        Context.Vk.MapMemory(Context.Device, DeviceMemory?.DeviceMemory ?? default, layout.Offset, layout.Size, 0, &pointer).Success();

        return new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = (uint)layout.Size,
            RowPitch = (uint)layout.RowPitch,
            SlicePitch = (uint)layout.DepthPitch
        };
    }

    public override void Unmap()
    {
        Context.Vk.UnmapMemory(Context.Device, DeviceMemory?.DeviceMemory ?? default);
    }

    public void TransitionLayout(VKCommandBuffer commandBuffer,
                                 uint firstMipLevel,
                                 uint mipLevelCount,
                                 uint firstArrayLayer,
                                 uint arrayLayerCount,
                                 uint firstFace,
                                 uint faceCount,
                                 ImageLayout newLayout)
    {
        if (newLayout is ImageLayout.Undefined)
        {
            return;
        }

        for (uint i = 0; i < mipLevelCount; i++)
        {
            for (uint j = 0; j < arrayLayerCount; j++)
            {
                for (uint k = 0; k < faceCount; k++)
                {
                    TextureSlice slice = new() { MipLevel = firstMipLevel + i, ArrayLayer = firstArrayLayer + j, Face = firstFace + k };

                    uint index = ZenithHelper.SubresourceIndex(Desc, slice);

                    ImageLayout oldLayout = Layouts[index];

                    if (oldLayout == newLayout)
                    {
                        continue;
                    }

                    AccessFlags srcAccessMask = AccessFlags.None;
                    PipelineStageFlags srcStageMask = PipelineStageFlags.None;

                    if (oldLayout is ImageLayout.Undefined or ImageLayout.Preinitialized)
                    {
                        srcAccessMask = AccessFlags.None;
                        srcStageMask = PipelineStageFlags.TopOfPipeBit;
                    }
                    else if (oldLayout == ImageLayout.General)
                    {
                        srcAccessMask = AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit;
                        srcStageMask = PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit;

                        if (Context.Capabilities.RayTracingSupported)
                        {
                            srcStageMask |= PipelineStageFlags.RayTracingShaderBitKhr;
                        }
                    }
                    else if (oldLayout == ImageLayout.ColorAttachmentOptimal)
                    {
                        srcAccessMask = AccessFlags.ColorAttachmentWriteBit;
                        srcStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
                    }
                    else if (oldLayout == ImageLayout.DepthStencilAttachmentOptimal)
                    {
                        srcAccessMask = AccessFlags.DepthStencilAttachmentWriteBit;
                        srcStageMask = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit;
                    }
                    else if (oldLayout == ImageLayout.ShaderReadOnlyOptimal)
                    {
                        srcAccessMask = AccessFlags.ShaderReadBit;
                        srcStageMask = PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit;

                        if (Context.Capabilities.RayTracingSupported)
                        {
                            srcStageMask |= PipelineStageFlags.RayTracingShaderBitKhr;
                        }
                    }
                    else if (oldLayout == ImageLayout.TransferSrcOptimal)
                    {
                        srcAccessMask = AccessFlags.TransferReadBit;
                        srcStageMask = PipelineStageFlags.TransferBit;
                    }
                    else if (oldLayout == ImageLayout.TransferDstOptimal)
                    {
                        srcAccessMask = AccessFlags.TransferWriteBit;
                        srcStageMask = PipelineStageFlags.TransferBit;
                    }
                    else if (oldLayout == ImageLayout.PresentSrcKhr)
                    {
                        srcAccessMask = AccessFlags.MemoryReadBit;
                        srcStageMask = PipelineStageFlags.BottomOfPipeBit;
                    }

                    AccessFlags dstAccessMask = AccessFlags.None;
                    PipelineStageFlags dstStageMask = PipelineStageFlags.None;

                    if (newLayout is ImageLayout.General)
                    {
                        dstAccessMask = AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit;
                        dstStageMask = PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit;

                        if (Context.Capabilities.RayTracingSupported)
                        {
                            dstStageMask |= PipelineStageFlags.RayTracingShaderBitKhr;
                        }
                    }
                    else if (newLayout == ImageLayout.ColorAttachmentOptimal)
                    {
                        dstAccessMask = AccessFlags.ColorAttachmentWriteBit;
                        dstStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
                    }
                    else if (newLayout == ImageLayout.DepthStencilAttachmentOptimal)
                    {
                        dstAccessMask = AccessFlags.DepthStencilAttachmentWriteBit;
                        dstStageMask = PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit;
                    }
                    else if (newLayout == ImageLayout.ShaderReadOnlyOptimal)
                    {
                        dstAccessMask = AccessFlags.ShaderReadBit;
                        dstStageMask = PipelineStageFlags.FragmentShaderBit | PipelineStageFlags.ComputeShaderBit;

                        if (Context.Capabilities.RayTracingSupported)
                        {
                            dstStageMask |= PipelineStageFlags.RayTracingShaderBitKhr;
                        }
                    }
                    else if (newLayout == ImageLayout.TransferSrcOptimal)
                    {
                        dstAccessMask = AccessFlags.TransferReadBit;
                        dstStageMask = PipelineStageFlags.TransferBit;
                    }
                    else if (newLayout == ImageLayout.TransferDstOptimal)
                    {
                        dstAccessMask = AccessFlags.TransferWriteBit;
                        dstStageMask = PipelineStageFlags.TransferBit;
                    }
                    else if (newLayout == ImageLayout.PresentSrcKhr)
                    {
                        dstAccessMask = AccessFlags.MemoryReadBit;
                        dstStageMask = PipelineStageFlags.BottomOfPipeBit;
                    }

                    ImageMemoryBarrier imageMemoryBarrier = new()
                    {
                        SType = StructureType.ImageMemoryBarrier,
                        SrcAccessMask = srcAccessMask,
                        DstAccessMask = dstAccessMask,
                        OldLayout = oldLayout,
                        NewLayout = newLayout,
                        Image = Image,
                        SubresourceRange = new()
                        {
                            AspectMask = VKFormats.Vulkan(Desc.Flags).ImageAspectFlags,
                            BaseMipLevel = slice.MipLevel,
                            LevelCount = 1,
                            BaseArrayLayer = ZenithHelper.FlattenArrayLayerIndex(Desc, slice),
                            LayerCount = 1
                        }
                    };

                    Context.Vk.CmdPipelineBarrier(commandBuffer.CommandBuffer,
                                                  srcStageMask,
                                                  dstStageMask,
                                                  DependencyFlags.None,
                                                  0,
                                                  null,
                                                  0,
                                                  null,
                                                  1,
                                                  &imageMemoryBarrier);

                    Layouts[index] = newLayout;
                }
            }
        }
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Image,
            ObjectHandle = Image.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        View.Dispose();

        if (DeviceMemory is not null)
        {
            DeviceMemory.Dispose();

            Context.Vk.DestroyImage(Context.Device, Image, null);
        }
    }
}
