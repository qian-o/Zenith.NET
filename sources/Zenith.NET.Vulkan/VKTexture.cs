using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

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
                Depth = desc.Depth
            },
            MipLevels = desc.MipLevels,
            ArrayLayers = ZenithHelper.ArrayLayerCount(desc),
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Tiling = desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? ImageTiling.Linear : ImageTiling.Optimal,
            Usage = VKFormats.Vulkan(desc.Flags).ImageUsageFlags,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        context.Vk.CreateImage(context.Device, &createInfo, null, (Image*)Unsafe.AsPointer(ref Image)).Success();

        DeviceMemory = new(context, this);

        View = new(context, new()
        {
            Texture = this,
            FirstLayer = 0,
            LayerCount = desc.Layers,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels
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
            FirstLayer = 0,
            LayerCount = desc.Layers,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels
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
                Depth = desc.Depth
            },
            MipLevels = desc.MipLevels,
            ArrayLayers = ZenithHelper.ArrayLayerCount(desc),
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Tiling = desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? ImageTiling.Linear : ImageTiling.Optimal,
            Usage = VKFormats.Vulkan(desc.Flags).ImageUsageFlags,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        createInfo.AddNext(out ExternalMemoryImageCreateInfo externalMemoryImageCreateInfo);
        externalMemoryImageCreateInfo.HandleTypes = handleTypes;

        context.Vk.CreateImage(context.Device, &createInfo, null, (Image*)Unsafe.AsPointer(ref Image)).Success();

        DeviceMemory = new(context, this, handleTypes, handle);

        View = new(context, new()
        {
            Texture = this,
            FirstLayer = 0,
            LayerCount = desc.Layers,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels
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
            ArrayLayer = ZenithHelper.ArrayLayerIndex(Desc, slice),
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

    public void TransitionLayout(VKCommandBuffer commandBuffer, uint firstLayer, uint layerCount, uint firstMipLevel, uint mipLevelCount, ImageLayout newLayout)
    {
        uint faces = Desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? 6u : 1u;

        for (uint i = 0; i < layerCount; i++)
        {
            for (uint j = 0; j < mipLevelCount; j++)
            {
                for (uint face = 0; face < faces; face++)
                {
                    uint index = ZenithHelper.SubresourceIndex(Desc, new() { Layer = firstLayer + i, MipLevel = firstMipLevel + j, Face = face });

                    ImageLayout oldLayout = Layouts[index];

                    if (oldLayout == newLayout)
                    {
                        continue;
                    }

                    throw new NotImplementedException();
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
