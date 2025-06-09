using System.Numerics;

namespace Zenith.NET;

/// <summary>
/// Describes an instance of a bottom-level acceleration structure (BLAS) for use in top-level acceleration structures,
/// including instance-specific transformation, masking, and contribution to hit group indexing.
/// </summary>
public record struct RayTracingInstanceDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RayTracingInstanceDesc"/> struct with default values.
    /// </summary>
    public RayTracingInstanceDesc()
    {
        BLAS = null!;
        InstanceID = 0;
        InstanceMask = 0xFF;
        InstanceContributionToHitGroupIndex = 0;
        Transform = Matrix4x4.Identity;
        Flags = RayTracingInstanceFlags.None;
    }

    /// <summary>
    /// The bottom-level acceleration structure (BLAS) to be instanced.
    /// </summary>
    public BLAS BLAS { get; set; }

    /// <summary>
    /// The 24-bit instance identifier, available to shaders via built-in semantics.
    /// </summary>
    public uint InstanceID { get; set; }

    /// <summary>
    /// The 8-bit visibility mask for the instance. Only rays with a matching mask will intersect this instance.
    /// </summary>
    public byte InstanceMask { get; set; }

    /// <summary>
    /// The offset added to the hit group index when a hit is reported for this instance.
    /// </summary>
    public uint InstanceContributionToHitGroupIndex { get; set; }

    /// <summary>
    /// The 4x3 transformation matrix applied to the instance. Only the upper 3x4 portion (rotation, scale, translation) is used; the last row is ignored.
    /// </summary>
    public Matrix4x4 Transform { get; set; }

    /// <summary>
    /// Gets or sets the instance flags that specify options for the instance in top-level acceleration structures.
    /// </summary>
    public RayTracingInstanceFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="RayTracingInstanceDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (BLAS is null)
        {
            return false;
        }

        if (!Enum.IsDefined(Flags))
        {
            return false;
        }

        return true;
    }
}
