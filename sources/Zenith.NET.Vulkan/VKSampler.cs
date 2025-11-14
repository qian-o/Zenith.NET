using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKSampler : Sampler
{
    public VkSampler Sampler;

    public VKSampler(VKGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        SamplerCreateInfo createInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = VKFormats.Vulkan(desc.Filter).MagFilter,
            MinFilter = VKFormats.Vulkan(desc.Filter).MinFilter,
            MipmapMode = VKFormats.Vulkan(desc.Filter).MipmapMode,
            AddressModeU = VKFormats.Vulkan(desc.U),
            AddressModeV = VKFormats.Vulkan(desc.V),
            AddressModeW = VKFormats.Vulkan(desc.W),
            MipLodBias = desc.LodBias,
            AnisotropyEnable = desc.Filter is Filter.Anisotropic,
            MaxAnisotropy = desc.MaxAnisotropy,
            CompareEnable = desc.ComparisonFunc is not ComparisonFunc.Never,
            CompareOp = VKFormats.Vulkan(desc.ComparisonFunc),
            MinLod = desc.MinLod,
            MaxLod = desc.MaxLod,
            BorderColor = VKFormats.Vulkan(desc.BorderColor)
        };

        context.Vk.CreateSampler(context.Device, &createInfo, null, out Sampler).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Sampler,
            ObjectHandle = Sampler.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroySampler(Context.Device, Sampler, null);
    }
}
