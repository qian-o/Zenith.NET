using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKDescriptorAllocator(VKGraphicsContext context) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly List<VKDescriptorPool> available = [];

    public VKDescriptorToken Alloc(VKResourceLayout resourceLayout)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (available.FirstOrDefault(item => item.CanAlloc(resourceLayout.Counts)) is not VKDescriptorPool descriptorPool)
        {
            available.Add(descriptorPool = new(context));
        }

        DescriptorSetAllocateInfo allocateInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = descriptorPool.Pool,
            DescriptorSetCount = 1,
            PSetLayouts = (DescriptorSetLayout*)Unsafe.AsPointer(ref resourceLayout.DescriptorSetLayout),
        };

        DescriptorSet descriptorSet;
        context.Vk.AllocateDescriptorSets(context.Device, &allocateInfo, &descriptorSet).Success();

        return new(descriptorPool, descriptorSet);
    }

    public void Free(VKDescriptorToken token)
    {
        DescriptorSet descriptorSet = token.DescriptorSet;
        context.Vk.FreeDescriptorSets(context.Device, token.DescriptorPool.Pool, 1, &descriptorSet).Success();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        foreach (VKDescriptorPool pool in available)
        {
            pool.Dispose();
        }
        available.Clear();
    }
}
