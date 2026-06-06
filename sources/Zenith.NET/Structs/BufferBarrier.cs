namespace Zenith.NET;

public struct BufferBarrier
{
    public Buffer Buffer;

    public PipelineStages SrcStages;

    public PipelineStages DstStages;

    public ResourceAccess SrcAccess;

    public ResourceAccess DstAccess;

    public static BufferBarrier Vertex(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexInput,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.Vertex
        };
    }

    public static BufferBarrier Index(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexInput,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.Index
        };
    }

    public static BufferBarrier Constant(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexShader | PipelineStages.FragmentShader | PipelineStages.ComputeShader,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.Constant
        };
    }

    public static BufferBarrier Indirect(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.Indirect,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.Indirect
        };
    }

    public static BufferBarrier ShaderRead(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexShader | PipelineStages.FragmentShader | PipelineStages.ComputeShader,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.ShaderRead
        };
    }

    public static BufferBarrier Storage(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexShader | PipelineStages.FragmentShader | PipelineStages.ComputeShader,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.ShaderRead | ResourceAccess.ShaderWrite
        };
    }

    public static BufferBarrier CopySrc(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.Copy,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.CopyRead
        };
    }

    public static BufferBarrier CopyDst(Buffer buffer, BufferBarrier? previous)
    {
        return new()
        {
            Buffer = buffer,
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.Copy,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.CopyWrite
        };
    }
}
