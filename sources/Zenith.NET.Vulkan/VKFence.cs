using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKFence : GraphicsResource
{
    public Fence Fence;

    public VKFence(VKGraphicsContext context) : base(context)
    {
        FenceCreateInfo createInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        context.Vk.CreateFence(context.Device, &createInfo, null, out Fence).Success();

        context.Vk.ResetFences(context.Device, 1, ref Fence).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public void Wait()
    {
        Context.Vk.WaitForFences(Context.Device, 1, ref Fence, true, ulong.MaxValue).Success();

        Context.Vk.ResetFences(Context.Device, 1, ref Fence).Success();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyFence(Context.Device, Fence, null);
    }
}
