namespace Zenith.NET;

internal unsafe class VKDescriptorSetAllocator(VKGraphicsContext context) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly List<VKDescriptorPool> available = [];

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
