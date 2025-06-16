using System.Numerics;

namespace Zenith.NET;

public record struct RayTracingTrianglesDesc : IRayTracingGeometry
{
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
}
