using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCapabilities(MTLGraphicsContext context) : Capabilities(context)
{
    public override string DeviceName { get; } = context.Device.Name;

    public override bool RayTracingSupported { get; } = context.Device.SupportsRaytracingFromRender;

    public override bool MeshShadingSupported { get; } = context.Device.SupportsFamily(MTLGPUFamily.Apple7);

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
