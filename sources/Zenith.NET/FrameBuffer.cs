namespace Zenith.NET;

public abstract class FrameBuffer(GraphicsContext context, FrameBufferDesc desc) : GraphicsResource(context)
{
    public FrameBufferDesc Desc { get; } = desc;

    public abstract uint ColorAttachmentCount { get; }

    public abstract bool HasDepthStencilAttachment { get; }

    public abstract uint Width { get; }

    public abstract uint Height { get; }

    public abstract Output Output { get; }
}
