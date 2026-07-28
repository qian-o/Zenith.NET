using System.Numerics;
using System.Runtime.InteropServices;

namespace FluidTank.Helpers;

internal static class FluidTankGeometry
{
    public static void CreateScene(out SceneVertex[] vertices, out uint[] indices, out SceneMaterial[] materials)
    {
        List<SceneVertex> verticesList = [];
        List<uint> indicesList = [];

        AddBox(verticesList, indicesList, new(-6.25f, -0.28f, -3.25f), new(6.25f, 0.0f, 3.25f), 0);
        AddBox(verticesList, indicesList, new(-2.17f, 0.0f, 0.23f), new(-0.73f, 1.64f, 1.67f), 1);
        AddCylinder(verticesList, indicesList, new(1.15f, 0.0f, -0.85f), 0.62f, 1.84f, 32, 2);
        AddTransformedBox(verticesList,
                          indicesList,
                          new(3.45f, 0.62f, 0.55f),
                          new(1.15f, 0.16f, 1.05f),
                          Matrix4x4.CreateRotationZ(-0.35f),
                          3);

        const float frameWidth = 0.075f;

        AddBox(verticesList, indicesList, new(-6.10f, 0.0f, -3.10f), new(6.10f, frameWidth, -2.95f), 4);
        AddBox(verticesList, indicesList, new(-6.10f, 0.0f, 2.95f), new(6.10f, frameWidth, 3.10f), 4);
        AddBox(verticesList, indicesList, new(-6.10f, 0.0f, -2.95f), new(-5.95f, frameWidth, 2.95f), 4);
        AddBox(verticesList, indicesList, new(5.95f, 0.0f, -2.95f), new(6.10f, frameWidth, 2.95f), 4);

        AddBox(verticesList, indicesList, new(-6.10f, 0.0f, -3.10f), new(-5.95f, 5.25f, -2.95f), 4);
        AddBox(verticesList, indicesList, new(5.95f, 0.0f, -3.10f), new(6.10f, 5.25f, -2.95f), 4);
        AddBox(verticesList, indicesList, new(-6.10f, 0.0f, 2.95f), new(-5.95f, 5.25f, 3.10f), 4);
        AddBox(verticesList, indicesList, new(5.95f, 0.0f, 2.95f), new(6.10f, 5.25f, 3.10f), 4);

        AddBox(verticesList, indicesList, new(-6.10f, 5.12f, -3.10f), new(6.10f, 5.27f, -2.95f), 4);
        AddBox(verticesList, indicesList, new(-6.10f, 5.12f, 2.95f), new(6.10f, 5.27f, 3.10f), 4);
        AddBox(verticesList, indicesList, new(-6.10f, 5.12f, -2.95f), new(-5.95f, 5.27f, 2.95f), 4);
        AddBox(verticesList, indicesList, new(5.95f, 5.12f, -2.95f), new(6.10f, 5.27f, 2.95f), 4);

        vertices = [.. verticesList];
        indices = [.. indicesList];
        materials =
        [
            new() { Albedo = new(0.38f, 0.52f, 0.55f), Roughness = 0.48f, Metallic = 0.04f },
            new() { Albedo = new(0.12f, 0.28f, 0.31f), Roughness = 0.24f, Metallic = 0.18f },
            new() { Albedo = new(0.56f, 0.63f, 0.66f), Roughness = 0.16f, Metallic = 0.72f },
            new() { Albedo = new(0.08f, 0.20f, 0.28f), Roughness = 0.18f, Metallic = 0.35f },
            new() { Albedo = new(0.55f, 0.68f, 0.72f), Roughness = 0.10f, Metallic = 0.88f }
        ];
    }

    public static void CreateGlass(out SceneVertex[] vertices, out uint[] indices)
    {
        List<SceneVertex> verticesList = [];
        List<uint> indicesList = [];

        AddQuad(verticesList, indicesList, new(-6.0f, 0.0f, -3.0f), new(6.0f, 0.0f, -3.0f), new(6.0f, 5.2f, -3.0f), new(-6.0f, 5.2f, -3.0f), 0);
        AddQuad(verticesList, indicesList, new(6.0f, 0.0f, 3.0f), new(-6.0f, 0.0f, 3.0f), new(-6.0f, 5.2f, 3.0f), new(6.0f, 5.2f, 3.0f), 0);
        AddQuad(verticesList, indicesList, new(-6.0f, 0.0f, 3.0f), new(-6.0f, 0.0f, -3.0f), new(-6.0f, 5.2f, -3.0f), new(-6.0f, 5.2f, 3.0f), 0);
        AddQuad(verticesList, indicesList, new(6.0f, 0.0f, -3.0f), new(6.0f, 0.0f, 3.0f), new(6.0f, 5.2f, 3.0f), new(6.0f, 5.2f, -3.0f), 0);

        vertices = [.. verticesList];
        indices = [.. indicesList];
    }

