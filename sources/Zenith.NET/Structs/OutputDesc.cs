using Zenith.NET.Helpers;

namespace Zenith.NET;

/// <summary>
/// Describes the output configuration for a render pass, including color and depth/stencil formats and sample count.
/// </summary>
public record struct OutputDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutputDesc"/> struct with default values.
    /// </summary>
    public OutputDesc()
    {
        ColorAttachments = [];
        DepthStencilAttachment = null;
        SampleCount = SampleCount.Count1;
    }

    /// <summary>
    /// The formats of the color attachments.
    /// </summary>
    public PixelFormat[] ColorAttachments { get; set; }

    /// <summary>
    /// The format of the depth/stencil attachment.
    /// </summary>
    public PixelFormat? DepthStencilAttachment { get; set; }

    /// <summary>
    /// The number of samples for each target attachment.
    /// </summary>
    public SampleCount SampleCount { get; set; }

    /// <summary>
    /// Validates the current <see cref="OutputDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
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
