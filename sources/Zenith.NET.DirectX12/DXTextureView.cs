using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTextureView(GraphicsContext context, TextureViewDesc desc) : TextureView(context, desc)
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
        DXDescriptorToken token = Context.CbvSrvUavAllocator.Allocate();

        ShaderResourceViewDesc viewDesc = new();

        Context.Device.CreateShaderResourceView(Desc.Texture.DirectX12().Resource, &viewDesc, token.Handle);

        return token;
    }

    private DXDescriptorToken CreateUavToken()
    {
        DXDescriptorToken token = Context.CbvSrvUavAllocator.Allocate();

        UnorderedAccessViewDesc viewDesc = new();

        Context.Device.CreateUnorderedAccessView(Desc.Texture.DirectX12().Resource, (ID3D12Resource*)null, &viewDesc, token.Handle);

        return token;
    }
}
