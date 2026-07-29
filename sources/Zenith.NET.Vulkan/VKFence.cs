using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKFence : DisposableObject
{
    public Fence Fence;

    public VKFence(VKGraphicsContext context)
    {
        FenceCreateInfo createInfo = new() { SType = StructureType.FenceCreateInfo };

        context.Vk.CreateFence(context.Device, &createInfo, default, out Fence).Success();

        Context = context;
    }

    public VKGraphicsContext Context { get; }

    public void Wait()
    {
        Context.Vk.WaitForFences(Context.Device, 1, ref Fence, true, ulong.MaxValue).Success();
        Context.Vk.ResetFences(Context.Device, 1, ref Fence).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyFence(Context.Device, Fence, default);
    }
}
