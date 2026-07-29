namespace Zenith.NET;

public struct BlendState
{
    public bool IsAlphaToCoverageEnabled;

    public bool IsIndependentBlendEnabled;

    public ColorAttachmentBlendState ColorAttachment0;

    public ColorAttachmentBlendState ColorAttachment1;

    public ColorAttachmentBlendState ColorAttachment2;

    public ColorAttachmentBlendState ColorAttachment3;

    public ColorAttachmentBlendState ColorAttachment4;

    public ColorAttachmentBlendState ColorAttachment5;

    public ColorAttachmentBlendState ColorAttachment6;

    public ColorAttachmentBlendState ColorAttachment7;

    public static BlendState Opaque()
    {
        return new()
        {
            IsAlphaToCoverageEnabled = false,
            IsIndependentBlendEnabled = false,
            ColorAttachment0 = ColorAttachmentBlendState.Opaque()
        };
    }

    public static BlendState AlphaBlend()
    {
        return new()
        {
            IsAlphaToCoverageEnabled = false,
            IsIndependentBlendEnabled = false,
            ColorAttachment0 = ColorAttachmentBlendState.AlphaBlend()
        };
    }

    public static BlendState Additive()
    {
        return new()
        {
            IsAlphaToCoverageEnabled = false,
            IsIndependentBlendEnabled = false,
            ColorAttachment0 = ColorAttachmentBlendState.Additive()
        };
    }

    public static BlendState NonPremultiplied()
    {
        return new()
        {
            IsAlphaToCoverageEnabled = false,
            IsIndependentBlendEnabled = false,
            ColorAttachment0 = ColorAttachmentBlendState.NonPremultiplied()
        };
    }

    public static BlendState ColorDisabled()
    {
        return new()
        {
            IsAlphaToCoverageEnabled = false,
            IsIndependentBlendEnabled = false,
            ColorAttachment0 = ColorAttachmentBlendState.ColorDisabled()
        };
    }
}
