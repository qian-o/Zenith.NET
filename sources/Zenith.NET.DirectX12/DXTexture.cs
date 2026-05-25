using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXTexture : Texture
{
    public ComPtr<ID3D12Resource> Resource;

    public DXTexture(DXGraphicsContext context, TextureDesc desc, ComPtr<ID3D12Resource>? resource) : base(context, desc)
    {
        if (resource is null)
        {
            ResourceDesc1 resourceDesc = ResourceDesc(desc);

            HeapProperties heapProperties = new(DxHeapType.Default);

            context.Device10.CreateCommittedResource3(&heapProperties,
                                                      HeapFlags.None,
                                                      &resourceDesc,
                                                      BarrierLayout.Undefined,
                                                      default,
                                                      default(ComPtr<ID3D12ProtectedResourceSession>),
                                                      0,
                                                      default,
                                                      out Resource).Success();
        }
        else
        {
            Resource = resource.Value;
        }

        View = new(context, new()
        {
            Texture = this,
            Type = desc.Type,
            Format = desc.Format,
            Range = new()
            {
                LevelCount = desc.MipLevels,
                LayerCount = desc.ArrayLayers
            }
        });
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
            Layout = TextureLayout.LayoutUnknown,
            Flags = DXFormats.DirectX12(desc.Usages)
        };
    }
}
