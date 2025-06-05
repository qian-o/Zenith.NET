namespace Zenith.NET;

/// <summary>
/// Represents a framebuffer object, which holds references to color and depth/stencil attachments for rendering.
/// </summary>
public abstract class FrameBuffer(GraphicsContext context, FrameBufferDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the framebuffer description.
    /// </summary>
    public FrameBufferDesc Desc { get; } = desc;

    /// <summary>
    /// Gets the number of color attachments.
    /// </summary>
    public abstract uint ColorAttachmentCount { get; }

    /// <summary>
    /// Gets a value indicating whether the framebuffer has a depth/stencil attachment.
    /// </summary>
    public abstract bool HasDepthStencilAttachment { get; }

    /// <summary>
    /// Gets the width of the framebuffer, in pixels.
    /// </summary>
    public abstract uint Width { get; }

    /// <summary>
    /// Gets the height of the framebuffer, in pixels.
    /// </summary>
    public abstract uint Height { get; }

    /// <summary>
    /// Gets the output description of the framebuffer.
    /// </summary>
    public abstract OutputDesc Output { get; }
}
