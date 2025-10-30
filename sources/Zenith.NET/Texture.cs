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
        if (desc.Flags.HasFlag(TextureUsageFlags.Dynamic))
        {
            if (data.Length != extent.Width * extent.Height * extent.Depth)
            {
                return;
            }

            MappedMemory mappedMemory = Map(slice);

            unsafe
            {
                byte* destination = (byte*)mappedMemory.Pointer
                                    + (offset.Z * mappedMemory.SlicePitch)
                                    + (offset.Y * mappedMemory.RowPitch)
                                    + (offset.X * ZenithHelper.SizeInBytes(desc.Format));

                for (uint z = 0; z < extent.Depth; z++)
                {
                    uint sourceOffset = z * extent.Height * extent.Width;
                    uint destinationOffset = z * mappedMemory.SlicePitch;

                    for (uint y = 0; y < extent.Height; y++)
                    {
                        uint sourceRowOffset = sourceOffset + (y * extent.Width);
                        uint destinationRowOffset = destinationOffset + (y * mappedMemory.RowPitch);

                        data.Slice((int)sourceRowOffset, (int)extent.Width).CopyTo(new(destination + destinationRowOffset, (int)extent.Width));
                    }
                }
            }

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
