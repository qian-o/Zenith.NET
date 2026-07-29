using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTexture : Texture
{
    private readonly Dictionary<TextureSubresource, DXDescriptorToken> rtvTokens = [];
    private readonly Dictionary<TextureSubresource, DXDescriptorToken> dsvTokens = [];

    public ComPtr<ID3D12Resource> Resource;

    public DXTexture(DXGraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        ResourceDesc1 resourceDesc = ResourceDesc(desc);

        HeapProperties heapProperties = new() { Type = DxHeapType.Default };

        context.Device.CreateCommittedResource3(&heapProperties,
                                                HeapFlags.None,
                                                &resourceDesc,
                                                BarrierLayout.Undefined,
                                                default(ClearValue*),
                                                default(ID3D12ProtectedResourceSession*),
                                                0,
                                                default(Format*),
                                                SilkMarshal.GuidPtrOf<ID3D12Resource>(),
                                                (void**)Resource.GetAddressOf()).Success();

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = TextureSubresourceRange.All(this)
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
            Range = TextureSubresourceRange.All(this)
        });
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXTextureView View { get; }

    public override ResourceHandle SampledHandle => View.SampledHandle;

    public override ResourceHandle StorageHandle => View.StorageHandle;

    public uint SubresourceIndex(TextureSubresource subresource)
    {
        return subresource.MipLevel + (subresource.ArrayLayer * Desc.MipLevels);
    }

    public CpuDescriptorHandle GetRtvHandle(TextureSubresource subresource)
    {
        if (!rtvTokens.TryGetValue(subresource, out DXDescriptorToken token))
        {
            rtvTokens[subresource] = token = Context.RtvHeap.Allocate();

            RenderTargetViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Format) };

            switch (Desc.Type)
            {
                case TextureType.Texture1D:
                    viewDesc.ViewDimension = RtvDimension.Texture1D;
                    viewDesc.Texture1D = new() { MipSlice = subresource.MipLevel };
                    break;

                case TextureType.Texture1DArray:
                    viewDesc.ViewDimension = RtvDimension.Texture1Darray;
                    viewDesc.Texture1DArray = new()
                    {
                        MipSlice = subresource.MipLevel,
                        FirstArraySlice = subresource.ArrayLayer,
                        ArraySize = 1
                    };
                    break;

                case TextureType.Texture2D:
                    if (Desc.SampleCount is SampleCount.Count1)
                    {
                        viewDesc.ViewDimension = RtvDimension.Texture2D;
                        viewDesc.Texture2D = new() { MipSlice = subresource.MipLevel };
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
                        viewDesc.Texture2DArray = new()
                        {
                            MipSlice = subresource.MipLevel,
                            FirstArraySlice = subresource.ArrayLayer,
                            ArraySize = 1
                        };
                    }
                    else
                    {
                        viewDesc.ViewDimension = RtvDimension.Texture2Dmsarray;
                        viewDesc.Texture2DMSArray = new()
                        {
                            FirstArraySlice = subresource.ArrayLayer,
                            ArraySize = 1
                        };
                    }
                    break;

                case TextureType.Texture3D:
                    viewDesc.ViewDimension = RtvDimension.Texture3D;
                    viewDesc.Texture3D = new()
                    {
                        MipSlice = subresource.MipLevel,
                        FirstWSlice = subresource.ArrayLayer,
                        WSize = 1
                    };
                    break;
            }

            Context.Device.CreateRenderTargetView(Resource, &viewDesc, token.CpuHandle);
        }

        return token.CpuHandle;
    }

    public CpuDescriptorHandle GetDsvHandle(TextureSubresource subresource)
    {
        if (!dsvTokens.TryGetValue(subresource, out DXDescriptorToken token))
        {
            dsvTokens[subresource] = token = Context.DsvHeap.Allocate();

            DepthStencilViewDesc viewDesc = new() { Format = DXFormats.DirectX12(Desc.Format) };

            switch (Desc.Type)
            {
                case TextureType.Texture1D:
                    viewDesc.ViewDimension = DsvDimension.Texture1D;
                    viewDesc.Texture1D = new() { MipSlice = subresource.MipLevel };
                    break;

                case TextureType.Texture1DArray:
                    viewDesc.ViewDimension = DsvDimension.Texture1Darray;
                    viewDesc.Texture1DArray = new()
                    {
                        MipSlice = subresource.MipLevel,
                        FirstArraySlice = subresource.ArrayLayer,
                        ArraySize = 1
                    };
                    break;

                case TextureType.Texture2D:
                    if (Desc.SampleCount is SampleCount.Count1)
                    {
                        viewDesc.ViewDimension = DsvDimension.Texture2D;
                        viewDesc.Texture2D = new() { MipSlice = subresource.MipLevel };
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
                        viewDesc.Texture2DArray = new()
                        {
                            MipSlice = subresource.MipLevel,
                            FirstArraySlice = subresource.ArrayLayer,
                            ArraySize = 1
                        };
                    }
                    else
                    {
                        viewDesc.ViewDimension = DsvDimension.Texture2Dmsarray;
                        viewDesc.Texture2DMSArray = new()
                        {
                            FirstArraySlice = subresource.ArrayLayer,
                            ArraySize = 1
                        };
                    }
                    break;
            }

            Context.Device.CreateDepthStencilView(Resource, &viewDesc, token.CpuHandle);
        }

        return token.CpuHandle;
    }

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
        foreach (DXDescriptorToken token in rtvTokens.Values)
        {
            token.Dispose();
        }
        rtvTokens.Clear();

        foreach (DXDescriptorToken token in dsvTokens.Values)
        {
            token.Dispose();
        }
        dsvTokens.Clear();

        View.Dispose();
        Resource.Dispose();
    }

    public static ResourceDesc1 ResourceDesc(TextureDesc desc)
    {
        return new()
        {
            Dimension = DXFormats.DirectX12(desc.Type),
            Width = desc.Width,
            Height = desc.Height,
            DepthOrArraySize = (ushort)(desc.Type is TextureType.Texture3D ? desc.Depth : desc.ArrayLayers),
            MipLevels = (ushort)desc.MipLevels,
            Format = DXFormats.DirectX12(desc.Format),
            SampleDesc = DXFormats.DirectX12(desc.SampleCount),
            Flags = DXFormats.DirectX12(desc.Usages)
        };
    }
}
