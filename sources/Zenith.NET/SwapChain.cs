namespace Zenith.NET;

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract Texture Drawable { get; }

    public CommandSubmission Acquire()
    {
        AcquireImpl();

        CommandBuffer commandBuffer = Context.GraphicsQueue.AcquireCommandBuffer();

        commandBuffer.Barrier([], [], [TextureBarrier.ColorAttachment(Drawable, null)]);

        return commandBuffer.Submit();
    }

    public CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.AcquireCommandBuffer();

        commandBuffer.Barrier([], [], [TextureBarrier.Present(Drawable, TextureBarrier.ColorAttachment(Drawable, null))]);

        CommandSubmission submission = commandBuffer.Submit(waits);

        PresentImpl();

        return submission;
    }

    public void Resize(uint width, uint height)
    {
        desc.Surface.Width = width;
        desc.Surface.Height = height;

        Context.GraphicsQueue.WaitIdle();

        ResizeImpl();

        SetResourceName(Name);
    }

    public void Refresh(Surface surface)
    {
        desc.Surface = surface;

        Context.GraphicsQueue.WaitIdle();

        RefreshImpl();

        SetResourceName(Name);
    }

    protected abstract void AcquireImpl();

    protected abstract void PresentImpl();

    protected abstract void ResizeImpl();

    protected abstract void RefreshImpl();
}
