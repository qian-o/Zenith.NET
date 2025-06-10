namespace Zenith.NET;

public record struct InputElementDesc : IDesc
{
    public const int AppendAligned = -1;

    public InputElementDesc()
    {
        Format = ElementFormat.UByte1;
        Type = ElementSemanticType.Position;
        Index = 0;
        OffsetInBytes = AppendAligned;
    }

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
