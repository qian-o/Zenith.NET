namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context), IBindableResource
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract TextureView View { get; }

    public abstract MappedMemory Map(TextureSlice slice);

    public abstract void Unmap();

    public void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent) where T : unmanaged
    {
        if (offset.X is 0 && offset.Y is 0 && offset.Z is 0)
        {
            MappedMemory mappedMemory = Map(slice);

            ZenithMarshal.Copy(data, mappedMemory.Pointer);

            Unmap();
        }
        else
        {
            CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

            commandBuffer.Begin();
            commandBuffer.Upload(this, slice, offset, extent, data);
            commandBuffer.End();
            commandBuffer.Submit();

            Context.Copy.WaitIdle();
        }
    }
}
