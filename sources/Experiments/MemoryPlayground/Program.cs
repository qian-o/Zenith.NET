using Zenith.NET;

string[] values = ["Hello, World!", "你好，世界！", "こんにちは、世界！"];

using MemoryOwner owner = new();

nint pointer = MemoryMarshal.StringArrayToPointer(owner, values, StringEncoding.UTF8);

foreach (string value in MemoryMarshal.StringArrayFromPointer(pointer, (uint)values.Length, StringEncoding.UTF8))
{
    Console.WriteLine(value);
}