namespace Zenith.NET;

[Flags]
public enum ASBuildOptions
{
    None = 0,

    /// <summary>
    /// Build the acceleration structure such that it supports future updates instead
    /// of the app having to entirely rebuild the structure.
    /// </summary>
    AllowUpdate = 1 << 0,

    /// <summary>
    /// Enables the option to compact the acceleration structure.
    /// </summary>
    AllowCompactation = 1 << 1,

    /// <summary>
    /// Construct a high quality acceleration structure that maximizes raytracing performance
    /// at the expense of additional build time.
    /// </summary>
    PreferFastTrace = 1 << 2,

    /// <summary>
    /// Construct a lower quality acceleration structure, trading raytracing performance
    /// for build speed.
    /// </summary>
    PreferFastBuild = 1 << 3,

    /// <summary>
    /// Minimize the amount of scratch memory used during the acceleration structure
    /// build as well as the size of the result.
    /// </summary>
    MinimizeMemory = 1 << 4,

    /// <summary>
    /// Perform an acceleration structure update, as opposed to building from scratch.
    /// </summary>
    PerformUpdate = 1 << 5
}
