using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKDescriptorAllocator(VKGraphicsContext context) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly List<VKDescriptorPool> available = [];

    public VKDescriptorToken Allocate(VKResourceLayout resourceLayout)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (available.FirstOrDefault(item => item.CanAllocate(resourceLayout.Counts)) is not VKDescriptorPool descriptorPool)
        {
            available.Add(descriptorPool = new(context));
        }

        fixed (DescriptorSetLayout* pSetLayouts = &resourceLayout.DescriptorSetLayout)
        {
            DescriptorSetAllocateInfo allocateInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = descriptorPool.Pool,
                DescriptorSetCount = 1,
                PSetLayouts = pSetLayouts
            };

            DescriptorSet descriptorSet;
            context.Vk.AllocateDescriptorSets(context.Device, &allocateInfo, &descriptorSet).Success();

            return new(descriptorPool, descriptorSet);
        }
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
