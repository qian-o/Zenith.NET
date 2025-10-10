using System.Runtime.InteropServices;
using System.Text;

namespace Zenith.NET;

public static class MemoryMarshal
{
    public static nint Alloc<T>(MemoryOwner owner, uint length) where T : unmanaged
    {
        T[] values = new T[length];
        Array.Fill(values, default);

        return owner.Native(values);
    }

    public static nint StringToNative(MemoryOwner owner, string value, StringEncoding encoding)
    {
        byte[] bytes = encoding switch
        {
            StringEncoding.Ansi => Encoding.Default.GetBytes(value + '\0'),
            StringEncoding.Uni => Encoding.Unicode.GetBytes(value + '\0'),
            StringEncoding.UTF8 => Encoding.UTF8.GetBytes(value + '\0'),
            _ => []
        };

        return owner.Native(bytes);
    }

    public static nint StringArrayToNative(MemoryOwner owner, string[] values, StringEncoding encoding)
    {
        nint[] ptrs = new nint[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            ptrs[i] = StringToNative(owner, values[i], encoding);
        }

        return owner.Native(ptrs);
    }

    public static string NativeToString(nint ptr, StringEncoding encoding)
    {
        return encoding switch
        {
            StringEncoding.Ansi => Marshal.PtrToStringAnsi(ptr) ?? string.Empty,
            StringEncoding.Uni => Marshal.PtrToStringUni(ptr) ?? string.Empty,
            StringEncoding.UTF8 => Marshal.PtrToStringUTF8(ptr) ?? string.Empty,
            _ => string.Empty
        };
    }

    public static string[] NativeToStringArray(nint ptr, uint length, StringEncoding encoding)
    {
        nint[] ptrs = new nint[length];
        Marshal.Copy(ptr, ptrs, 0, (int)length);

        string[] values = new string[length];

        for (uint i = 0; i < length; i++)
        {
            values[i] = NativeToString(ptrs[i], encoding);
        }

        return values;
    }
}