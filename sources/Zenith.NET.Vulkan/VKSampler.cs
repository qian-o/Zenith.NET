using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKSampler : Sampler
{
    public VkSampler Sampler;

    public VKSampler(VKGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        SamplerCreateInfo createInfo = new()
        {
        };

        context.Vk.CreateSampler(context.Device, &createInfo, null, (VkSampler*)Unsafe.AsPointer(ref Sampler)).Success();
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
