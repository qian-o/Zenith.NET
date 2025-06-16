namespace Zenith.NET;

public record struct InputElementDesc
{
    public ElementFormat Format { get; set; }

    public ElementSemanticType Type { get; set; }

    public uint Index { get; set; }

    public uint OffsetInBytes { get; set; }
}
