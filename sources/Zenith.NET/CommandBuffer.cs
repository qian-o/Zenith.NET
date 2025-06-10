namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public abstract void Begin();

    public abstract void End();

    public virtual void Reset()
    {
        Context.Uploader!.Release(this);
    }

    public virtual void Submit()
    {
        queue.Submit(this);
    }
}
