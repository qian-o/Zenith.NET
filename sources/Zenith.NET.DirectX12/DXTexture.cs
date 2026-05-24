using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXTexture : Texture
{
    public ComPtr<ID3D12Resource> Resource;

    public DXTexture(DXGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
    }

    public DXTextureView View { get; }

    public override ResourceHandle SampledHandle => View.SampledHandle;

    public override ResourceHandle StorageHandle => View.StorageHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        Resource.SetName(name).Success();
    }

    protected override void Destroy()
    {
        View.Dispose();

        Resource.Dispose();
    }
}
