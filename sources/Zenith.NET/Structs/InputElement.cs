namespace Zenith.NET;

public record struct InputElement
{
    public ElementFormat Format;

    public ElementSemantic Semantic;

    public uint Index;

    public uint OffsetInBytes;
}
