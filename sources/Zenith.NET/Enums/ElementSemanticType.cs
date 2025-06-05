namespace Zenith.NET;

/// <summary>
/// Specifies the semantic meaning of a vertex element.
/// </summary>
public enum ElementSemanticType
{
    /// <summary>
    /// Describes a position.
    /// </summary>
    Position,

    /// <summary>
    /// Describes a texture coordinate.
    /// </summary>
    TexCoord,

    /// <summary>
    /// Describes a normal vector.
    /// </summary>
    Normal,

    /// <summary>
    /// Describes a tangent vector.
    /// </summary>
    Tangent,

    /// <summary>
    /// Describes a binormal vector.
    /// </summary>
    Binormal,

    /// <summary>
    /// Describes a color.
    /// </summary>
    Color,

    /// <summary>
    /// Blend indices.
    /// </summary>
    BlendIndices,

    /// <summary>
    /// Blend weights.
    /// </summary>
    BlendWeight,

    /// <summary>
    /// Auxiliary value to count all semantics.
    /// </summary>
    Count
}
