using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXSampler : Sampler
{
    public DXDescriptorToken Token;

    public DXSampler(DXGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        Token = context.SamplerAllocator.Allocate(1);

        DxSamplerDesc samplerDesc = new()
        {
            Filter = DXFormats.DirectX12(desc.Filter, desc.ComparisonFunc).Filter,
            AddressU = DXFormats.DirectX12(desc.U),
            AddressV = DXFormats.DirectX12(desc.V),
            AddressW = DXFormats.DirectX12(desc.W),
            MipLODBias = desc.LodBias,
            MaxAnisotropy = desc.MaxAnisotropy,
            ComparisonFunc = DXFormats.DirectX12(desc.Filter, desc.ComparisonFunc).ComparisonFunc,
            MinLOD = desc.MinLod,
            MaxLOD = desc.MaxLod
        };

        switch (desc.BorderColor)
        {
            case BorderColor.OpaqueBlack:
                samplerDesc.BorderColor[3] = 1.0f;
                break;

            case BorderColor.OpaqueWhite:
                samplerDesc.BorderColor[0] = 1.0f;
                samplerDesc.BorderColor[1] = 1.0f;
                samplerDesc.BorderColor[2] = 1.0f;
                samplerDesc.BorderColor[3] = 1.0f;
                break;
        }

        context.Device.CreateSampler(&samplerDesc, Token.Handle);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Token.Dispose();
    }
}
