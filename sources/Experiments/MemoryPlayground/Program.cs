using Zenith.NET;

string[] values = ["Hello, World!", "你好，世界！", "こんにちは、世界！"];

using MemoryOwner owner = new();

nint pointer = MemoryMarshal.StringArrayToNative(owner, values, StringEncoding.UTF8);

foreach (string value in MemoryMarshal.StringArrayFromNative(pointer, (uint)values.Length, StringEncoding.UTF8))
{
    Console.WriteLine(value);
}