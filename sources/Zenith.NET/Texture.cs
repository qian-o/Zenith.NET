namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context), IBindableResource
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract MappedMemory Map(TextureSlice slice);

    public abstract void Unmap();

    public void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent) where T : unmanaged
    {
        if (data.Length is 0)
        {
            return;
        }

        if (desc.Type is TextureType.Texture2D && desc.Flags.HasFlag(TextureUsageFlags.Dynamic))
        {
            MappedMemory mappedMemory = Map(slice);

            unsafe
            {
                byte* destination = (byte*)mappedMemory.Pointer + (offset.Y * mappedMemory.RowPitch) + (offset.X * ZenithHelper.SizeInBytes(desc.Format));

                for (uint y = 0; y < extent.Height; y++)
                {
                    uint sourceRowOffset = y * extent.Width;
                    uint destinationRowOffset = y * mappedMemory.RowPitch;

                    data.Slice((int)sourceRowOffset, (int)extent.Width).CopyTo(new(destination + destinationRowOffset, (int)extent.Width));
                }
            }

            Unmap();
        }
        else
        {
            CommandBuffer commandBuffer = Context.Copy.CommandBuffer();

            commandBuffer.Upload(this, slice, offset, extent, data);
            commandBuffer.Submit();

            Context.Copy.WaitIdle();
        }
    }
}
