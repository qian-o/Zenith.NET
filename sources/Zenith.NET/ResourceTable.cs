namespace Zenith.NET;

public abstract class ResourceTable(GraphicsContext context, ResourceTableDesc desc) : GraphicsResource(context)
{
    private ResourceTableDesc desc = desc;

    public ref readonly ResourceTableDesc Desc => ref desc;

    public abstract void Write(uint binding, Buffer buffer);

    public abstract void Write(uint binding, BufferRange bufferRange);

    public abstract void Write(uint binding, Texture texture);

    public abstract void Write(uint binding, TextureView textureView);

    public abstract void Write(uint binding, Sampler sampler);

    public abstract void Write(uint binding, TopLevelAccelerationStructure topLevelAccelerationStructure);

    public abstract void Write(uint binding, ReadOnlySpan<Buffer> buffers);

    public abstract void Write(uint binding, ReadOnlySpan<BufferRange> bufferRanges);

    public abstract void Write(uint binding, ReadOnlySpan<Texture> textures);

    public abstract void Write(uint binding, ReadOnlySpan<TextureView> textureViews);

    public abstract void Write(uint binding, ReadOnlySpan<Sampler> samplers);

    public abstract void Write(uint binding, ReadOnlySpan<TopLevelAccelerationStructure> topLevelAccelerationStructures);
}
