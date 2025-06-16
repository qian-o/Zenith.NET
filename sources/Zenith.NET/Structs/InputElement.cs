namespace Zenith.NET;

public struct InputElement
{
    public ElementFormat Format { get; set; }

    public ElementSemanticType Type { get; set; }

    public uint Index { get; set; }

    public uint OffsetInBytes { get; set; }
}
