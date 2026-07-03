using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal class VKSampler : Sampler
{
    public VKDescriptorToken Token;

    public VKSampler(VKGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        Token = context.SamplerHeap.Allocate(new SamplerCreateInfo()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = VKFormats.Vulkan(desc.MagFilter).Filter,
            MinFilter = VKFormats.Vulkan(desc.MinFilter).Filter,
            MipmapMode = VKFormats.Vulkan(desc.MipFilter).MipmapMode,
            AddressModeU = VKFormats.Vulkan(desc.AddressU),
            AddressModeV = VKFormats.Vulkan(desc.AddressV),
            AddressModeW = VKFormats.Vulkan(desc.AddressW),
            MipLodBias = desc.LodBias,
            AnisotropyEnable = desc.MaxAnisotropy > 1,
            MaxAnisotropy = desc.MaxAnisotropy,
            CompareEnable = desc.CompareOp is not CompareOp.Never,
            CompareOp = VKFormats.Vulkan(desc.CompareOp),
            MinLod = desc.MinLod,
            MaxLod = desc.MaxLod,
            BorderColor = VKFormats.Vulkan(desc.BorderColor)
        });
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override ResourceHandle Handle => Token.ResourceHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Token.Dispose();
    }
}
