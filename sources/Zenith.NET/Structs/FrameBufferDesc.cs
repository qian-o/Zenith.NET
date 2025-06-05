namespace Zenith.NET;

/// <summary>
/// Describes a framebuffer, including its color and depth/stencil attachments.
/// </summary>
public record struct FrameBufferDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FrameBufferDesc"/> struct with default values.
    /// </summary>
    public FrameBufferDesc()
    {
        ColorTargets = [];
        DepthStencilTarget = null;
    }

    /// <summary>
    /// An array of color texture attachments.
    /// </summary>
    public FrameBufferAttachmentDesc[] ColorTargets { get; set; }

    /// <summary>
    /// The depth/stencil texture attachment.
    /// </summary>
    public FrameBufferAttachmentDesc? DepthStencilTarget { get; set; }

    /// <summary>
    /// Validates the current <see cref="FrameBufferDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (ColorTargets is null)
        {
            return false;
        }

        if (ColorTargets.Any(static item => !item.Validate()))
        {
            return false;
        }

        if (DepthStencilTarget?.Validate() is false)
        {
            return false;
        }

        return true;
    }
}
