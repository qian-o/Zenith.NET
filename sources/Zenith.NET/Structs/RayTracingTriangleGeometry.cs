using System.Numerics;

namespace Zenith.NET;

public struct RayTracingTriangleGeometry
{
    public Buffer VertexBuffer;

    public uint VertexCount;

    public uint VertexStrideInBytes;

    public uint VertexOffsetInBytes;

    public Buffer? IndexBuffer;

    public IndexFormat IndexFormat;

    public uint IndexCount;

    public uint IndexOffsetInBytes;

    public Matrix4x4 Transform;
}
