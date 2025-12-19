using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTexture : Texture
{
    public ComPtr<ID3D12Resource> Resource;

    public DXTexture(DXGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        ResourceDesc resourceDesc = new()
        {
            Dimension = DXFormats.DirectX12(desc.Type),
            Width = desc.Width,
            Height = desc.Height,
            DepthOrArraySize = (ushort)(desc.Type is TextureType.Texture3D ? desc.Depth : ZenithHelper.FlattenArrayLayerCount(desc)),
            MipLevels = (ushort)desc.MipLevels,
            Format = DXFormats.DirectX12(desc.Format),
            SampleDesc = DXFormats.DirectX12(desc.SampleCount),
            Layout = desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? TextureLayout.LayoutRowMajor : TextureLayout.LayoutUnknown,
            Flags = DXFormats.DirectX12(desc.Flags).Flags
        };

        HeapProperties heapProperties = new(HeapType.Default);

        if (desc.Flags.HasFlag(TextureUsageFlags.RenderTarget) || desc.Flags.HasFlag(TextureUsageFlags.DepthStencil))
        {
            DxClearValue clearValue = new() { Format = DXFormats.DirectX12(desc.Format) };

            if (desc.Flags.HasFlag(TextureUsageFlags.DepthStencil))
            {
                clearValue.DepthStencil = new() { Depth = 1.0f };
            }

            context.Device.CreateCommittedResource(&heapProperties,
                                                   desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? HeapFlags.SharedCrossAdapter : HeapFlags.None,
                                                   &resourceDesc,
                                                   DXFormats.DirectX12(desc.Flags).States,
                                                   &clearValue,
                                                   out Resource).Success();
        }
        else
        {
            context.Device.CreateCommittedResource(&heapProperties,
                                                   desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? HeapFlags.SharedCrossAdapter : HeapFlags.None,
                                                   &resourceDesc,
                                                   DXFormats.DirectX12(desc.Flags).States,
                                                   null,
                                                   out Resource).Success();
        }

        View = new(context, new()
        {
            Texture = this,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels,
            FirstArrayLayer = 0,
            ArrayLayerCount = desc.ArrayLayers
        });

        States = new ResourceStates[ZenithHelper.SubresourceCount(desc)];
        Array.Fill(States, DXFormats.DirectX12(desc.Flags).States);
    }

    public DXTexture(DXGraphicsContext context, TextureDesc desc, ComPtr<ID3D12Resource> resource) : base(context, desc)
    {
        Resource = resource;

        View = new(context, new()
        {
            Texture = this,
            FirstMipLevel = 0,
            MipLevelCount = desc.MipLevels,
            FirstArrayLayer = 0,
            ArrayLayerCount = desc.ArrayLayers
        });

        States = new ResourceStates[ZenithHelper.SubresourceCount(desc)];
        Array.Fill(States, ResourceStates.Common);
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXTextureView View { get; }

    public ResourceStates[] States { get; }

    public override MappedMemory Map(TextureSlice slice)
    {
        ResourceDesc resourceDesc = Resource.GetDesc();

        PlacedSubresourceFootprint footprint;
        uint numRows;
        ulong rowSizeInBytes;
        ulong totalBytes;
        Context.Device.GetCopyableFootprints(&resourceDesc,
                                             ZenithHelper.SubresourceIndex(Desc, slice),
                                             1,
                                             0,
                                             &footprint,
                                             &numRows,
                                             &rowSizeInBytes,
                                             &totalBytes);

        DxRange range = new()
        {
            Begin = (uint)footprint.Offset,
            End = (uint)(footprint.Offset + totalBytes)
        };

        void* pointer;
        Resource.Map(0, &range, &pointer).Success();

        return new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = (uint)totalBytes,
            RowPitch = (uint)rowSizeInBytes,
            SlicePitch = (uint)(numRows * rowSizeInBytes)
        };
    }

    public override void Unmap()
    {
        Resource.Unmap(0, (DxRange*)null);
    }

    public DXDescriptorToken CreateRtvToken(TextureSlice slice)
    {
        DXDescriptorToken token = Context.RtvAllocator.Allocate();

        RenderTargetViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Format) };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = RtvDimension.Texture1D;
                    viewDesc.Texture1D.MipSlice = slice.MipLevel;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = RtvDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MipSlice = slice.MipLevel;
                    viewDesc.Texture1DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerIndex(Desc, slice);
                    viewDesc.Texture1DArray.ArraySize = 1;
                }
                break;

            case TextureType.Texture2D:
                if (Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = RtvDimension.Texture2D;
                    viewDesc.Texture2D.MipSlice = slice.MipLevel;
                }
                else
                {
                    viewDesc.ViewDimension = RtvDimension.Texture2Dms;
                }
                break;

            case TextureType.Texture2DArray:
            case TextureType.TextureCube:
            case TextureType.TextureCubeArray:
                if (Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = RtvDimension.Texture2Darray;
                    viewDesc.Texture2DArray.MipSlice = slice.MipLevel;
                    viewDesc.Texture2DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerIndex(Desc, slice);
                    viewDesc.Texture2DArray.ArraySize = 1;
                }
                else
                {
                    viewDesc.ViewDimension = RtvDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerIndex(Desc, slice);
                    viewDesc.Texture2DMSArray.ArraySize = 1;
                }
                break;

            case TextureType.Texture3D:
                {
                    viewDesc.ViewDimension = RtvDimension.Texture3D;
                    viewDesc.Texture3D.MipSlice = slice.MipLevel;
                    viewDesc.Texture3D.WSize = Desc.Depth;
                }
                break;
        }

        Context.Device.CreateRenderTargetView(Resource, &viewDesc, token.Handle);

        return token;
    }

    public DXDescriptorToken CreateDsvToken(TextureSlice slice)
    {
        DXDescriptorToken token = Context.DsvAllocator.Allocate();

        DepthStencilViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Format) };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = DsvDimension.Texture1D;
                    viewDesc.Texture1D.MipSlice = slice.MipLevel;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = DsvDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MipSlice = slice.MipLevel;
                    viewDesc.Texture1DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerIndex(Desc, slice);
                    viewDesc.Texture1DArray.ArraySize = 1;
                }
                break;

            case TextureType.Texture2D:
            case TextureType.Texture3D:
                if (Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = DsvDimension.Texture2D;
                    viewDesc.Texture2D.MipSlice = slice.MipLevel;
                }
                else
                {
                    viewDesc.ViewDimension = DsvDimension.Texture2Dms;
                }
                break;

            case TextureType.Texture2DArray:
            case TextureType.TextureCube:
            case TextureType.TextureCubeArray:
                if (Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = DsvDimension.Texture2Darray;
                    viewDesc.Texture2DArray.MipSlice = slice.MipLevel;
                    viewDesc.Texture2DArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerIndex(Desc, slice);
                    viewDesc.Texture2DArray.ArraySize = 1;
                }
                else
                {
                    viewDesc.ViewDimension = DsvDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = ZenithHelper.FlattenArrayLayerIndex(Desc, slice);
                    viewDesc.Texture2DMSArray.ArraySize = 1;
                }
                break;
        }

        Context.Device.CreateDepthStencilView(Resource, &viewDesc, token.Handle);

        return token;
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
