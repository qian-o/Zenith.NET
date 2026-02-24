using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLCapabilities(MTLGraphicsContext context) : Capabilities
{
    public override string DeviceName { get; } = context.Device.Name!;

    public override bool RayTracingSupported { get; } = context.Device.SupportsRaytracingFromRender;

    public override bool MeshShadingSupported { get; } = context.Device.SupportsFamily(MTLGPUFamily.Apple7) || context.Device.SupportsFamily(MTLGPUFamily.Mac2);
}
