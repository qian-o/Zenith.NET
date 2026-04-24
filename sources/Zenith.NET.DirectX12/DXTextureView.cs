using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTextureView(DXGraphicsContext context, TextureViewDesc desc) : TextureView(context, desc)
{
    private const uint CubeMapFaceCount = 6;

    private DXDescriptorToken? srvToken;
    private DXDescriptorToken? uavToken;

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
        DXDescriptorToken token = context.CbvSrvUavAllocator.Allocate(1);
        TextureSubresourceRange range = Desc.Range;

        ShaderResourceViewDesc viewDesc = new()
        {
            Format = Resolve(Desc.Format),
            Shader4ComponentMapping = DXGraphicsContext.Shader4ComponentMapping
        };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = SrvDimension.Texture1D;
                    viewDesc.Texture1D.MostDetailedMip = range.BaseMipLevel;
                    viewDesc.Texture1D.MipLevels = range.LevelCount;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = SrvDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MostDetailedMip = range.BaseMipLevel;
                    viewDesc.Texture1DArray.MipLevels = range.LevelCount;
                    viewDesc.Texture1DArray.FirstArraySlice = range.BaseArrayLayer;
                    viewDesc.Texture1DArray.ArraySize = range.LayerCount;
                }
                break;

            case TextureType.Texture2D:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2D;
                    viewDesc.Texture2D.MostDetailedMip = range.BaseMipLevel;
                    viewDesc.Texture2D.MipLevels = range.LevelCount;
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
                    viewDesc.Texture2DArray.MostDetailedMip = range.BaseMipLevel;
                    viewDesc.Texture2DArray.MipLevels = range.LevelCount;
                    viewDesc.Texture2DArray.FirstArraySlice = range.BaseArrayLayer;
                    viewDesc.Texture2DArray.ArraySize = range.LayerCount;
                }
                else
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = range.BaseArrayLayer;
                    viewDesc.Texture2DMSArray.ArraySize = range.LayerCount;
                }
                break;

            case TextureType.Texture3D:
                {
                    viewDesc.ViewDimension = SrvDimension.Texture3D;
                    viewDesc.Texture3D.MostDetailedMip = range.BaseMipLevel;
                    viewDesc.Texture3D.MipLevels = range.LevelCount;
                }
                break;

            case TextureType.TextureCube:
                {
                    viewDesc.ViewDimension = SrvDimension.Texturecube;
                    viewDesc.TextureCube.MostDetailedMip = range.BaseMipLevel;
                    viewDesc.TextureCube.MipLevels = range.LevelCount;
                }
                break;

            case TextureType.TextureCubeArray:
                {
                    viewDesc.ViewDimension = SrvDimension.Texturecubearray;
                    viewDesc.TextureCubeArray.MostDetailedMip = range.BaseMipLevel;
                    viewDesc.TextureCubeArray.MipLevels = range.LevelCount;
                    viewDesc.TextureCubeArray.First2DArrayFace = range.BaseArrayLayer;
                    viewDesc.TextureCubeArray.NumCubes = range.LayerCount / CubeMapFaceCount;
                }
                break;
        }

        context.Device.CreateShaderResourceView(Desc.Texture.DirectX12().Resource, &viewDesc, token.Handle);

        return token;
    }

    private DXDescriptorToken CreateUavToken()
    {
        DXDescriptorToken token = context.CbvSrvUavAllocator.Allocate(1);
        TextureSubresourceRange range = Desc.Range;

        UnorderedAccessViewDesc viewDesc = new() { Format = Resolve(Desc.Format) };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = UavDimension.Texture1D;
                    viewDesc.Texture1D.MipSlice = range.BaseMipLevel;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = UavDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MipSlice = range.BaseMipLevel;
                    viewDesc.Texture1DArray.FirstArraySlice = range.BaseArrayLayer;
                    viewDesc.Texture1DArray.ArraySize = range.LayerCount;
                }
                break;

            case TextureType.Texture2D:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = UavDimension.Texture2D;
                    viewDesc.Texture2D.MipSlice = range.BaseMipLevel;
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
                    viewDesc.Texture2DArray.MipSlice = range.BaseMipLevel;
                    viewDesc.Texture2DArray.FirstArraySlice = range.BaseArrayLayer;
                    viewDesc.Texture2DArray.ArraySize = range.LayerCount;
                }
                else
                {
                    viewDesc.ViewDimension = UavDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = range.BaseArrayLayer;
                    viewDesc.Texture2DMSArray.ArraySize = range.LayerCount;
                }
                break;

            case TextureType.Texture3D:
                {
                    viewDesc.ViewDimension = UavDimension.Texture3D;
                    viewDesc.Texture3D.MipSlice = range.BaseMipLevel;
                    viewDesc.Texture3D.WSize = Desc.Texture.Desc.Depth;
                }
                break;
        }

        context.Device.CreateUnorderedAccessView(Desc.Texture.DirectX12().Resource, (ID3D12Resource*)null, &viewDesc, token.Handle);

        return token;
    }

    private static Format Resolve(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.D16UNorm => Format.FormatR16Unorm,
            PixelFormat.D24UNormS8UInt => Format.FormatR24G8Typeless,
            PixelFormat.D32Float => Format.FormatR32Float,
            PixelFormat.D32FloatS8UInt => Format.FormatR32G8X24Typeless,
            _ => DXFormats.DirectX12(pixelFormat)
        };
    }
}
