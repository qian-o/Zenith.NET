using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTextureView(DXGraphicsContext context, TextureViewDesc desc) : TextureView(context, desc)
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
        DXDescriptorToken token = context.CbvSrvUavHeap.Allocate();

        ShaderResourceViewDesc viewDesc = new()
        {
            Format = DXFormats.DirectX12(Desc.Format),
            Shader4ComponentMapping = DXGraphicsContext.Shader4ComponentMapping
        };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                viewDesc.ViewDimension = SrvDimension.Texture1D;
                viewDesc.Texture1D = new()
                {
                    MostDetailedMip = Desc.Range.BaseMipLevel,
                    MipLevels = Desc.Range.LevelCount
                };
                break;

            case TextureType.Texture2D:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2D;
                    viewDesc.Texture2D = new()
                    {
                        MostDetailedMip = Desc.Range.BaseMipLevel,
                        MipLevels = Desc.Range.LevelCount
                    };
                }
                else
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2Dms;
                }
                break;

            case TextureType.Texture3D:
                viewDesc.ViewDimension = SrvDimension.Texture3D;
                viewDesc.Texture3D = new()
                {
                    MostDetailedMip = Desc.Range.BaseMipLevel,
                    MipLevels = Desc.Range.LevelCount
                };
                break;

            case TextureType.TextureCube:
                viewDesc.ViewDimension = SrvDimension.Texturecube;
                viewDesc.TextureCube = new()
                {
                    MostDetailedMip = Desc.Range.BaseMipLevel,
                    MipLevels = Desc.Range.LevelCount
                };
                break;

            case TextureType.Texture1DArray:
                viewDesc.ViewDimension = SrvDimension.Texture1Darray;
                viewDesc.Texture1DArray = new()
                {
                    MostDetailedMip = Desc.Range.BaseMipLevel,
                    MipLevels = Desc.Range.LevelCount,
                    FirstArraySlice = Desc.Range.BaseArrayLayer,
                    ArraySize = Desc.Range.LayerCount
                };
                break;

            case TextureType.Texture2DArray:
                if (Desc.Texture.Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2Darray;
                    viewDesc.Texture2DArray = new()
                    {
                        MostDetailedMip = Desc.Range.BaseMipLevel,
                        MipLevels = Desc.Range.LevelCount,
                        FirstArraySlice = Desc.Range.BaseArrayLayer,
                        ArraySize = Desc.Range.LayerCount
                    };
                }
                else
                {
                    viewDesc.ViewDimension = SrvDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray = new()
                    {
                        FirstArraySlice = Desc.Range.BaseArrayLayer,
                        ArraySize = Desc.Range.LayerCount
                    };
                }
                break;

            case TextureType.TextureCubeArray:
                viewDesc.ViewDimension = SrvDimension.Texturecubearray;
                viewDesc.TextureCubeArray = new()
                {
                    MostDetailedMip = Desc.Range.BaseMipLevel,
                    MipLevels = Desc.Range.LevelCount,
                    First2DArrayFace = Desc.Range.BaseArrayLayer,
                    NumCubes = Desc.Range.LayerCount / 6
                };
                break;
        }

        context.Device.CreateShaderResourceView(Desc.Texture.DirectX12().Resource, &viewDesc, token.CpuHandle);

        return token;
    }

    private DXDescriptorToken CreateUavToken()
    {
        DXDescriptorToken token = context.CbvSrvUavHeap.Allocate();

        UnorderedAccessViewDesc viewDesc = new()
        {
            Format = DXFormats.DirectX12(Desc.Format)
        };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                viewDesc.ViewDimension = UavDimension.Texture1D;
                viewDesc.Texture1D = new()
                {
                    MipSlice = Desc.Range.BaseMipLevel
                };
                break;

            case TextureType.Texture2D:
                viewDesc.ViewDimension = UavDimension.Texture2D;
                viewDesc.Texture2D = new()
                {
                    MipSlice = Desc.Range.BaseMipLevel
                };
                break;

            case TextureType.Texture3D:
                viewDesc.ViewDimension = UavDimension.Texture3D;
                viewDesc.Texture3D = new()
                {
                    MipSlice = Desc.Range.BaseMipLevel,
                    FirstWSlice = 0,
                    WSize = Desc.Texture.Desc.Depth
                };
                break;

            case TextureType.TextureCube:
                viewDesc.ViewDimension = UavDimension.Texture2Darray;
                viewDesc.Texture2DArray = new()
                {
                    MipSlice = Desc.Range.BaseMipLevel,
                    FirstArraySlice = Desc.Range.BaseArrayLayer,
                    ArraySize = Desc.Range.LayerCount
                };
                break;

            case TextureType.Texture1DArray:
                viewDesc.ViewDimension = UavDimension.Texture1Darray;
                viewDesc.Texture1DArray = new()
                {
                    MipSlice = Desc.Range.BaseMipLevel,
                    FirstArraySlice = Desc.Range.BaseArrayLayer,
                    ArraySize = Desc.Range.LayerCount
                };
                break;

            case TextureType.Texture2DArray:
            case TextureType.TextureCubeArray:
                viewDesc.ViewDimension = UavDimension.Texture2Darray;
                viewDesc.Texture2DArray = new()
                {
                    MipSlice = Desc.Range.BaseMipLevel,
                    FirstArraySlice = Desc.Range.BaseArrayLayer,
                    ArraySize = Desc.Range.LayerCount
                };
                break;
        }

        context.Device.CreateUnorderedAccessView(Desc.Texture.DirectX12().Resource, default(ID3D12Resource*), &viewDesc, token.CpuHandle);

        return token;
    }
}
