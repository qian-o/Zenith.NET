using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKTextureView(VKGraphicsContext context, TextureViewDesc desc) : TextureView(context, desc)
{
    private VKDescriptorToken? sampledToken;
    private VKDescriptorToken? storageToken;

    public override ResourceHandle SampledHandle => (sampledToken ??= CreateToken(DescriptorType.SampledImage, ImageLayout.ShaderReadOnlyOptimal)).ResourceHandle;

    public override ResourceHandle StorageHandle => (storageToken ??= CreateToken(DescriptorType.StorageImage, ImageLayout.General)).ResourceHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        storageToken?.Dispose();
        sampledToken?.Dispose();
    }

    private VKDescriptorToken CreateToken(DescriptorType type, ImageLayout layout)
    {
        ImageViewCreateInfo view = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Desc.Texture.Vulkan().Image,
            ViewType = VKFormats.Vulkan(Desc.Type).ViewType,
            Format = VKFormats.Vulkan(Desc.Format).Format,
            SubresourceRange = new()
            {
                AspectMask = VKFormats.Vulkan(Desc.Format).AspectFlags & ~ImageAspectFlags.StencilBit,
                BaseMipLevel = Desc.Range.BaseMipLevel,
                LevelCount = Desc.Range.LevelCount,
                BaseArrayLayer = Desc.Range.BaseArrayLayer,
                LayerCount = Desc.Range.LayerCount
            }
        };

        ImageDescriptorInfoEXT image = new()
        {
            SType = StructureType.ImageDescriptorInfoExt(),
            PView = &view,
            Layout = layout
        };

        return context.ResourceHeap.Allocate(new ResourceDescriptorInfoEXT()
        {
            SType = StructureType.ResourceDescriptorInfoExt(),
            Type = type,
            Data = new() { PImage = &image }
        });
    }
}
