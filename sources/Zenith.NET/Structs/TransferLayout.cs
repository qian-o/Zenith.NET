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
        nint pointer = buffer.Map();

        unsafe
        {
            for (uint row = 0; row < Rows; row++)
            {
                new ReadOnlySpan<byte>((void*)(Pointer + (SrcRowStrideInBytes * row)), (int)RowSizeInBytes).CopyTo(new((void*)(pointer + (DstRowStrideInBytes * row)), (int)RowSizeInBytes));
            }
        }

        buffer.Unmap();
    }

    public readonly void Download(Buffer buffer)
    {
        nint pointer = buffer.Map();

        unsafe
        {
            for (uint row = 0; row < Rows; row++)
            {
                new ReadOnlySpan<byte>((void*)(pointer + (SrcRowStrideInBytes * row)), (int)RowSizeInBytes).CopyTo(new((void*)(Pointer + (DstRowStrideInBytes * row)), (int)RowSizeInBytes));
            }
        }

        buffer.Unmap();
    }
}
