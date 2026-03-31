using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Helpers;

internal static class CornellBoxGeometry
{
    public static void Create(out Vertex[] vertices, out uint[] indices, out Material[] materials)
    {
        List<Vertex> verticesList = [];
        List<uint> indicesList = [];

        // 0: Left wall (red)
        AddQuad(verticesList,
                indicesList,
                new(552.8f, 0.0f, 0.0f),
                new(549.6f, 0.0f, 559.2f),
                new(556.0f, 548.8f, 559.2f),
                new(556.0f, 548.8f, 0.0f),
                0);

        // 1: Right wall (green)
        AddQuad(verticesList,
                indicesList,
                new(0.0f, 0.0f, 559.2f),
                new(0.0f, 0.0f, 0.0f),
                new(0.0f, 548.8f, 0.0f),
                new(0.0f, 548.8f, 559.2f),
                1);

        // 2: Ceiling (white)
        AddQuad(verticesList,
                indicesList,
                new(556.0f, 548.8f, 0.0f),
                new(556.0f, 548.8f, 559.2f),
                new(0.0f, 548.8f, 559.2f),
                new(0.0f, 548.8f, 0.0f),
                2);

        // 3: Floor (white)
        AddQuad(verticesList,
                indicesList,
                new(552.8f, 0.0f, 0.0f),
                new(0.0f, 0.0f, 0.0f),
                new(0.0f, 0.0f, 559.2f),
                new(549.6f, 0.0f, 559.2f),
                2);

        // 4: Back wall (white)
        AddQuad(verticesList,
                indicesList,
                new(549.6f, 0.0f, 559.2f),
                new(0.0f, 0.0f, 559.2f),
                new(0.0f, 548.8f, 559.2f),
                new(556.0f, 548.8f, 559.2f),
                2);

        // 5-9: Short block
        AddQuad(verticesList, indicesList, new(130.0f, 165.0f, 65.0f), new(82.0f, 165.0f, 225.0f), new(240.0f, 165.0f, 272.0f), new(290.0f, 165.0f, 114.0f), 4);
        AddQuad(verticesList, indicesList, new(290.0f, 0.0f, 114.0f), new(290.0f, 165.0f, 114.0f), new(240.0f, 165.0f, 272.0f), new(240.0f, 0.0f, 272.0f), 4);
        AddQuad(verticesList, indicesList, new(130.0f, 0.0f, 65.0f), new(130.0f, 165.0f, 65.0f), new(290.0f, 165.0f, 114.0f), new(290.0f, 0.0f, 114.0f), 4);
        AddQuad(verticesList, indicesList, new(82.0f, 0.0f, 225.0f), new(82.0f, 165.0f, 225.0f), new(130.0f, 165.0f, 65.0f), new(130.0f, 0.0f, 65.0f), 4);
        AddQuad(verticesList, indicesList, new(240.0f, 0.0f, 272.0f), new(240.0f, 165.0f, 272.0f), new(82.0f, 165.0f, 225.0f), new(82.0f, 0.0f, 225.0f), 4);

        // 10-14: Tall block
        AddQuad(verticesList, indicesList, new(423.0f, 330.0f, 247.0f), new(265.0f, 330.0f, 296.0f), new(314.0f, 330.0f, 456.0f), new(472.0f, 330.0f, 406.0f), 5);
        AddQuad(verticesList, indicesList, new(423.0f, 0.0f, 247.0f), new(423.0f, 330.0f, 247.0f), new(472.0f, 330.0f, 406.0f), new(472.0f, 0.0f, 406.0f), 5);
        AddQuad(verticesList, indicesList, new(472.0f, 0.0f, 406.0f), new(472.0f, 330.0f, 406.0f), new(314.0f, 330.0f, 456.0f), new(314.0f, 0.0f, 456.0f), 5);
        AddQuad(verticesList, indicesList, new(314.0f, 0.0f, 456.0f), new(314.0f, 330.0f, 456.0f), new(265.0f, 330.0f, 296.0f), new(265.0f, 0.0f, 296.0f), 5);
        AddQuad(verticesList, indicesList, new(265.0f, 0.0f, 296.0f), new(265.0f, 330.0f, 296.0f), new(423.0f, 330.0f, 247.0f), new(423.0f, 0.0f, 247.0f), 5);

        // 15: Light
        AddQuad(verticesList,
                indicesList,
                new(343.0f, 548.6f, 227.0f),
                new(343.0f, 548.6f, 332.0f),
                new(213.0f, 548.6f, 332.0f),
                new(213.0f, 548.6f, 227.0f),
                3);

        vertices = [.. verticesList];
        indices = [.. indicesList];
        materials =
        [
            new() { Albedo = new(0.63f, 0.06f, 0.06f), Emission = 0.00f, Metallic = 0.0f, Roughness = 0.90f },
            new() { Albedo = new(0.14f, 0.45f, 0.09f), Emission = 0.00f, Metallic = 0.0f, Roughness = 0.90f },
            new() { Albedo = new(0.73f, 0.71f, 0.68f), Emission = 0.00f, Metallic = 0.0f, Roughness = 0.90f },
            new() { Albedo = new(1.00f, 0.85f, 0.60f), Emission = 25.0f, Metallic = 0.0f, Roughness = 0.50f },
            new() { Albedo = new(0.73f, 0.71f, 0.68f), Emission = 0.00f, Metallic = 0.0f, Roughness = 0.30f },
            new() { Albedo = new(0.95f, 0.93f, 0.88f), Emission = 0.00f, Metallic = 1.0f, Roughness = 0.05f }
        ];
    }

    private static void AddQuad(List<Vertex> vertices,
                                List<uint> indices,
                                Vector3 v0,
                                Vector3 v1,
                                Vector3 v2,
                                Vector3 v3,
                                uint materialID)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));

        uint startIndex = (uint)vertices.Count;

        vertices.Add(new() { Position = v0, Normal = normal, MaterialID = materialID });
        vertices.Add(new() { Position = v1, Normal = normal, MaterialID = materialID });
        vertices.Add(new() { Position = v2, Normal = normal, MaterialID = materialID });
        vertices.Add(new() { Position = v3, Normal = normal, MaterialID = materialID });

        indices.Add(startIndex);
        indices.Add(startIndex + 1);
        indices.Add(startIndex + 2);
        indices.Add(startIndex);
        indices.Add(startIndex + 2);
        indices.Add(startIndex + 3);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct Vertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(16)]
    public Vector3 Normal;

    [FieldOffset(28)]
    public uint MaterialID;
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct Material
{
    [FieldOffset(0)]
    public Vector3 Albedo;

    [FieldOffset(12)]
    public float Emission;

    [FieldOffset(16)]
    public float Metallic;

    [FieldOffset(20)]
    public float Roughness;
}
