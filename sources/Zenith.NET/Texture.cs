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
            MappedMemory mappedMemory = Map(slice);

            unsafe
            {
                uint offsetInBytes = (offset.Z * mappedMemory.SlicePitch) + (offset.Y * mappedMemory.RowPitch) + (offset.X * ZenithHelper.SizeInBytes(desc.Format));

                for (uint z = 0; z < extent.Depth; z++)
                {
                    uint zSourceOffset = z * extent.Height * extent.Width;
                    uint zDestinationOffset = offsetInBytes + (z * mappedMemory.SlicePitch);

                    for (uint y = 0; y < extent.Height; y++)
                    {
                        uint sourceOffset = zSourceOffset + (y * extent.Width);
                        uint destinationOffset = zDestinationOffset + (y * mappedMemory.RowPitch);

                        data.Slice((int)sourceOffset, (int)extent.Width).CopyTo(new((void*)(mappedMemory.Pointer + destinationOffset), (int)extent.Width));
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
