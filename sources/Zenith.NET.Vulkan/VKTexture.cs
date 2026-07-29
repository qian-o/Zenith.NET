using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKTexture : Texture
{
    private readonly Dictionary<TextureSubresource, ImageView> attachmentViews = [];

    public Image Image;

    public VKAllocation Allocation;

    public VKTexture(VKGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        ImageCreateInfo createInfo = CreateInfo(desc, context.QueueFamilies);

        context.Vk.CreateImage(context.Device, &createInfo, default, out Image).Success();

        ImageMemoryRequirementsInfo2 requirementsInfo2 = new()
        {
            SType = StructureType.ImageMemoryRequirementsInfo2,
            Image = Image
        };

        MemoryRequirements2 requirements2 = new() { SType = StructureType.MemoryRequirements2 };
        requirements2.AddNext(out MemoryDedicatedRequirements dedicatedRequirements);

        context.Vk.GetImageMemoryRequirements2(context.Device, &requirementsInfo2, &requirements2);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements2.MemoryRequirements.Size,
            MemoryTypeIndex = context.FindMemoryTypeIndex(requirements2.MemoryRequirements.MemoryTypeBits, MemoryResidency.GpuOnly)
        };

        if (dedicatedRequirements.PrefersDedicatedAllocation || dedicatedRequirements.RequiresDedicatedAllocation)
        {
            allocateInfo.AddNext(out MemoryDedicatedAllocateInfo dedicatedAllocateInfo);
            dedicatedAllocateInfo.Image = Image;
        }

        context.Vk.AllocateMemory(context.Device, &allocateInfo, default, out DeviceMemory deviceMemory).Success();
        context.Vk.BindImageMemory(context.Device, Image, deviceMemory, 0).Success();

        Allocation = new(deviceMemory, 0, true, true);

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = TextureSubresourceRange.All(this)
        });
    }

    public VKTexture(VKGraphicsContext context, TextureDesc desc, Image image, VKAllocation allocation) : base(context, desc)
    {
        Image = image;
        Allocation = allocation;

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = TextureSubresourceRange.All(this)
        });
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKTextureView View { get; }

    public override ResourceHandle SampledHandle => View.SampledHandle;

    public override ResourceHandle StorageHandle => View.StorageHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public ImageView GetAttachmentView(TextureSubresource subresource)
    {
        if (!attachmentViews.TryGetValue(subresource, out ImageView view))
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = Image,
                ViewType = VKFormats.Vulkan(Desc.Type).ViewType,
                Format = VKFormats.Vulkan(Desc.Format).Format,
                SubresourceRange = new()
                {
                    AspectMask = VKFormats.Vulkan(Desc.Format).AspectFlags,
                    BaseMipLevel = subresource.MipLevel,
                    LevelCount = 1,
                    BaseArrayLayer = subresource.ArrayLayer,
                    LayerCount = 1
                }
            };

            Context.Vk.CreateImageView(Context.Device, &createInfo, default, out view).Success();

            attachmentViews[subresource] = view;
        }

        return view;
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
        foreach (ImageView attachmentView in attachmentViews.Values)
        {
            Context.Vk.DestroyImageView(Context.Device, attachmentView, default);
        }
        attachmentViews.Clear();

        View.Dispose();

        if (Allocation.OwnsResource)
        {
            Context.Vk.DestroyImage(Context.Device, Image, default);
        }

        if (Allocation.OwnsMemory)
        {
            Context.Vk.FreeMemory(Context.Device, Allocation.DeviceMemory, default);
        }
    }

    public static ImageCreateInfo CreateInfo(TextureDesc desc, QueueFamilies queueFamilies)
    {
        return new()
        {
            SType = StructureType.ImageCreateInfo,
            Flags = desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? ImageCreateFlags.CreateCubeCompatibleBit : ImageCreateFlags.None,
            ImageType = VKFormats.Vulkan(desc.Type).Type,
            Format = VKFormats.Vulkan(desc.Format).Format,
            Extent = new()
            {
                Width = desc.Width,
                Height = desc.Height,
                Depth = desc.Depth
            },
            MipLevels = desc.MipLevels,
            ArrayLayers = desc.ArrayLayers,
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Usage = VKFormats.Vulkan(desc.Usages),
            SharingMode = queueFamilies.SharingMode,
            QueueFamilyIndexCount = queueFamilies.IndexCount,
            PQueueFamilyIndices = queueFamilies.Indices
        };
    }
}
