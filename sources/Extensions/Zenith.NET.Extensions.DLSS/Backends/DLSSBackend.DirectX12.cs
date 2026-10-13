using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend
{
    private void InitializeDirectX12(byte* projectId, byte* engineVersion, void* dataPath, NGXFeatureCommonInfo* featureInfo)
    {
        Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, Device, featureInfo, NGXVersion.Api).Success();
    }

    private void GetDirectX12CapabilityParameters(NGXParameter* parameters)
    {
        Ngx.D3D12.GetCapabilityParameters(parameters).Success();
    }

    private void AllocateDirectX12Parameters(NGXParameter* parameters)
    {
        Ngx.D3D12.AllocateParameters(parameters).Success();
    }

    private void DestroyDirectX12Parameters(NGXParameter parameters)
    {
        Ngx.D3D12.DestroyParameters(parameters).Success();
    }

    private void ReleaseDirectX12Feature(NGXHandle handle)
    {
        Ngx.D3D12.ReleaseFeature(handle).Success();
    }

    private DLSSBinding CreateDirectX12Binding(Texture texture, NGXDimensions size)
    {
        return new(texture.GetNativeObject(NativeObjectType.D3D12Resource), default, size);
    }

    private void ShutdownDirectX12()
    {
        Ngx.D3D12.Shutdown1(Device).Success();
    }
}
