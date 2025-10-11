using Zenith.NET;

using MemoryOwner owner = new();

nint ptr = MemoryMarshal.StringToNative(owner, "Hello, World!", StringEncoding.UTF8);

string str = MemoryMarshal.StringFromNative(ptr, StringEncoding.UTF8);

Console.WriteLine(str);