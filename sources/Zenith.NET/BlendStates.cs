namespace Zenith.NET;

public static class BlendStates
{
    public static readonly BlendState Default = new()
    {
        AlphaToCoverageEnable = false,
        IndependentBlendEnable = false,
        RenderTarget0 = new()
        {
            SrcBlend = Blend.One,
            DestBlend = Blend.Zero,
            BlendOp = BlendOp.Add,
            SrcBlendAlpha = Blend.One,
            DestBlendAlpha = Blend.Zero,
            BlendOpAlpha = BlendOp.Add,
            Flags = ColorComponentFlags.All
        }
    };

    public static readonly BlendState Additive = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true,
            SrcBlend = Blend.SrcAlpha,
            DestBlend = Blend.One,
            SrcBlendAlpha = Blend.SrcAlpha,
            DestBlendAlpha = Blend.One
        }
    };

    public static readonly BlendState AlphaBlend = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true,
            SrcBlend = Blend.SrcAlpha,
            DestBlend = Blend.InverseSrcAlpha,
            SrcBlendAlpha = Blend.SrcAlpha,
            DestBlendAlpha = Blend.InverseSrcAlpha
        }
    };

    public static readonly BlendState NonPremultiplied = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true,
            SrcBlend = Blend.SrcAlpha,
            DestBlend = Blend.InverseSrcAlpha,
            SrcBlendAlpha = Blend.SrcAlpha,
            DestBlendAlpha = Blend.InverseSrcAlpha
        }
    };

    public static readonly BlendState Opaque = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true
        }
    };

    public static readonly BlendState ColorDisabled = new()
    {
        RenderTarget0 = Default.RenderTarget0 with
        {
            BlendEnable = true,
            Flags = ColorComponentFlags.None
        }
    };
}
