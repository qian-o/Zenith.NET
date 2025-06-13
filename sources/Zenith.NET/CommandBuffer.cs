using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    protected CommandQueue Queue { get; } = queue;

    #region State Management
    public abstract void Begin();

    public abstract void End();

    public virtual void Reset()
    {
        Context.Uploader!.Release(this);
    }

    public virtual void Submit()
    {
        Queue.Submit(this);
    }
    #endregion

    #region Buffer Operations
    public void UpdateBuffer<T>(Buffer buffer, ReadOnlySpan<T> data, uint offsetInBytes = 0) where T : unmanaged
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader!.Buffer(this, sizeInBytes);

        temporary.Upload(data, offsetInBytes);

        CopyBuffer(temporary, buffer, sizeInBytes, 0, offsetInBytes);
    }

    public abstract void CopyBuffer(Buffer source,
                                    Buffer destination,
                                    uint sizeInBytes = 0,
                                    uint sourceOffsetInBytes = 0,
                                    uint destinationOffsetInBytes = 0);
    #endregion

    #region Texture Operations
    public abstract void UpdateTexture<T>(Texture texture,
                                          ReadOnlySpan<T> data,
                                          TexturePosition position,
                                          uint width,
                                          uint height,
                                          uint depth) where T : unmanaged;

    public abstract void CopyTexture(Texture source,
                                     TexturePosition sourcePosition,
                                     Texture destination,
                                     TexturePosition destinationPosition,
                                     uint width,
                                     uint height,
                                     uint depth);

    public abstract void ResolveTexture(Texture source,
                                        TexturePosition sourcePosition,
                                        Texture destination,
                                        TexturePosition destinationPosition);
    #endregion
}
