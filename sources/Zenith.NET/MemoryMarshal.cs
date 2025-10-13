using System.Runtime.InteropServices;
using System.Text;

namespace Zenith.NET;

public static unsafe class MemoryMarshal
{
    public static nint Alloc<T>(MemoryOwner owner, uint length) where T : unmanaged
    {
        return owner.Native(new T[length]);
    }

    public static nint StringToNative(MemoryOwner owner, string value, StringEncoding encoding)
    {
        byte[] values = encoding switch
        {
            StringEncoding.Ansi => Encoding.Default.GetBytes(value + '\0'),
            StringEncoding.Uni => Encoding.Unicode.GetBytes(value + '\0'),
            StringEncoding.UTF8 => Encoding.UTF8.GetBytes(value + '\0'),
            _ => []
        };

        return owner.Native(values);
    }

    public static nint StringArrayToNative(MemoryOwner owner, string[] values, StringEncoding encoding)
    {
        nint[] pointers = new nint[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            pointers[i] = StringToNative(owner, values[i], encoding);
        }

        return owner.Native(pointers);
    }

    public static string StringFromNative(nint pointer, StringEncoding encoding)
    {
        return encoding switch
        {
            StringEncoding.Ansi => Marshal.PtrToStringAnsi(pointer) ?? string.Empty,
            StringEncoding.Uni => Marshal.PtrToStringUni(pointer) ?? string.Empty,
            StringEncoding.UTF8 => Marshal.PtrToStringUTF8(pointer) ?? string.Empty,
            _ => string.Empty
        };
    }

    public static string[] StringArrayFromNative(nint pointer, uint length, StringEncoding encoding)
    {
        nint* pointers = (nint*)pointer;

        string[] values = new string[length];

        for (uint i = 0; i < length; i++)
        {
            values[i] = StringFromNative(pointers[i], encoding);
        }

        return values;
    }

    public static void Copy<T>(ReadOnlySpan<T> source, nint destination) where T : unmanaged
    {
        source.CopyTo(new Span<T>((void*)destination, source.Length));
    }

    public static void Copy<T>(nint source, Span<T> destination) where T : unmanaged
    {
        new ReadOnlySpan<T>((void*)source, destination.Length).CopyTo(destination);
    }

    public static void Copy<T>(nint source, nint destination, uint length) where T : unmanaged
    {
        new ReadOnlySpan<T>((void*)source, (int)length).CopyTo(new Span<T>((void*)destination, (int)length));
    }
}