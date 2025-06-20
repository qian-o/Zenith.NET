namespace Zenith.NET;

public record struct InputElement
{
    public ElementFormat Format;

    public ElementSemanticType Type;

    public uint Index;

    public uint OffsetInBytes;
}
