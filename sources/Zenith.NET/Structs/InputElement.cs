namespace Zenith.NET;

public record struct InputElement
{
    public ElementFormat Format;

    public ElementSemantic Semantic;

    public uint SemanticIndex;

    public uint OffsetInBytes;
}
