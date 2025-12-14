using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXTextureView(GraphicsContext context, TextureViewDesc desc) : TextureView(context, desc)
{
    private DXDescriptorToken? srvToken;
    private DXDescriptorToken? uavToken;

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public CpuDescriptorHandle SrvHandle => (srvToken ??= CreateSrvToken()).Handle;

    public CpuDescriptorHandle UavHandle => (uavToken ??= CreateUavToken()).Handle;

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        uavToken?.Free();
        srvToken?.Free();
    }

    private DXDescriptorToken CreateSrvToken()
    {
        throw new NotImplementedException();
    }

    private DXDescriptorToken CreateUavToken()
    {
        throw new NotImplementedException();
    }
}
