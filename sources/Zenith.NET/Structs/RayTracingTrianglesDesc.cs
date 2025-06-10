using System.Numerics;

namespace Zenith.NET;

public record struct RayTracingTrianglesDesc : IRayTracingGeometryDesc
{
    public RayTracingTrianglesDesc()
    {
        VertexBuffer = null!;
        VertexFormat = PixelFormat.R32G32B32A32Float;
        VertexCount = 0;
        VertexStrideInBytes = 1;
        VertexOffsetInBytes = 0;
        IndexBuffer = null;
        IndexFormat = IndexFormat.UInt32;
        IndexCount = 0;
        IndexOffsetInBytes = 0;
        Transform = Matrix4x4.Identity;
        Flags = RayTracingGeometryFlags.None;
    }

    public Buffer VertexBuffer { get; set; }

    public PixelFormat VertexFormat { get; set; }

    public uint VertexCount { get; set; }

    public uint VertexStrideInBytes { get; set; }

    public uint VertexOffsetInBytes { get; set; }

    public Buffer? IndexBuffer { get; set; }

    public IndexFormat IndexFormat { get; set; }

    public uint IndexCount { get; set; }

    public uint IndexOffsetInBytes { get; set; }

    public Matrix4x4 Transform { get; set; }

    public RayTracingGeometryFlags Flags { get; set; }

    public readonly bool Validate()
    {
        if (VertexBuffer is null)
        {
            return false;
        }

        if (!Enum.IsDefined(VertexFormat))
        {
            return false;
        }

        if (VertexCount is 0)
        {
            return false;
        }

        if (VertexStrideInBytes is 0)
        {
            return false;
        }

        if (IndexBuffer is not null)
        {
            if (!Enum.IsDefined(IndexFormat))
            {
                return false;
            }

            if (IndexCount is 0)
            {
                return false;
            }
        }

        return true;
    }
}
