using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKTextureView : TextureView
{
    public ImageView ImageView;

    public VKTextureView(VKGraphicsContext context, TextureViewDesc desc) : base(context, desc)
    {
        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = desc.Texture.Vulkan().Image,
            ViewType = VKFormats.Vulkan(desc.Texture.Desc.Type).ImageViewType,
            Format = VKFormats.Vulkan(desc.Texture.Desc.Format),
            SubresourceRange = new()
            {
                AspectMask = VKFormats.Vulkan(desc.Texture.Desc.Flags).ImageAspectFlags,
                BaseMipLevel = desc.FirstMipLevel,
                LevelCount = desc.MipLevelCount,
                BaseArrayLayer = ZenithHelper.ArrayLayerRange(desc).ArrayLayerIndex,
                LayerCount = ZenithHelper.ArrayLayerRange(desc).ArrayLayerCount
            }
        };

        context.Vk.CreateImageView(context.Device, &createInfo, null, out ImageView).Success();

        SrvImageInfo = new()
        {
            ImageView = ImageView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };

        UavImageInfo = new()
        {
            ImageView = ImageView,
            ImageLayout = ImageLayout.General
        };
    }

    public VKTextureView(VKGraphicsContext context, Texture texture, TextureSlice slice) : base(context, new() { Texture = texture, FirstMipLevel = slice.MipLevel, MipLevelCount = 1, FirstLayer = slice.Layer, LayerCount = 1 })
    {
        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = texture.Vulkan().Image,
            ViewType = ImageViewType.Type2D,
            Format = VKFormats.Vulkan(texture.Desc.Format),
            SubresourceRange = new()
            {
                AspectMask = VKFormats.Vulkan(texture.Desc.Flags).ImageAspectFlags,
                BaseMipLevel = slice.MipLevel,
                LevelCount = 1,
                BaseArrayLayer = ZenithHelper.ArrayLayerIndex(texture.Desc, slice),
                LayerCount = 1
            }
        };

        context.Vk.CreateImageView(context.Device, &createInfo, null, out ImageView).Success();

        SrvImageInfo = new()
        {
            ImageView = ImageView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };

        UavImageInfo = new()
        {
            ImageView = ImageView,
            ImageLayout = ImageLayout.General
        };

        Slice = slice;
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public DescriptorImageInfo SrvImageInfo { get; }

    public DescriptorImageInfo UavImageInfo { get; }

    public TextureSlice? Slice { get; }

    public void TransitionLayout(VKCommandBuffer commandBuffer, ImageLayout newLayout)
    {
        if (Slice is null)
        {
            Desc.Texture.Vulkan().TransitionLayout(commandBuffer,
                                                   Desc.FirstMipLevel,
                                                   Desc.MipLevelCount,
                                                   Desc.FirstLayer,
                                                   Desc.LayerCount,
                                                   0,
                                                   ZenithHelper.FaceCount(Desc.Texture.Desc),
                                                   newLayout);
        }
        else
        {
            Desc.Texture.Vulkan().TransitionLayout(commandBuffer,
                                                   Desc.FirstMipLevel,
                                                   Desc.MipLevelCount,
                                                   Desc.FirstLayer,
                                                   Desc.LayerCount,
                                                   Slice.Value.Face,
                                                   1,
                                                   newLayout);
        }
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.ImageView,
            ObjectHandle = ImageView.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyImageView(Context.Device, ImageView, null);
    }
}
