using Zenith.NET;

string[] values = ["Hello, World!", "你好，世界！", "こんにちは、世界！"];

using ZenithMarshal.Owner owner = new();

nint pointer = ZenithMarshal.StringArrayToPointer(owner, values, StringEncoding.UTF8);

foreach (string value in ZenithMarshal.StringArrayFromPointer(pointer, (uint)values.Length, StringEncoding.UTF8))
{
    Console.WriteLine(value);
}