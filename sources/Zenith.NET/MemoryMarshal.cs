namespace Zenith.NET;

public static class MemoryMarshal
{
    public static nint Alloc<T>(uint length, MemoryOwner owner) where T : unmanaged
    {
        T[] values = new T[length];
        Array.Fill(values, default);

        return owner.Alloc(values);
    }
}