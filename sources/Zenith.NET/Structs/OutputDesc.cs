using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct OutputDesc : IDesc
{
    public OutputDesc()
    {
        ColorAttachments = [];
        DepthStencilAttachment = null;
        SampleCount = SampleCount.Count1;
    }

    public PixelFormat[] ColorAttachments { get; set; }

    public PixelFormat? DepthStencilAttachment { get; set; }

    public SampleCount SampleCount { get; set; }

    public readonly bool Validate()
    {
        if (ColorAttachments is null)
        {
            return false;
        }

        if (ColorAttachments.Any(static item => !Enum.IsDefined(item)))
        {
            return false;
        }

        if (Validation.IsValidDepthStencilFormat(DepthStencilAttachment))
        {
            return false;
        }

        if (!Enum.IsDefined(SampleCount))
        {
            return false;
        }

        return true;
    }
}
