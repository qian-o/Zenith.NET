using System.Runtime.InteropServices;
using System.Text;

namespace Zenith.NET;

public static unsafe class ZenithMarshal
{
    public class Scope : DisposableObject
    {
        private readonly List<nint> pointers = [];

        internal nint Native<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            nint pointer = (nint)NativeMemory.Alloc((uint)(sizeof(T) * data.Length));

            data.CopyTo(new((void*)pointer, data.Length));

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

    public static nint Allocate<T>(Scope scope, uint length) where T : unmanaged
    {
        return scope.Native(new T[length]);
    }

    public static nint StringToPointer(Scope scope, string value, StringEncoding encoding)
    {
        byte[] values = encoding switch
        {
            StringEncoding.Uni => Encoding.Unicode.GetBytes(value + '\0'),
            StringEncoding.UTF8 => Encoding.UTF8.GetBytes(value + '\0'),
            _ => []
        };

        return scope.Native(values);
    }

    public static nint StringArrayToPointer(Scope scope, string[] values, StringEncoding encoding)
    {
        nint[] pointers = new nint[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            pointers[i] = StringToPointer(scope, values[i], encoding);
        }

        return scope.Native(pointers);
    }

    public static string StringFromPointer(nint pointer, StringEncoding encoding)
    {
        return encoding switch
        {
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