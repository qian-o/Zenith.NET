using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using Feature = Silk.NET.Direct3D12.Feature;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCapabilities : Capabilities
{
    public DXCapabilities(DXGraphicsContext context)
    {
        AdapterDesc desc;
        context.Adapter4.GetDesc(&desc).Success();

        FeatureDataD3D12Options5 options5 = new();
        context.Device14.CheckFeatureSupport(Feature.D3D12Options5, &options5, (uint)sizeof(FeatureDataD3D12Options5)).Success();

        FeatureDataD3D12Options7 options7 = new();
        context.Device14.CheckFeatureSupport(Feature.D3D12Options7, &options7, (uint)sizeof(FeatureDataD3D12Options7)).Success();

        DeviceName = ZenithMarshal.StringFromPointer((nint)desc.Description, StringEncoding.UTF16);
        RayTracing = options5.RaytracingTier >= RaytracingTier.Tier11;
        MeshShading = options7.MeshShaderTier >= MeshShaderTier.Tier1;
    }

    public override string DeviceName { get; }

    public override bool RayTracing { get; }

    public override bool MeshShading { get; }
}
