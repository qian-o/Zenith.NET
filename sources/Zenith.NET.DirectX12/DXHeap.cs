using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXHeap : GraphicsResource
{
    public ComPtr<ID3D12Heap> Heap;

    public DXHeap(DXGraphicsContext context, DXBuffer buffer, out ResourceDesc resourceDesc) : base(context)
    {
        resourceDesc = new()
        {
            Dimension = ResourceDimension.Buffer,
            Width = ZenithHelper.Align(buffer.Desc.SizeInBytes, 256u),
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            SampleDesc = new(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = DXFormats.DirectX12(buffer.Desc.Flags).Flags
        };

        ResourceAllocationInfo allocationInfo = context.Device.GetResourceAllocationInfo(0, 1, ref resourceDesc);

        HeapDesc desc = new()
        {
            SizeInBytes = allocationInfo.SizeInBytes,
            Properties = new(buffer.Desc.Flags.HasFlag(BufferUsageFlags.Dynamic) ? HeapType.GpuUpload : HeapType.Default),
            Alignment = allocationInfo.Alignment,
            Flags = HeapFlags.AllowOnlyBuffers
        };

        context.Device.CreateHeap(&desc, out Heap).Success();
    }

    public DXHeap(DXGraphicsContext context, DXTexture texture, out ResourceDesc resourceDesc) : base(context)
    {
        resourceDesc = new()
        {
            Dimension = DXFormats.DirectX12(texture.Desc.Type),
            Width = texture.Desc.Width,
            Height = texture.Desc.Height,
            DepthOrArraySize = (ushort)(texture.Desc.Type is TextureType.Texture3D ? texture.Desc.Depth : ZenithHelper.FlattenArrayLayerCount(texture.Desc)),
            MipLevels = (ushort)texture.Desc.MipLevels,
            Format = DXFormats.DirectX12(texture.Desc.Format),
            SampleDesc = DXFormats.DirectX12(texture.Desc.SampleCount),
            Flags = DXFormats.DirectX12(texture.Desc.Flags).Flags
        };

        ResourceAllocationInfo allocationInfo = context.Device.GetResourceAllocationInfo(0, 1, ref resourceDesc);

        HeapDesc desc = new()
        {
            SizeInBytes = allocationInfo.SizeInBytes,
            Properties = new(texture.Desc.Flags.HasFlag(TextureUsageFlags.Dynamic) ? HeapType.GpuUpload : HeapType.Default),
            Alignment = allocationInfo.Alignment,
            Flags = texture.Desc.Flags.HasFlag(TextureUsageFlags.RenderTarget) || texture.Desc.Flags.HasFlag(TextureUsageFlags.DepthStencil) ? HeapFlags.AllowOnlyRTDSTextures : HeapFlags.AllowOnlyNonRTDSTextures
        };

        context.Device.CreateHeap(&desc, out Heap).Success();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Heap.Dispose();
    }
}
