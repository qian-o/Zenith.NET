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
        uavToken?.Dispose();
        srvToken?.Dispose();
    }

    private DXDescriptorToken CreateSrvToken()
    {
        DXDescriptorToken token = Context.CbvSrvUavAllocator.Allocate();

        ShaderResourceViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Texture.Desc.Format) };

        switch (Desc.Texture.Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = SrvDimension.Texture1D;
                    viewDesc.Texture1D.MostDetailedMip = Desc.FirstMipLevel;
                    viewDesc.Texture1D.MipLevels = Desc.MipLevelCount;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = SrvDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MostDetailedMip = Desc.FirstMipLevel;
                    viewDesc.Texture1DArray.MipLevels = Desc.MipLevelCount;
                    viewDesc.Texture1DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerIndex;
                    viewDesc.Texture1DArray.ArraySize = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerCount;
                }
                break;

            case TextureType.Texture2D:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2D;
                    viewDesc.Texture2D.MostDetailedMip = Desc.FirstMipLevel;
                    viewDesc.Texture2D.MipLevels = Desc.MipLevelCount;
                }
                else
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2Dms;
                }
                break;

            case TextureType.Texture2DArray:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2Darray;
                    viewDesc.Texture2DArray.MostDetailedMip = Desc.FirstMipLevel;
                    viewDesc.Texture2DArray.MipLevels = Desc.MipLevelCount;
                    viewDesc.Texture2DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerIndex;
                    viewDesc.Texture2DArray.ArraySize = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerCount;
                }
                else
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerIndex;
                    viewDesc.Texture2DMSArray.ArraySize = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerCount;
                }
                break;

            case TextureType.Texture3D:
                {
                    viewDesc.ViewDimension = SrvDimension.Texture3D;
                    viewDesc.Texture3D.MostDetailedMip = Desc.FirstMipLevel;
                    viewDesc.Texture3D.MipLevels = Desc.MipLevelCount;
                }
                break;

            case TextureType.TextureCube:
                {
                    viewDesc.ViewDimension = SrvDimension.Texturecube;
                    viewDesc.TextureCube.MostDetailedMip = Desc.FirstMipLevel;
                    viewDesc.TextureCube.MipLevels = Desc.MipLevelCount;
                }
                break;

            case TextureType.TextureCubeArray:
                {
                    viewDesc.ViewDimension = SrvDimension.Texturecubearray;
                    viewDesc.TextureCubeArray.MostDetailedMip = Desc.FirstMipLevel;
                    viewDesc.TextureCubeArray.MipLevels = Desc.MipLevelCount;
                    viewDesc.TextureCubeArray.First2DArrayFace = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerIndex;
                    viewDesc.TextureCubeArray.NumCubes = Desc.ArrayLayerCount;
                }
                break;
        }

        Context.Device.CreateShaderResourceView(Desc.Texture.DirectX12().Resource, &viewDesc, token.Handle);

        return token;
    }

    private DXDescriptorToken CreateUavToken()
    {
        DXDescriptorToken token = Context.CbvSrvUavAllocator.Allocate();

        UnorderedAccessViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Texture.Desc.Format) };

        switch (Desc.Texture.Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = UavDimension.Texture1D;
                    viewDesc.Texture1D.MipSlice = Desc.FirstMipLevel;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = UavDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MipSlice = Desc.FirstMipLevel;
                    viewDesc.Texture1DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerIndex;
                    viewDesc.Texture1DArray.ArraySize = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerCount;
                }
                break;

            case TextureType.Texture2D:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = UavDimension.Texture2D;
                    viewDesc.Texture2D.MipSlice = Desc.FirstMipLevel;
                }
                else
                {
                    viewDesc.ViewDimension = UavDimension.Texture2Dms;
                }
                break;

            case TextureType.Texture2DArray:
            case TextureType.TextureCube:
            case TextureType.TextureCubeArray:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = UavDimension.Texture2Darray;
                    viewDesc.Texture2DArray.MipSlice = Desc.FirstMipLevel;
                    viewDesc.Texture2DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerIndex;
                    viewDesc.Texture2DArray.ArraySize = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerCount;
                }
                else
                {
                    viewDesc.ViewDimension = UavDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerIndex;
                    viewDesc.Texture2DMSArray.ArraySize = ZenithHelper.FlattenArrayLayerRange(Desc).FlattenArrayLayerCount;
                }
                break;

            case TextureType.Texture3D:
                {
                    viewDesc.ViewDimension = UavDimension.Texture3D;
                    viewDesc.Texture3D.MipSlice = Desc.FirstMipLevel;
                    viewDesc.Texture3D.WSize = Desc.Texture.Desc.Depth;
                }
                break;
        }

        Context.Device.CreateUnorderedAccessView(Desc.Texture.DirectX12().Resource, (ID3D12Resource*)null, &viewDesc, token.Handle);

        return token;
    }
}
