using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXSampler : Sampler
{
    public DXDescriptorToken Token;

    public DXSampler(DXGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        Token = context.SamplerHeap.Allocate();

        (float r, float g, float b, float a) = DXFormats.DirectX12(desc.BorderColor);

        DxSamplerDesc samplerDesc = new()
        {
            Filter = DXFormats.DirectX12(desc.MinFilter, desc.MagFilter, desc.MipFilter, desc.MaxAnisotropy, desc.CompareOp),
            AddressU = DXFormats.DirectX12(desc.AddressU),
            AddressV = DXFormats.DirectX12(desc.AddressV),
            AddressW = DXFormats.DirectX12(desc.AddressW),
            MipLODBias = desc.LodBias,
            MaxAnisotropy = desc.MaxAnisotropy,
            ComparisonFunc = DXFormats.DirectX12(desc.CompareOp),
            MinLOD = desc.MinLod,
            MaxLOD = desc.MaxLod
        };

        samplerDesc.BorderColor[0] = r;
        samplerDesc.BorderColor[1] = g;
        samplerDesc.BorderColor[2] = b;
        samplerDesc.BorderColor[3] = a;

        context.Device14.CreateSampler(&samplerDesc, Token.CpuHandle);
    }

    public override ResourceHandle Handle => Token.ResourceHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Token.Dispose();
    }
}

