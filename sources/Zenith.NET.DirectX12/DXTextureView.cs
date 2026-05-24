namespace Zenith.NET.DirectX12;

internal class DXTextureView(DXGraphicsContext context, TextureViewDesc desc) : TextureView(context, desc)
{
    private DXDescriptorToken? srvToken;
    private DXDescriptorToken? uavToken;

    public override ResourceHandle SampledHandle => (srvToken ??= CreateSrvToken()).ResourceHandle;

    public override ResourceHandle StorageHandle => (uavToken ??= CreateUavToken()).ResourceHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        uavToken?.Dispose();
        srvToken?.Dispose();
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
