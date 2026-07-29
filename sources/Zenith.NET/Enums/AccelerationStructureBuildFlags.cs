namespace Zenith.NET;

[Flags]
public enum AccelerationStructureBuildFlags
{
    None = 0,

    AllowUpdate = 1 << 0,

    AllowCompaction = 1 << 1,

    PreferFastTrace = 1 << 2,

    PreferFastBuild = 1 << 3,

    MinimizeMemory = 1 << 4
}
