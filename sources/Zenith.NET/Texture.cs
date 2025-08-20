namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context), IBindableResource
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract TextureView View { get; }

    public void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.Upload(this, slice, offset, extent, data);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
