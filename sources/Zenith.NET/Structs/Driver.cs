namespace Zenith.NET;

public readonly struct Driver(string name, bool isRayTracingSupported)
{
    public string Name { get; } = name;

    public bool IsRayTracingSupported { get; } = isRayTracingSupported;
}
