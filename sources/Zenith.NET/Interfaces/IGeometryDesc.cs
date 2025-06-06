namespace Zenith.NET;

/// <summary>
/// Represents a description interface for geometry descriptors used in acceleration structures.
/// Inherits from <see cref="IDesc"/> and adds geometry-specific flags.
/// </summary>
public interface IGeometryDesc : IDesc
{
    /// <summary>
    /// Gets or sets the geometry flags that specify options for the geometry in acceleration structures.
    /// </summary>
    GeometryFlags Flags { get; set; }
}