    private static void AddBox(List<SceneVertex> vertices, List<uint> indices, Vector3 minimum, Vector3 maximum, uint materialId)
    {
        AddTransformedBox(vertices, indices, (minimum + maximum) * 0.5f, (maximum - minimum) * 0.5f, Matrix4x4.Identity, materialId);
    }

    private static void AddTransformedBox(List<SceneVertex> vertices,
                                          List<uint> indices,
                                          Vector3 center,
                                          Vector3 halfExtents,
                                          Matrix4x4 rotation,
                                          uint materialId)
    {
        Vector3[] local =
        [
            new(-halfExtents.X, -halfExtents.Y, -halfExtents.Z),
            new(halfExtents.X, -halfExtents.Y, -halfExtents.Z),
            new(halfExtents.X, halfExtents.Y, -halfExtents.Z),
            new(-halfExtents.X, halfExtents.Y, -halfExtents.Z),
            new(-halfExtents.X, -halfExtents.Y, halfExtents.Z),
            new(halfExtents.X, -halfExtents.Y, halfExtents.Z),
            new(halfExtents.X, halfExtents.Y, halfExtents.Z),
            new(-halfExtents.X, halfExtents.Y, halfExtents.Z)
        ];

        Vector3[] points = new Vector3[local.Length];
        for (int index = 0; index < local.Length; index++)
        {
            points[index] = Vector3.Transform(local[index], rotation) + center;
        }

        AddQuad(vertices, indices, points[1], points[0], points[3], points[2], materialId);
        AddQuad(vertices, indices, points[4], points[5], points[6], points[7], materialId);
        AddQuad(vertices, indices, points[0], points[4], points[7], points[3], materialId);
        AddQuad(vertices, indices, points[5], points[1], points[2], points[6], materialId);
        AddQuad(vertices, indices, points[3], points[7], points[6], points[2], materialId);
        AddQuad(vertices, indices, points[0], points[1], points[5], points[4], materialId);
    }

    private static void AddCylinder(List<SceneVertex> vertices,
                                    List<uint> indices,
                                    Vector3 baseCenter,
                                    float radius,
                                    float height,
                                    int segments,
                                    uint materialId)
    {
        for (int segment = 0; segment < segments; segment++)
        {
            float angle0 = segment * MathF.Tau / segments;
            float angle1 = (segment + 1) * MathF.Tau / segments;
            Vector3 radial0 = new(MathF.Cos(angle0) * radius, 0.0f, MathF.Sin(angle0) * radius);
            Vector3 radial1 = new(MathF.Cos(angle1) * radius, 0.0f, MathF.Sin(angle1) * radius);

            AddQuad(vertices, indices, baseCenter + radial1, baseCenter + radial0, baseCenter + radial0 + Vector3.UnitY * height, baseCenter + radial1 + Vector3.UnitY * height, materialId);
            AddTriangle(vertices, indices, baseCenter + Vector3.UnitY * height, baseCenter + radial1 + Vector3.UnitY * height, baseCenter + radial0 + Vector3.UnitY * height, materialId);
        }
    }

    private static void AddTriangle(List<SceneVertex> vertices, List<uint> indices, Vector3 v0, Vector3 v1, Vector3 v2, uint materialId)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
        uint startIndex = (uint)vertices.Count;

        vertices.Add(new() { Position = v0, Normal = normal, MaterialId = materialId });
        vertices.Add(new() { Position = v1, Normal = normal, MaterialId = materialId });
        vertices.Add(new() { Position = v2, Normal = normal, MaterialId = materialId });

        indices.Add(startIndex);
        indices.Add(startIndex + 1);
        indices.Add(startIndex + 2);
    }

    private static void AddQuad(List<SceneVertex> vertices,
                                List<uint> indices,
                                Vector3 v0,
                                Vector3 v1,
                                Vector3 v2,
                                Vector3 v3,
                                uint materialId)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
        uint startIndex = (uint)vertices.Count;

        vertices.Add(new() { Position = v0, Normal = normal, MaterialId = materialId });
        vertices.Add(new() { Position = v1, Normal = normal, MaterialId = materialId });
        vertices.Add(new() { Position = v2, Normal = normal, MaterialId = materialId });
        vertices.Add(new() { Position = v3, Normal = normal, MaterialId = materialId });

        indices.Add(startIndex);
        indices.Add(startIndex + 1);
        indices.Add(startIndex + 2);
        indices.Add(startIndex);
        indices.Add(startIndex + 2);
        indices.Add(startIndex + 3);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct SceneVertex
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(16)]
    public Vector3 Normal;

    [FieldOffset(28)]
    public uint MaterialId;
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct SceneMaterial
{
    [FieldOffset(0)]
    public Vector3 Albedo;

    [FieldOffset(12)]
    public float Roughness;

    [FieldOffset(16)]
    public float Metallic;

    [FieldOffset(20)]
    public float Emission;
}
