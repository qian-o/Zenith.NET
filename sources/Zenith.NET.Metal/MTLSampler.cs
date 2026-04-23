using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSampler : Sampler
{
    public MTLSamplerState SamplerState;

    public MTLSampler(MTLGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        MTLSamplerDescriptor descriptor = new()
        {
            MinFilter = MTLFormats.Metal(desc.Filter).MinFilter,
            MagFilter = MTLFormats.Metal(desc.Filter).MagFilter,
            MipFilter = MTLFormats.Metal(desc.Filter).MipFilter,
            MaxAnisotropy = desc.Filter is Filter.Anisotropic ? desc.MaxAnisotropy : 1,
            SAddressMode = MTLFormats.Metal(desc.U),
            TAddressMode = MTLFormats.Metal(desc.V),
            RAddressMode = MTLFormats.Metal(desc.W),
            BorderColor = MTLFormats.Metal(desc.BorderColor),
            LodMinClamp = desc.MinLod,
            LodMaxClamp = desc.MaxLod,
            LodBias = desc.LodBias,
            CompareFunction = MTLFormats.Metal(desc.ComparisonFunc),
            SupportArgumentBuffers = true
        };

        SamplerState = context.Device.MakeSamplerState(descriptor);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        SamplerState.Dispose();
    }
}
