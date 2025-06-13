namespace Zenith.NET;

public record struct InputElementDesc : IDesc
{
    public const int AppendAligned = -1;

    public ElementFormat Format { get; set; }

    public ElementSemanticType Type { get; set; }

    public uint Index { get; set; }

    public int OffsetInBytes { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(Format))
        {
            return false;
        }

        if (!Enum.IsDefined(Type))
        {
            return false;
        }

        return true;
    }
}
