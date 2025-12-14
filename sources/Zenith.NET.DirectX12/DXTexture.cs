using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXTexture : Texture
{
    public ComPtr<ID3D12Resource> Resource;

    public DXTexture(GraphicsContext context, TextureDesc desc) : base(context, desc)
    {
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXTextureView View { get; }

    public override MappedMemory Map(TextureSlice slice)
    {
        throw new NotImplementedException();
    }

    public override void Unmap()
    {
        throw new NotImplementedException();
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
