namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    #region Command Buffer Management
    public abstract void Begin();

    public abstract void End();

    public abstract void Reset();

    public void Submit()
    {
        queue.Submit(this);
    }
    #endregion
}
