namespace Zenith.NET;

/// <summary>
/// Describes a rectangular region of a render target, including its position, size, and depth range.
/// </summary>
public record struct Viewport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Viewport"/> struct with default values.
    /// </summary>
    public Viewport()
    {
        X = 0;
        Y = 0;
        Width = 0;
        Height = 0;
        MinDepth = 0;
        MaxDepth = 1;
    }

    /// <summary>
    /// The X coordinate of the upper-left corner of the viewport, in pixels.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// The Y coordinate of the upper-left corner of the viewport, in pixels.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// The width of the viewport, in pixels.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// The height of the viewport, in pixels.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// The minimum depth of the viewport. Typically ranges from 0.0 (near plane) to 1.0 (far plane).
    /// </summary>
    public float MinDepth { get; set; }

    /// <summary>
    /// The maximum depth of the viewport. Typically ranges from 0.0 (near plane) to 1.0 (far plane).
    /// </summary>
    public float MaxDepth { get; set; }
}
