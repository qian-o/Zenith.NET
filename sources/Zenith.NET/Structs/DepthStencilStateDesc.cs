namespace Zenith.NET;

/// <summary>
/// Describes the depth-stencil state, controlling depth and stencil testing and operations for both front and back faces.
/// </summary>
public record struct DepthStencilStateDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DepthStencilStateDesc"/> struct with default values.
    /// </summary>
    public DepthStencilStateDesc()
    {
        DepthEnable = true;
        DepthWriteEnable = true;
        DepthFunc = ComparisonFunc.LessEqual;
        StencilEnable = false;
        StencilReadMask = byte.MaxValue;
        StencilWriteMask = byte.MaxValue;
        FrontFace = new();
        BackFace = new();
    }

    /// <summary>
    /// Enables or disables depth testing.
    /// </summary>
    public bool DepthEnable { get; set; }

    /// <summary>
    /// Enables or disables writing to the depth buffer.
    /// </summary>
    public bool DepthWriteEnable { get; set; }

    /// <summary>
    /// The comparison function used for depth testing.
    /// </summary>
    public ComparisonFunc DepthFunc { get; set; }

    /// <summary>
    /// Enables or disables stencil testing.
    /// </summary>
    public bool StencilEnable { get; set; }

    /// <summary>
    /// The bit mask applied to stencil values for reading.
    /// </summary>
    public byte StencilReadMask { get; set; }

    /// <summary>
    /// The bit mask applied to stencil values for writing.
    /// </summary>
    public byte StencilWriteMask { get; set; }

    /// <summary>
    /// Describes stencil operations and comparison for front-facing polygons.
    /// </summary>
    public DepthStencilOpStateDesc FrontFace { get; set; }

    /// <summary>
    /// Describes stencil operations and comparison for back-facing polygons.
    /// </summary>
    public DepthStencilOpStateDesc BackFace { get; set; }

    /// <summary>
    /// Validates the current <see cref="DepthStencilStateDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!Enum.IsDefined(DepthFunc))
        {
            return false;
        }

        if (!FrontFace.Validate())
        {
            return false;
        }

        if (!BackFace.Validate())
        {
            return false;
        }

        return true;
    }
}
