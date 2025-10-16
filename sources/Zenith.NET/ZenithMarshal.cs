using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Zenith.NET;

public static unsafe class ZenithMarshal
{
    public class Owner : DisposableObject
    {
        private readonly List<nint> pointers = [];

        internal nint Native<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            nint pointer = (nint)NativeMemory.Alloc((uint)(Unsafe.SizeOf<T>() * data.Length));

            Copy(data, pointer);

            pointers.Add(pointer);

            return pointer;
        }

        protected override void Destroy()
        {
            foreach (nint pointer in pointers)
            {
                NativeMemory.Free((void*)pointer);
            }

            pointers.Clear();
        }
    }

    public static nint Allocate<T>(Owner owner, uint length) where T : unmanaged
    {
        return owner.Native(new T[length]);
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

    public static nint StringToPointer(Owner owner, string value, StringEncoding encoding)
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

    public static nint StringArrayToPointer(Owner owner, string[] values, StringEncoding encoding)
    {
        nint[] pointers = new nint[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            pointers[i] = StringToPointer(owner, values[i], encoding);
        }

        return owner.Native(pointers);
    }

    public static string StringFromPointer(nint pointer, StringEncoding encoding)
    {
        return encoding switch
        {
            StringEncoding.Ansi => Marshal.PtrToStringAnsi(pointer) ?? string.Empty,
            StringEncoding.Uni => Marshal.PtrToStringUni(pointer) ?? string.Empty,
            StringEncoding.UTF8 => Marshal.PtrToStringUTF8(pointer) ?? string.Empty,
            _ => string.Empty
        };
    }

    public static string[] StringArrayFromPointer(nint pointer, uint length, StringEncoding encoding)
    {
        nint* pointers = (nint*)pointer;

        string[] values = new string[length];

        for (uint i = 0; i < length; i++)
        {
            values[i] = StringFromPointer(pointers[i], encoding);
        }

        return values;
    }
}