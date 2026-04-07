using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKDescriptorAllocator(VKGraphicsContext context) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly List<VKDescriptorPool> available = [];

    public VKDescriptorToken Allocate(ResourceSlot[] resourceSlots)
    {
        using Lock.Scope _ = @lock.EnterScope();

        using ZenithMarshal.Scope scope = new();

        resourceSlots.Vulkan(out DescriptorSetLayoutBinding[] bindings, out VKDescriptorCounts counts);

        if (available.FirstOrDefault(item => item.CanAllocate(counts)) is not VKDescriptorPool pool)
        {
            available.Add(pool = new(context));
        }

        DescriptorSetLayoutCreateInfo createInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)resourceSlots.Length,
            PBindings = (DescriptorSetLayoutBinding*)ZenithMarshal.AllocateAndFill(scope, bindings)
        };

        context.Vk.CreateDescriptorSetLayout(context.Device, &createInfo, null, out DescriptorSetLayout descriptorSetLayout).Success();

        DescriptorSetAllocateInfo allocateInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = pool.Pool,
            DescriptorSetCount = 1,
            PSetLayouts = (DescriptorSetLayout*)ZenithMarshal.AllocateAndFill(scope, [descriptorSetLayout])
        };

        context.Vk.AllocateDescriptorSets(context.Device, &allocateInfo, out DescriptorSet set).Success();
        context.Vk.DestroyDescriptorSetLayout(context.Device, descriptorSetLayout, null);

        return new() { Pool = pool, Set = set };
    }

    public void Free(VKDescriptorToken token)
    {
        context.Vk.FreeDescriptorSets(context.Device, token.Pool.Pool, 1, &token.Set).Success();
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
