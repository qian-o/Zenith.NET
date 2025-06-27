namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context), ITexture
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture upload validation is not implemented yet.");
        }

        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.UpdateTexture(data, this, slice, offset, extent);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
