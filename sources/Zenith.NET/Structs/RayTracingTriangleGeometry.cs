using System.Numerics;

namespace Zenith.NET;

public struct RayTracingTriangleGeometry
{
    public Buffer VertexBuffer;

    public Buffer? IndexBuffer;

    public Matrix4x4 Transform;

    public PixelFormat VertexFormat;

    public uint VertexCount;

    public uint VertexStrideInBytes;

    public uint VertexOffsetInBytes;

    public IndexFormat IndexFormat;

    public uint IndexCount;

    public uint IndexOffsetInBytes;
}