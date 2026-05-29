namespace Zenith.NET;

public struct MemoryBarrier
{
    public PipelineStages SrcStages;

    public PipelineStages DstStages;

    public ResourceAccess SrcAccess;

    public ResourceAccess DstAccess;

    public static MemoryBarrier Storage(MemoryBarrier? previous)
    {
        return new()
        {
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexShader | PipelineStages.FragmentShader | PipelineStages.ComputeShader,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.ShaderWrite,
            DstAccess = ResourceAccess.ShaderRead | ResourceAccess.ShaderWrite
        };
    }
}
