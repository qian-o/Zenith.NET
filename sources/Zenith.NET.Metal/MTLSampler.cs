using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLSampler : Sampler
{
    public MTLSamplerState SamplerState;

    public MTLSampler(MTLGraphicsContext context, SamplerDesc desc) : base(context, desc)
    {
        SamplerState = context.Device.MakeSamplerState(new()
        {
            MinFilter = MTLFormats.Metal(desc.MinFilter).MinMagFilter,
            MagFilter = MTLFormats.Metal(desc.MagFilter).MinMagFilter,
            MipFilter = MTLFormats.Metal(desc.MipFilter).MipFilter,
            MaxAnisotropy = desc.MaxAnisotropy,
            SAddressMode = MTLFormats.Metal(desc.AddressU),
            TAddressMode = MTLFormats.Metal(desc.AddressV),
            RAddressMode = MTLFormats.Metal(desc.AddressW),
            BorderColor = MTLFormats.Metal(desc.BorderColor),
            NormalizedCoordinates = true,
            LodMinClamp = desc.MinLod,
            LodMaxClamp = desc.MaxLod,
            LodBias = desc.LodBias,
            CompareFunction = MTLFormats.Metal(desc.CompareOp),
            SupportArgumentBuffers = true
        });

        Handle = SamplerState.GpuResourceID.Impl.ToHandle();
    }

    public override ResourceHandle Handle { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        SamplerState.Dispose();
    }
}
