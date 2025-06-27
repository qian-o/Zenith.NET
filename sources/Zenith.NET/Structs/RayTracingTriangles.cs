using System.Numerics;

namespace Zenith.NET;

public record struct RayTracingTriangles
{
    public IBuffer VertexBuffer;

    public PixelFormat VertexFormat;

    public uint VertexCount;

    public uint VertexStrideInBytes;

    public uint VertexOffsetInBytes;

    public IBuffer? IndexBuffer;

    public IndexFormat IndexFormat;

    public uint IndexCount;

    public uint IndexOffsetInBytes;

    public Matrix4x4 Transform;

    public RayTracingGeometryFlags Flags;
}
