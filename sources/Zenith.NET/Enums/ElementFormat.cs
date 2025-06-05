namespace Zenith.NET;

/// <summary>
/// Specifies the data format of a vertex element.
/// </summary>
public enum ElementFormat
{
    /// <summary>
    /// Unsigned 8-bit integer, 1 component.
    /// </summary>
    UByte1,

    /// <summary>
    /// Unsigned 8-bit integer, 2 components.
    /// </summary>
    UByte2,

    /// <summary>
    /// Unsigned 8-bit integer, 4 components.
    /// </summary>
    UByte4,

    /// <summary>
    /// Signed 8-bit integer, 1 component.
    /// </summary>
    Byte1,

    /// <summary>
    /// Signed 8-bit integer, 2 components.
    /// </summary>
    Byte2,

    /// <summary>
    /// Signed 8-bit integer, 4 components.
    /// </summary>
    Byte4,

    /// <summary>
    /// Unsigned normalized 8-bit integer, 1 component.
    /// </summary>
    UByte1Normalized,

    /// <summary>
    /// Unsigned normalized 8-bit integer, 2 components.
    /// </summary>
    UByte2Normalized,

    /// <summary>
    /// Unsigned normalized 8-bit integer, 4 components.
    /// </summary>
    UByte4Normalized,

    /// <summary>
    /// Signed normalized 8-bit integer, 1 component.
    /// </summary>
    Byte1Normalized,

    /// <summary>
    /// Signed normalized 8-bit integer, 2 components.
    /// </summary>
    Byte2Normalized,

    /// <summary>
    /// Signed normalized 8-bit integer, 4 components.
    /// </summary>
    Byte4Normalized,

    /// <summary>
    /// Unsigned 16-bit integer, 1 component.
    /// </summary>
    UShort1,

    /// <summary>
    /// Unsigned 16-bit integer, 2 components.
    /// </summary>
    UShort2,

    /// <summary>
    /// Unsigned 16-bit integer, 4 components.
    /// </summary>
    UShort4,

    /// <summary>
    /// Signed 16-bit integer, 1 component.
    /// </summary>
    Short1,

    /// <summary>
    /// Signed 16-bit integer, 2 components.
    /// </summary>
    Short2,

    /// <summary>
    /// Signed 16-bit integer, 4 components.
    /// </summary>
    Short4,

    /// <summary>
    /// Unsigned normalized 16-bit integer, 1 component.
    /// </summary>
    UShort1Normalized,

    /// <summary>
    /// Unsigned normalized 16-bit integer, 2 components.
    /// </summary>
    UShort2Normalized,

    /// <summary>
    /// Unsigned normalized 16-bit integer, 4 components.
    /// </summary>
    UShort4Normalized,

    /// <summary>
    /// Signed normalized 16-bit integer, 1 component.
    /// </summary>
    Short1Normalized,

    /// <summary>
    /// Signed normalized 16-bit integer, 2 components.
    /// </summary>
    Short2Normalized,

    /// <summary>
    /// Signed normalized 16-bit integer, 4 components.
    /// </summary>
    Short4Normalized,

    /// <summary>
    /// Half-precision (16-bit) floating point, 1 component.
    /// </summary>
    Half1,

    /// <summary>
    /// Half-precision (16-bit) floating point, 2 components.
    /// </summary>
    Half2,

    /// <summary>
    /// Half-precision (16-bit) floating point, 4 components.
    /// </summary>
    Half4,

    /// <summary>
    /// 32-bit floating point, 1 component.
    /// </summary>
    Float1,

    /// <summary>
    /// 32-bit floating point, 2 components.
    /// </summary>
    Float2,

    /// <summary>
    /// 32-bit floating point, 3 components.
    /// </summary>
    Float3,

    /// <summary>
    /// 32-bit floating point, 4 components.
    /// </summary>
    Float4,

    /// <summary>
    /// Unsigned 32-bit integer, 1 component.
    /// </summary>
    UInt1,

    /// <summary>
    /// Unsigned 32-bit integer, 2 components.
    /// </summary>
    UInt2,

    /// <summary>
    /// Unsigned 32-bit integer, 3 components.
    /// </summary>
    UInt3,

    /// <summary>
    /// Unsigned 32-bit integer, 4 components.
    /// </summary>
    UInt4,

    /// <summary>
    /// Signed 32-bit integer, 1 component.
    /// </summary>
    Int1,

    /// <summary>
    /// Signed 32-bit integer, 2 components.
    /// </summary>
    Int2,

    /// <summary>
    /// Signed 32-bit integer, 3 components.
    /// </summary>
    Int3,

    /// <summary>
    /// Signed 32-bit integer, 4 components.
    /// </summary>
    Int4
}