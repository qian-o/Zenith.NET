using System.Numerics;

namespace Sponza.Models;

internal struct DrawData
{
    public uint IndexCount;

    public uint FirstIndex;

    public int VertexOffset;

    public uint MaterialIndex;

    public Matrix4x4 World;

    public Matrix4x4 NormalWorld;
}
