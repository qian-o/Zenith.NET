namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context), IBindableResource
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent) where T : unmanaged
    {
        if (data.Length is 0 || data.Length != extent.Width * extent.Height * extent.Depth)
        {
            return;
        }

        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Upload(this, slice, offset, extent, data);
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
