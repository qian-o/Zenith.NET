namespace Zenith.NET;

internal struct TransferLayout
{
    public nint Pointer;

    public uint RowSizeInBytes;

    public uint SrcRowStrideInBytes;

    public uint DstRowStrideInBytes;

    public uint Rows;

    public readonly void Upload(Buffer buffer)
    {
        MappedMemory mappedMemory = buffer.Map();

        unsafe
        {
            for (uint row = 0; row < Rows; row++)
            {
                new ReadOnlySpan<byte>((void*)(Pointer + (SrcRowStrideInBytes * row)), (int)RowSizeInBytes).CopyTo(new((void*)(mappedMemory.Pointer + (DstRowStrideInBytes * row)), (int)RowSizeInBytes));
            }
        }

        buffer.Unmap();
    }

    public readonly void Download(Buffer buffer)
    {
        MappedMemory mappedMemory = buffer.Map();

        unsafe
        {
            for (uint row = 0; row < Rows; row++)
            {
                new ReadOnlySpan<byte>((void*)(mappedMemory.Pointer + (SrcRowStrideInBytes * row)), (int)RowSizeInBytes).CopyTo(new((void*)(Pointer + (DstRowStrideInBytes * row)), (int)RowSizeInBytes));
            }
        }

        buffer.Unmap();
    }
}
