namespace Zenith.NET;

[Flags]
public enum PipelineStages
{
    None = 0,

    Indirect = 1 << 0,

    VertexInput = 1 << 1,

    VertexShader = 1 << 2,

    TaskShader = 1 << 3,

    MeshShader = 1 << 4,

    EarlyFragmentTests = 1 << 5,

    FragmentShader = 1 << 6,

    LateFragmentTests = 1 << 7,

    ColorAttachmentOutput = 1 << 8,

    ComputeShader = 1 << 9,

    AccelerationStructureBuild = 1 << 10,

    Copy = 1 << 11,

    Resolve = 1 << 12,

    AllGraphics = 1 << 13,

    AllCommands = 1 << 14
}
