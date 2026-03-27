using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Helpers;

internal static class CornellBoxGeometry
{
    public static void Create(out PackedVertex[] vertices, out uint[] indices, out Material[] materials)
    {
        List<PackedVertex> verticesList = [];
        List<uint> indicesList = [];

        // 0: Left wall (red)
        AddQuad(verticesList, indicesList,
                new(552.8f, 0.0f, 0.0f),
                new(549.6f, 0.0f, 559.2f),
                new(556.0f, 548.8f, 559.2f),
                new(556.0f, 548.8f, 0.0f),
                0);

        // 1: Right wall (green)
        AddQuad(verticesList, indicesList,
                new(0.0f, 0.0f, 559.2f),
                new(0.0f, 0.0f, 0.0f),
                new(0.0f, 548.8f, 0.0f),
                new(0.0f, 548.8f, 559.2f),
                1);

        // 2: Ceiling (white)
        AddQuad(verticesList, indicesList,
                new(556.0f, 548.8f, 0.0f),
                new(556.0f, 548.8f, 559.2f),
                new(0.0f, 548.8f, 559.2f),
                new(0.0f, 548.8f, 0.0f),
                2);

        // 3: Floor (white)
        AddQuad(verticesList, indicesList,
                new(552.8f, 0.0f, 0.0f),
                new(0.0f, 0.0f, 0.0f),
                new(0.0f, 0.0f, 559.2f),
                new(549.6f, 0.0f, 559.2f),
                2);

        // 4: Back wall (white)
        AddQuad(verticesList, indicesList,
                new(549.6f, 0.0f, 559.2f),
                new(0.0f, 0.0f, 559.2f),
                new(0.0f, 548.8f, 559.2f),
                new(556.0f, 548.8f, 559.2f),
                2);

        // 5-9: Short block
        AddQuad(verticesList, indicesList, new(130.0f, 165.0f, 65.0f), new(82.0f, 165.0f, 225.0f), new(240.0f, 165.0f, 272.0f), new(290.0f, 165.0f, 114.0f), 2);
        AddQuad(verticesList, indicesList, new(290.0f, 0.0f, 114.0f), new(290.0f, 165.0f, 114.0f), new(240.0f, 165.0f, 272.0f), new(240.0f, 0.0f, 272.0f), 2);
        AddQuad(verticesList, indicesList, new(130.0f, 0.0f, 65.0f), new(130.0f, 165.0f, 65.0f), new(290.0f, 165.0f, 114.0f), new(290.0f, 0.0f, 114.0f), 2);
        AddQuad(verticesList, indicesList, new(82.0f, 0.0f, 225.0f), new(82.0f, 165.0f, 225.0f), new(130.0f, 165.0f, 65.0f), new(130.0f, 0.0f, 65.0f), 2);
        AddQuad(verticesList, indicesList, new(240.0f, 0.0f, 272.0f), new(240.0f, 165.0f, 272.0f), new(82.0f, 165.0f, 225.0f), new(82.0f, 0.0f, 225.0f), 2);

        // 10-14: Tall block
        AddQuad(verticesList, indicesList, new(423.0f, 330.0f, 247.0f), new(265.0f, 330.0f, 296.0f), new(314.0f, 330.0f, 456.0f), new(472.0f, 330.0f, 406.0f), 2);
        AddQuad(verticesList, indicesList, new(423.0f, 0.0f, 247.0f), new(423.0f, 330.0f, 247.0f), new(472.0f, 330.0f, 406.0f), new(472.0f, 0.0f, 406.0f), 2);
        AddQuad(verticesList, indicesList, new(472.0f, 0.0f, 406.0f), new(472.0f, 330.0f, 406.0f), new(314.0f, 330.0f, 456.0f), new(314.0f, 0.0f, 456.0f), 2);
        AddQuad(verticesList, indicesList, new(314.0f, 0.0f, 456.0f), new(314.0f, 330.0f, 456.0f), new(265.0f, 330.0f, 296.0f), new(265.0f, 0.0f, 296.0f), 2);
        AddQuad(verticesList, indicesList, new(265.0f, 0.0f, 296.0f), new(265.0f, 330.0f, 296.0f), new(423.0f, 330.0f, 247.0f), new(423.0f, 0.0f, 247.0f), 2);

        // 15: Light
        AddQuad(verticesList, indicesList,
                new(343.0f, 548.6f, 227.0f),
                new(343.0f, 548.6f, 332.0f),
                new(213.0f, 548.6f, 332.0f),
                new(213.0f, 548.6f, 227.0f),
                3);

        vertices = [.. verticesList];
        indices = [.. indicesList];
        materials =
        [
            new() { AlbedoAndEmission = new(0.63f, 0.06f, 0.06f, 0.0f) },
            new() { AlbedoAndEmission = new(0.14f, 0.45f, 0.09f, 0.0f) },
            new() { AlbedoAndEmission = new(0.73f, 0.71f, 0.68f, 0.0f) },
            new() { AlbedoAndEmission = new(1.0f, 0.85f, 0.6f, 15.0f) },
        ];
    }

    private static void AddQuad(List<PackedVertex> vertices, List<uint> indices,
                                Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                                uint materialID)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
        float matBits = BitConverter.UInt32BitsToSingle(materialID);

        uint startIndex = (uint)vertices.Count;

        vertices.Add(new() { PositionAndMatID = new(v0, matBits), NormalAndPad = new(normal, 0.0f) });
        vertices.Add(new() { PositionAndMatID = new(v1, matBits), NormalAndPad = new(normal, 0.0f) });
        vertices.Add(new() { PositionAndMatID = new(v2, matBits), NormalAndPad = new(normal, 0.0f) });
        vertices.Add(new() { PositionAndMatID = new(v3, matBits), NormalAndPad = new(normal, 0.0f) });

        indices.Add(startIndex);
        indices.Add(startIndex + 1);
        indices.Add(startIndex + 2);
        indices.Add(startIndex);
        indices.Add(startIndex + 2);
        indices.Add(startIndex + 3);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct PackedVertex
{
    [FieldOffset(0)]
    public Vector4 PositionAndMatID;

    [FieldOffset(16)]
    public Vector4 NormalAndPad;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct Material
{
    [FieldOffset(0)]
    public Vector4 AlbedoAndEmission;
}
