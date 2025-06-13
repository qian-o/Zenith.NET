namespace Zenith.NET;

public record struct RayTracingAABBsDesc : IRayTracingGeometryDesc
{
    public Buffer Buffer { get; set; }

    public uint Count { get; set; }

    public uint StrideInBytes { get; set; }

    public uint OffsetInBytes { get; set; }

    public RayTracingGeometryFlags Flags { get; set; }

    public readonly bool Validate()
    {
        if (Buffer is null)
        {
            return false;
        }

        if (Count is 0)
        {
            return false;
        }

        if (StrideInBytes is 0)
        {
            return false;
        }

        return true;
    }
}
