using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSampler : Sampler
{
    public MTLSamplerState SamplerState;

    public MTLSampler(MTLGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        MTLSamplerDescriptor descriptor = new()
        {
            SAddressMode = MTLFormats.Metal(desc.U),
            TAddressMode = MTLFormats.Metal(desc.V),
            RAddressMode = MTLFormats.Metal(desc.W),
            MinFilter = MTLFormats.Metal(desc.Filter).MinFilter,
            MagFilter = MTLFormats.Metal(desc.Filter).MagFilter,
            MipFilter = MTLFormats.Metal(desc.Filter).MipFilter,
            CompareFunction = MTLFormats.Metal(desc.ComparisonFunc),
            MaxAnisotropy = desc.MaxAnisotropy,
            LodMinClamp = desc.MinLod,
            LodMaxClamp = desc.MaxLod,
            LodBias = desc.LodBias,
            BorderColor = MTLFormats.Metal(desc.BorderColor),
            SupportArgumentBuffers = true
        };

        SamplerState = context.Device.NewSamplerState(descriptor);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        SamplerState.Dispose();
    }
}
