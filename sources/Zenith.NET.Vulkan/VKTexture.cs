using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKTexture : Texture
{
    public Image Image;

    public VKTexture(VKGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        uint* queueFamilyIndices = (uint*)ZenithMarshal.Allocate<uint>(scope, (uint)context.QueueFamilyIndices.Length);
        context.QueueFamilyIndices.CopyTo(new Span<uint>(queueFamilyIndices, context.QueueFamilyIndices.Length));

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
            ArrayLayers = desc.Layers,
            Samples = VKFormats.Vulkan(desc.SampleCount),
            Tiling = desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? ImageTiling.Linear : ImageTiling.Optimal,
            Usage = VKFormats.Vulkan(desc.Flags).ImageUsageFlags,
            SharingMode = context.QueueFamilyIndices.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent,
            QueueFamilyIndexCount = (uint)context.QueueFamilyIndices.Length,
            PQueueFamilyIndices = queueFamilyIndices
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

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKDeviceMemory? DeviceMemory { get; }

    public VKTextureView View { get; }

    public ImageLayout[] Layouts { get; }

    public override MappedMemory Map(TextureSlice slice)
    {
        throw new NotImplementedException();
    }

    public override void Unmap()
    {
        throw new NotImplementedException();
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
