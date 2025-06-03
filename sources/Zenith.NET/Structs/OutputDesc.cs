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

    /// <summary>
    /// Color attachment formats.
    /// </summary>
    public PixelFormat[] ColorAttachments { get; set; }

    /// <summary>
    /// Depth stencil attachment format.
    /// </summary>
    public PixelFormat? DepthStencilAttachment { get; set; }

    /// <summary>
    /// The number of samples in each target attachment.
    /// </summary>
    public SampleCount SampleCount { get; set; }

    public readonly bool Validate()
    {
        if (ColorAttachments is null)
        {
            return false;
        }

        if (Validation.IsValidDepthStencilFormat(DepthStencilAttachment))
        {
            return false;
        }

        return true;
    }
}
