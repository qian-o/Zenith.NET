namespace Zenith.NET;

public struct MemoryBarrier
{
    public PipelineStages SrcStages;

    public PipelineStages DstStages;

    public ResourceAccess SrcAccess;

    public ResourceAccess DstAccess;
}
