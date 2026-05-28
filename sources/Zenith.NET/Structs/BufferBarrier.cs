namespace Zenith.NET;

public struct BufferBarrier
{
    public Buffer Buffer;

    public ulong OffsetInBytes;

    public ulong SizeInBytes;

    public PipelineStages SrcStages;

    public PipelineStages DstStages;

    public ResourceAccess SrcAccess;

    public ResourceAccess DstAccess;
}
