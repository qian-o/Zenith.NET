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
            ImageType = VKFormats.Vulkan(desc.Type).Type,
            Format = VKFormats.Vulkan(desc.Format),
            Extent = new()
            {
                Width = desc.Width,
                Height = desc.Height,
                Depth = desc.Type is TextureType.Texture3D ? desc.Depth : 1
            },
            MipLevels = desc.MipLevels,
            ArrayLayers = desc.ArrayLayers,
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Usage = VKFormats.Vulkan(desc.Format, desc.Flags).UsageFlags,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = (uint*)pQueueFamilyIndices
        };

        context.Vk.CreateImage(context.Device, &createInfo, null, out Image).Success();

        DeviceMemory = new(context, this);

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = new()
            {
                BaseMipLevel = 0,
                LevelCount = desc.MipLevels,
                BaseArrayLayer = 0,
                LayerCount = desc.ArrayLayers
            }
        });
    }

    public VKTexture(VKGraphicsContext context, TextureDesc desc, Image image) : base(context, desc)
    {
        Image = image;

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = new()
            {
                BaseMipLevel = 0,
                LevelCount = desc.MipLevels,
                BaseArrayLayer = 0,
                LayerCount = desc.ArrayLayers
            }
        });
    }

    public VKTexture(VKGraphicsContext context, TextureDesc desc, ExternalMemoryHandleTypeFlags handleTypes, nint handle) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        (SharingMode sharingMode, uint queueFamilyIndexCount, nint pQueueFamilyIndices) = context.GetSharingModeInfo(scope);

        ImageCreateInfo createInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            Flags = desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? ImageCreateFlags.CreateCubeCompatibleBit : ImageCreateFlags.None,
            ImageType = VKFormats.Vulkan(desc.Type).Type,
            Format = VKFormats.Vulkan(desc.Format),
            Extent = new()
            {
                Width = desc.Width,
                Height = desc.Height,
                Depth = desc.Type is TextureType.Texture3D ? desc.Depth : 1
            },
            MipLevels = desc.MipLevels,
            ArrayLayers = desc.ArrayLayers,
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Usage = VKFormats.Vulkan(desc.Format, desc.Flags).UsageFlags,
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
            Type = desc.Type,
            Format = desc.Format,
            Range = new()
            {
                BaseMipLevel = 0,
                LevelCount = desc.MipLevels,
                BaseArrayLayer = 0,
                LayerCount = desc.ArrayLayers
            }
        });
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKDeviceMemory? DeviceMemory { get; }

    public VKTextureView View { get; }

    public uint SubresourceIndex(TextureSubresource subresource)
    {
        return (subresource.ArrayLayer * Desc.MipLevels) + subresource.MipLevel;
    }

    public ImageView CreateAttachmentView(TextureSubresource subresource)
    {
        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Image,
            ViewType = ImageViewType.Type2D,
            Format = VKFormats.Vulkan(Desc.Format),
            SubresourceRange = new()
            {
                AspectMask = VKFormats.Vulkan(Desc.Format, Desc.Flags).AspectFlags,
                BaseMipLevel = subresource.MipLevel,
                LevelCount = 1,
                BaseArrayLayer = subresource.ArrayLayer,
                LayerCount = 1
            }
        };

        ImageView imageView;
        Context.Vk.CreateImageView(Context.Device, &createInfo, null, &imageView).Success();

        return imageView;
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
            Context.Vk.DestroyImage(Context.Device, Image, null);

            DeviceMemory.Dispose();
        }
    }
}
