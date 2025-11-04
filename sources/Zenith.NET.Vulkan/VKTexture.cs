using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKTexture : Texture
{
    public Image Image;

    public VKTexture(GraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        uint* queueFamilyIndices = (uint*)ZenithMarshal.Allocate<uint>(scope, (uint)Context.QueueFamilyIndices.Length);
        Context.QueueFamilyIndices.CopyTo(new Span<uint>(queueFamilyIndices, Context.QueueFamilyIndices.Length));

        ImageCreateInfo createInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            Flags = Desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray ? ImageCreateFlags.CreateCubeCompatibleBit : ImageCreateFlags.None,
            ImageType = VKFormats.Vulkan(Desc.Type),
            Format = VKFormats.Vulkan(Desc.Format),
            Extent = new()
            {
                Width = Desc.Width,
                Height = Desc.Height,
                Depth = Desc.Depth
            },
            MipLevels = Desc.MipLevels,
            ArrayLayers = Desc.Layers,
            Samples = VKFormats.Vulkan(Desc.SampleCount),
            Tiling = Desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? ImageTiling.Linear : ImageTiling.Optimal,
            Usage = VKFormats.Vulkan(Desc.Flags),
            SharingMode = Context.QueueFamilyIndices.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent,
            QueueFamilyIndexCount = (uint)Context.QueueFamilyIndices.Length,
            PQueueFamilyIndices = queueFamilyIndices
        };

        Context.Vk.CreateImage(Context.Device, &createInfo, null, (Image*)Unsafe.AsPointer(ref Image)).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKDeviceMemory DeviceMemory { get; }

    public VKTextureView View { get; }

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

        DeviceMemory.Dispose();

        Context.Vk.DestroyImage(Context.Device, Image, null);
    }
}
