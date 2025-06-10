namespace Zenith.NET;

public record struct IndirectDispatchArgs
{
    public IndirectDispatchArgs()
    {
        GroupCountX = 1;
        GroupCountY = 1;
        GroupCountZ = 1;
    }

    public uint GroupCountX { get; set; }

    public uint GroupCountY { get; set; }

    public uint GroupCountZ { get; set; }
}
