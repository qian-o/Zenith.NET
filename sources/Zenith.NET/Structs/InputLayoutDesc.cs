using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct InputLayoutDesc : IDesc
{
    public InputLayoutDesc()
    {
        Elements = [];
        StrideInBytes = 1;
    }

    public InputElementDesc[] Elements { get; set; }

    public uint StrideInBytes { get; set; }

    public readonly bool Validate()
    {
        if (!Validation.IsValidDescs(Elements))
        {
            return false;
        }

        if (StrideInBytes is 0)
        {
            return false;
        }

        return true;
    }

    public InputLayoutDesc Add(InputElementDesc element)
    {
        if (element.OffsetInBytes is InputElementDesc.AppendAligned)
        {
            element.OffsetInBytes = (int)StrideInBytes;
        }

        Elements = [.. Elements, element];

        StrideInBytes += GetFormatSizeInBytes(element.Format);

        return this;
    }

    private static uint GetFormatSizeInBytes(ElementFormat format)
    {
        return format switch
        {
            ElementFormat.UByte1 or
            ElementFormat.Byte1 or
            ElementFormat.UByte1Normalized or
            ElementFormat.Byte1Normalized => 1,

            ElementFormat.UByte2 or
            ElementFormat.Byte2 or
            ElementFormat.UByte2Normalized or
            ElementFormat.Byte2Normalized or
            ElementFormat.UShort1 or
            ElementFormat.Short1 or
            ElementFormat.UShort1Normalized or
            ElementFormat.Short1Normalized or
            ElementFormat.Half1 => 2,

            ElementFormat.UByte4 or
            ElementFormat.Byte4 or
            ElementFormat.UByte4Normalized or
            ElementFormat.Byte4Normalized or
            ElementFormat.UShort2 or
            ElementFormat.Short2 or
            ElementFormat.UShort2Normalized or
            ElementFormat.Short2Normalized or
            ElementFormat.Half2 or
            ElementFormat.Float1 or
            ElementFormat.UInt1 or
            ElementFormat.Int1 => 4,

            ElementFormat.UShort4 or
            ElementFormat.Short4 or
            ElementFormat.UShort4Normalized or
            ElementFormat.Short4Normalized or
            ElementFormat.Half4 or
            ElementFormat.Float2 or
            ElementFormat.UInt2 or
            ElementFormat.Int2 => 8,

            ElementFormat.Float3 or
            ElementFormat.UInt3 or
            ElementFormat.Int3 => 12,

            ElementFormat.Float4 or
            ElementFormat.UInt4 or
            ElementFormat.Int4 => 16,

            _ => throw new ArgumentOutOfRangeException(nameof(format), $"Unsupported format: {format}")
        };
    }
}
