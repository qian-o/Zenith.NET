namespace Zenith.NET;

public struct BufferDesc
{
    public uint SizeInBytes;

    public uint StrideInBytes;

    public BufferAccess Access;

    public BufferUsages Usages;
}
