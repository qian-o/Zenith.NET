namespace Zenith.NET;

/// <summary>
/// Describes the rasterizer state, controlling how primitives are converted to pixels.
/// Includes culling, fill mode, front face winding, depth bias, and related options.
/// </summary>
public record struct RasterizerStateDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RasterizerStateDesc"/> struct with default values.
    /// </summary>
    public RasterizerStateDesc()
    {
        CullMode = CullMode.Back;
        FillMode = FillMode.Solid;
        FrontFace = FrontFace.CounterClockwise;
        DepthBias = 0;
        DepthBiasClamp = 0.0f;
        SlopeScaledDepthBias = 0.0f;
        DepthClipEnable = true;
        ScissorEnable = false;
    }

    /// <summary>
    /// Specifies which faces of a primitive are culled during rasterization.
    /// </summary>
    public CullMode CullMode { get; set; }

    /// <summary>
    /// Specifies how polygons are rasterized (solid or wireframe).
    /// </summary>
    public FillMode FillMode { get; set; }

    /// <summary>
    /// Specifies the winding order that determines the front face of a primitive.
    /// </summary>
    public FrontFace FrontFace { get; set; }

    /// <summary>
    /// Constant depth value added to each pixel. Used for depth biasing.
    /// </summary>
    public int DepthBias { get; set; }

    /// <summary>
    /// Maximum (absolute) depth bias of a pixel.
    /// </summary>
    public float DepthBiasClamp { get; set; }

    /// <summary>
    /// Scalar applied to a pixel's slope for depth biasing.
    /// </summary>
    public float SlopeScaledDepthBias { get; set; }

    /// <summary>
    /// Enables or disables depth clipping.
    /// </summary>
    public bool DepthClipEnable { get; set; }

    /// <summary>
    /// Enables or disables the scissor test.
    /// </summary>
    public bool ScissorEnable { get; set; }

    /// <summary>
    /// Validates the current <see cref="RasterizerStateDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!Enum.IsDefined(CullMode))
        {
            return false;
        }

        if (!Enum.IsDefined(FillMode))
        {
            return false;
        }

        if (!Enum.IsDefined(FrontFace))
        {
            return false;
        }

        return true;
    }
}
