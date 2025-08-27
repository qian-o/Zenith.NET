using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context), IBindableResource
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract TextureView View { get; }

    public abstract void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent);

    public abstract void Download<T>(Span<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent);

    protected void UploadInternal<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.Upload(this, slice, offset, extent, data);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();
    }

    protected void DownloadInternal<T>(Span<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        using Buffer buffer = Context.Factory.CreateBuffer(new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 1,
            Flags = BufferUsageFlags.Dynamic
        });

        CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

        commandBuffer.Begin();
        commandBuffer.CopyTextureToBuffer(this, slice, offset, extent, buffer, 0);
        commandBuffer.End();
        commandBuffer.Submit();

        Context.Copy.WaitIdle();

        buffer.Download(data, 0);
    }
}
