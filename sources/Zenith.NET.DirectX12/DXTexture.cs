using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTexture : Texture
{
    public ComPtr<ID3D12Resource> Resource;

    public DXTexture(GraphicsContext context, TextureDesc desc) : base(context, desc)
    {
        ResourceDesc resourceDesc = new()
        {
            Dimension = DXFormats.DirectX12(desc.Type),
            Width = desc.Width,
            Height = desc.Height,
            DepthOrArraySize = (ushort)(desc.Type is TextureType.Texture3D ? desc.Depth : desc.ArrayLayers),
            MipLevels = (ushort)desc.MipLevels,
            Format = DXFormats.DirectX12(desc.Format),
            SampleDesc = DXFormats.DirectX12(desc.SampleCount),
            Layout = desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? TextureLayout.LayoutRowMajor : TextureLayout.LayoutUnknown,
            Flags = DXFormats.DirectX12(desc.Flags).ResourceFlags
        };

        HeapProperties heapProperties = new(HeapType.Default);

        if (desc.Flags.HasFlag(TextureUsageFlags.RenderTarget) || desc.Flags.HasFlag(TextureUsageFlags.DepthStencil))
        {
            DxClearValue clearValue = new() { Format = DXFormats.DirectX12(desc.Format) };

            if (desc.Flags.HasFlag(TextureUsageFlags.DepthStencil))
            {
                clearValue.DepthStencil = new() { Depth = 1.0f };
            }

            Context.Device.CreateCommittedResource(&heapProperties,
                                                   desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? HeapFlags.SharedCrossAdapter : HeapFlags.None,
                                                   &resourceDesc,
                                                   DXFormats.DirectX12(desc.Flags).ResourceStates,
                                                   &clearValue,
                                                   out Resource).Success();
        }
        else
        {
            Context.Device.CreateCommittedResource(&heapProperties,
                                                   desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? HeapFlags.SharedCrossAdapter : HeapFlags.None,
                                                   &resourceDesc,
                                                   DXFormats.DirectX12(desc.Flags).ResourceStates,
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
        Array.Fill(States, DXFormats.DirectX12(desc.Flags).ResourceStates);
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXTextureView View { get; }

    public ResourceStates[] States { get; }

    public override MappedMemory Map(TextureSlice slice)
    {
        ResourceDesc desc = Resource.GetDesc();

        PlacedSubresourceFootprint footprint;
        uint numRows;
        ulong rowSizeInBytes;
        ulong totalBytes;
        Context.Device.GetCopyableFootprints(&desc,
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
