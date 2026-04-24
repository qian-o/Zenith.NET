using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTexture : Texture
{
    public ComPtr<ID3D12Resource> Resource;

    public DXTexture(DXGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        bool isRenderTargetOrDepthStencil = desc.Flags.HasFlag(TextureUsageFlags.RenderTarget) || desc.Flags.HasFlag(TextureUsageFlags.DepthStencil);

        ResourceDesc resourceDesc = new()
        {
            Dimension = DXFormats.DirectX12(desc.Type),
            Width = desc.Width,
            Height = desc.Height,
            DepthOrArraySize = (ushort)(desc.Type is TextureType.Texture3D ? desc.Depth : desc.ArrayLayers),
            MipLevels = (ushort)desc.MipLevels,
            Format = DXFormats.DirectX12(desc.Format),
            SampleDesc = DXFormats.DirectX12(desc.SampleCount),
            Flags = DXFormats.DirectX12(desc.Flags).Flags
        };

        Heap = new(context, resourceDesc, HeapType.Default, isRenderTargetOrDepthStencil ? HeapFlags.AllowOnlyRTDSTextures : HeapFlags.AllowOnlyNonRTDSTextures);

        if (isRenderTargetOrDepthStencil)
        {
            DxClearValue clearValue = new() { Format = DXFormats.DirectX12(desc.Format) };

            if (desc.Flags.HasFlag(TextureUsageFlags.RenderTarget))
            {
                clearValue.Anonymous.Color[3] = 1.0f;
            }

            if (desc.Flags.HasFlag(TextureUsageFlags.DepthStencil))
            {
                clearValue.Anonymous.DepthStencil.Depth = 1.0f;
            }

            context.Device.CreatePlacedResource(Heap.Heap, 0, &resourceDesc, DXFormats.DirectX12(desc.Flags).States, &clearValue, out Resource).Success();
        }
        else
        {
            context.Device.CreatePlacedResource(Heap.Heap, 0, &resourceDesc, DXFormats.DirectX12(desc.Flags).States, null, out Resource).Success();
        }

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = new()
            {
                BaseMipLevel = 0,
                LevelCount = desc.MipLevels,
                BaseArrayLayer = 0,
                LayerCount = desc.ArrayLayers
            }
        });
    }

    public DXTexture(DXGraphicsContext context, TextureDesc desc, ComPtr<ID3D12Resource> resource) : base(context, desc)
    {
        Resource = resource;

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = new()
            {
                BaseMipLevel = 0,
                LevelCount = desc.MipLevels,
                BaseArrayLayer = 0,
                LayerCount = desc.ArrayLayers
            }
        });
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXHeap? Heap { get; }

    public DXTextureView View { get; }

    public uint SubresourceIndex(TextureSubresource subresource)
    {
        return (subresource.ArrayLayer * Desc.MipLevels) + subresource.MipLevel;
    }

    public DXDescriptorToken CreateRtvToken(TextureSubresource subresource)
    {
        DXDescriptorToken token = Context.RtvAllocator.Allocate(1);

        RenderTargetViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Format) };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = RtvDimension.Texture1D;
                    viewDesc.Texture1D.MipSlice = subresource.MipLevel;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = RtvDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MipSlice = subresource.MipLevel;
                    viewDesc.Texture1DArray.FirstArraySlice = subresource.ArrayLayer;
                    viewDesc.Texture1DArray.ArraySize = 1;
                }
                break;

            case TextureType.Texture2D:
                if (Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = RtvDimension.Texture2D;
                    viewDesc.Texture2D.MipSlice = subresource.MipLevel;
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
                    viewDesc.Texture2DArray.MipSlice = subresource.MipLevel;
                    viewDesc.Texture2DArray.FirstArraySlice = subresource.ArrayLayer;
                    viewDesc.Texture2DArray.ArraySize = 1;
                }
                else
                {
                    viewDesc.ViewDimension = RtvDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = subresource.ArrayLayer;
                    viewDesc.Texture2DMSArray.ArraySize = 1;
                }
                break;

            case TextureType.Texture3D:
                {
                    viewDesc.ViewDimension = RtvDimension.Texture3D;
                    viewDesc.Texture3D.MipSlice = subresource.MipLevel;
                    viewDesc.Texture3D.WSize = Desc.Depth;
                }
                break;
        }

        Context.Device.CreateRenderTargetView(Resource, &viewDesc, token.Handle);

        return token;
    }

    public DXDescriptorToken CreateDsvToken(TextureSubresource subresource)
    {
        DXDescriptorToken token = Context.DsvAllocator.Allocate(1);

        DepthStencilViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Format) };

        switch (Desc.Type)
        {
            case TextureType.Texture1D:
                {
                    viewDesc.ViewDimension = DsvDimension.Texture1D;
                    viewDesc.Texture1D.MipSlice = subresource.MipLevel;
                }
                break;

            case TextureType.Texture1DArray:
                {
                    viewDesc.ViewDimension = DsvDimension.Texture1Darray;
                    viewDesc.Texture1DArray.MipSlice = subresource.MipLevel;
                    viewDesc.Texture1DArray.FirstArraySlice = subresource.ArrayLayer;
                    viewDesc.Texture1DArray.ArraySize = 1;
                }
                break;

            case TextureType.Texture2D:
            case TextureType.Texture3D:
                if (Desc.SampleCount is SampleCount.Count1)
                {
                    viewDesc.ViewDimension = DsvDimension.Texture2D;
                    viewDesc.Texture2D.MipSlice = subresource.MipLevel;
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
                    viewDesc.Texture2DArray.MipSlice = subresource.MipLevel;
                    viewDesc.Texture2DArray.FirstArraySlice = subresource.ArrayLayer;
                    viewDesc.Texture2DArray.ArraySize = 1;
                }
                else
                {
                    viewDesc.ViewDimension = DsvDimension.Texture2Dmsarray;
                    viewDesc.Texture2DMSArray.FirstArraySlice = subresource.ArrayLayer;
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

        Heap?.Dispose();
    }
}
