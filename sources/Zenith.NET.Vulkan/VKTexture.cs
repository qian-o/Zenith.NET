using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKTexture : Texture
{
    public Image Image;

    public VKTexture(GraphicsContext context, TextureDesc desc) : base(context, desc)
    {
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
