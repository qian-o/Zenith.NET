namespace Zenith.NET;

public struct BufferViewDesc
{
    public Buffer Buffer;

    public uint OffsetInBytes;

    public uint SizeInBytes;

    public uint StrideInBytes;

    public static BufferViewDesc Uniform(Buffer buffer, uint offsetInBytes, uint sizeInBytes)
    {
        return new()
        {
            Buffer = buffer,
            OffsetInBytes = offsetInBytes,
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0
        };
    }

    public static BufferViewDesc StorageReadOnly(Buffer buffer, uint offsetInBytes, uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            Buffer = buffer,
            OffsetInBytes = offsetInBytes,
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes
        };
    }

    public static BufferViewDesc StorageReadWrite(Buffer buffer, uint offsetInBytes, uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            Buffer = buffer,
            OffsetInBytes = offsetInBytes,
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes
        };
    }
}
