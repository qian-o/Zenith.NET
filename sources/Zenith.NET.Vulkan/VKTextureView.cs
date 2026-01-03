using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

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
                AspectMask = VKFormats.Vulkan(desc.Texture.Desc.Format, desc.Texture.Desc.Flags).AspectFlags & ~ImageAspectFlags.StencilBit,
                BaseMipLevel = desc.FirstMipLevel,
                LevelCount = desc.MipLevelCount,
                BaseArrayLayer = ZenithHelper.FlattenArrayLayerRange(desc).FlattenArrayLayerIndex,
                LayerCount = ZenithHelper.FlattenArrayLayerRange(desc).FlattenArrayLayerCount
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

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public DescriptorImageInfo SrvImageInfo { get; }

    public DescriptorImageInfo UavImageInfo { get; }

    public void TransitionLayout(VKCommandBuffer commandBuffer, ImageLayout newLayout)
    {
        Desc.Texture.Vulkan().TransitionLayout(commandBuffer,
                                               Desc.FirstMipLevel,
                                               Desc.MipLevelCount,
                                               Desc.FirstArrayLayer,
                                               Desc.ArrayLayerCount,
                                               0,
                                               ZenithHelper.FaceCount(Desc.Texture.Desc),
                                               newLayout);
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
