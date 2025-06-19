namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    public TextureDesc Desc { get; } = desc;

    public void Upload<T>(ReadOnlySpan<T> data, TexturePosition position, uint width, uint height, uint depth)
    {
        CommandBuffer commandBuffer = Context.Copy!.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.UpdateTexture(this, data, position, width, height, depth);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }
}
