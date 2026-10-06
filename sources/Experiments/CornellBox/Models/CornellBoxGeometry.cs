using System.Numerics;

namespace CornellBox.Models;

internal static class CornellBoxGeometry
{
    public static void Create(out Vertex[] vertices, out uint[] indices, out Material[] materials, out Mesh[] meshes)
    {
        List<Vertex> verticesList = [];
        List<uint> indicesList = [];
        List<Mesh> meshesList = [];

        AddQuad(verticesList,
                indicesList,
                new(552.8f, 0.0f, 0.0f),
                new(549.6f, 0.0f, 559.2f),
                new(556.0f, 548.8f, 559.2f),
                new(556.0f, 548.8f, 0.0f),
                0);

        AddQuad(verticesList,
                indicesList,
                new(0.0f, 0.0f, 559.2f),
                new(0.0f, 0.0f, 0.0f),
                new(0.0f, 548.8f, 0.0f),
                new(0.0f, 548.8f, 559.2f),
                1);

        AddQuad(verticesList,
                indicesList,
                new(556.0f, 548.8f, 0.0f),
                new(556.0f, 548.8f, 559.2f),
                new(0.0f, 548.8f, 559.2f),
                new(0.0f, 548.8f, 0.0f),
                2);

        AddQuad(verticesList,
                indicesList,
                new(552.8f, 0.0f, 0.0f),
                new(0.0f, 0.0f, 0.0f),
                new(0.0f, 0.0f, 559.2f),
                new(549.6f, 0.0f, 559.2f),
                6);

        AddQuad(verticesList,
                indicesList,
                new(549.6f, 0.0f, 559.2f),
                new(0.0f, 0.0f, 559.2f),
                new(0.0f, 548.8f, 559.2f),
                new(556.0f, 548.8f, 559.2f),
                7);

        AddQuad(verticesList, indicesList, new(130.0f, 165.0f, 65.0f), new(82.0f, 165.0f, 225.0f), new(240.0f, 165.0f, 272.0f), new(290.0f, 165.0f, 114.0f), 4);
        AddQuad(verticesList, indicesList, new(290.0f, 0.0f, 114.0f), new(290.0f, 165.0f, 114.0f), new(240.0f, 165.0f, 272.0f), new(240.0f, 0.0f, 272.0f), 4);
        AddQuad(verticesList, indicesList, new(130.0f, 0.0f, 65.0f), new(130.0f, 165.0f, 65.0f), new(290.0f, 165.0f, 114.0f), new(290.0f, 0.0f, 114.0f), 4);
        AddQuad(verticesList, indicesList, new(82.0f, 0.0f, 225.0f), new(82.0f, 165.0f, 225.0f), new(130.0f, 165.0f, 65.0f), new(130.0f, 0.0f, 65.0f), 4);
        AddQuad(verticesList, indicesList, new(240.0f, 0.0f, 272.0f), new(240.0f, 165.0f, 272.0f), new(82.0f, 165.0f, 225.0f), new(82.0f, 0.0f, 225.0f), 4);

        AddQuad(verticesList, indicesList, new(423.0f, 330.0f, 247.0f), new(265.0f, 330.0f, 296.0f), new(314.0f, 330.0f, 456.0f), new(472.0f, 330.0f, 406.0f), 5);
        AddQuad(verticesList, indicesList, new(423.0f, 0.0f, 247.0f), new(423.0f, 330.0f, 247.0f), new(472.0f, 330.0f, 406.0f), new(472.0f, 0.0f, 406.0f), 5);
        AddQuad(verticesList, indicesList, new(472.0f, 0.0f, 406.0f), new(472.0f, 330.0f, 406.0f), new(314.0f, 330.0f, 456.0f), new(314.0f, 0.0f, 456.0f), 5);
        AddQuad(verticesList, indicesList, new(314.0f, 0.0f, 456.0f), new(314.0f, 330.0f, 456.0f), new(265.0f, 330.0f, 296.0f), new(265.0f, 0.0f, 296.0f), 5);
        AddQuad(verticesList, indicesList, new(265.0f, 0.0f, 296.0f), new(265.0f, 330.0f, 296.0f), new(423.0f, 330.0f, 247.0f), new(423.0f, 0.0f, 247.0f), 5);

        AddQuad(verticesList,
                indicesList,
                new(343.0f, 547.0f, 227.0f),
                new(343.0f, 547.0f, 332.0f),
                new(213.0f, 547.0f, 332.0f),
                new(213.0f, 547.0f, 227.0f),
                3);

        AddSphere(verticesList, indicesList, new(185.5f, 205.0f, 169.0f), 40.0f, 8);

        meshesList.Add(new(0, (uint)indicesList.Count));

        uint firstIndex = (uint)indicesList.Count;
        AddSphere(verticesList, indicesList, Vector3.Zero, 45.0f, 9);
        meshesList.Add(new(firstIndex, (uint)indicesList.Count - firstIndex));

        firstIndex = (uint)indicesList.Count;
        AddTorus(verticesList, indicesList, 75.0f, 2.4f, 10);
        meshesList.Add(new(firstIndex, (uint)indicesList.Count - firstIndex));

        firstIndex = (uint)indicesList.Count;
        AddTorus(verticesList, indicesList, 62.0f, 2.1f, 11);
        meshesList.Add(new(firstIndex, (uint)indicesList.Count - firstIndex));

        firstIndex = (uint)indicesList.Count;
        AddTorus(verticesList, indicesList, 49.0f, 1.8f, 12);
        meshesList.Add(new(firstIndex, (uint)indicesList.Count - firstIndex));

        vertices = [.. verticesList];
        indices = [.. indicesList];
        meshes = [.. meshesList];
        materials =
        [
            new()
            {
                Albedo = new(0.63f, 0.06f, 0.06f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.90f
            },
            new()
            {
                Albedo = new(0.14f, 0.45f, 0.09f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.90f
            },
            new()
            {
                Albedo = new(0.73f, 0.71f, 0.68f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.90f
            },
            new()
            {
                Albedo = new(1.00f, 0.85f, 0.60f),
                Emission = 25.0f,
                Metallic = 0.0f,
                Roughness = 0.50f
            },
            new()
            {
                Albedo = new(0.73f, 0.71f, 0.68f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.30f
            },
            new()
            {
                Albedo = new(0.95f, 0.93f, 0.88f),
                Emission = 0.00f,
                Metallic = 1.0f,
                Roughness = 0.05f
            },
            new()
            {
                Albedo = new(0.73f, 0.71f, 0.68f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.20f,
                Pattern = SurfacePattern.Checker,
                PatternScale = 35.0f
            },
            new()
            {
                Albedo = new(0.73f, 0.71f, 0.68f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.90f,
                Pattern = SurfacePattern.Grid,
                PatternScale = 40.0f
            },
            new()
            {
                Albedo = new(1.00f, 0.78f, 0.34f),
                Emission = 0.00f,
                Metallic = 1.0f,
                Roughness = 0.30f
            },
            new()
            {
                Albedo = new(0.95f, 0.95f, 0.95f),
                Emission = 0.00f,
                Metallic = 1.0f,
                Roughness = 0.00f
            },
            new()
            {
                Albedo = new(0.95f, 0.64f, 0.54f),
                Emission = 0.00f,
                Metallic = 1.0f,
                Roughness = 0.25f
            },
            new()
            {
                Albedo = new(0.85f, 0.85f, 0.82f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.15f
            },
            new()
            {
                Albedo = new(0.10f, 0.25f, 0.80f),
                Emission = 0.00f,
                Metallic = 0.0f,
                Roughness = 0.30f
            }
        ];
    }

    private static void AddQuad(List<Vertex> vertices,
                                List<uint> indices,
                                Vector3 v0,
                                Vector3 v1,
                                Vector3 v2,
                                Vector3 v3,
                                uint materialId)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));

        uint startIndex = (uint)vertices.Count;

        vertices.Add(new()
        {
            Position = v0,
            Normal = normal,
            MaterialId = materialId
        });

        vertices.Add(new()
        {
            Position = v1,
            Normal = normal,
            MaterialId = materialId
        });

        vertices.Add(new()
        {
            Position = v2,
            Normal = normal,
            MaterialId = materialId
        });

        vertices.Add(new()
        {
            Position = v3,
            Normal = normal,
            MaterialId = materialId
        });

        indices.Add(startIndex);
        indices.Add(startIndex + 1);
        indices.Add(startIndex + 2);
        indices.Add(startIndex);
        indices.Add(startIndex + 2);
        indices.Add(startIndex + 3);
    }

    private static void AddSphere(List<Vertex> vertices, List<uint> indices, Vector3 center, float radius, uint materialId)
    {
        const int Segments = 64;
        const int Rings = 32;

        uint startIndex = (uint)vertices.Count;

        for (int y = 0; y <= Rings; y++)
        {
            float theta = y * MathF.PI / Rings;

            for (int x = 0; x <= Segments; x++)
            {
                float phi = x * MathF.Tau / Segments;

                Vector3 normal = new(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));

                vertices.Add(new()
                {
                    Position = center + (normal * radius),
                    Normal = normal,
                    MaterialId = materialId
                });
            }
        }

        for (int y = 0; y < Rings; y++)
        {
            for (int x = 0; x < Segments; x++)
            {
                uint top = startIndex + (uint)((y * (Segments + 1)) + x);
                uint bottom = top + Segments + 1;

                if (y > 0)
                {
                    indices.AddRange([top, top + 1, bottom + 1]);
                }

                if (y < Rings - 1)
                {
                    indices.AddRange([top, bottom + 1, bottom]);
                }
            }
        }
    }

    private static void AddTorus(List<Vertex> vertices, List<uint> indices, float majorRadius, float minorRadius, uint materialId)
    {
        const int MajorSegments = 128;
        const int MinorSegments = 16;

        uint startIndex = (uint)vertices.Count;

        for (int i = 0; i <= MajorSegments; i++)
        {
            float u = i * MathF.Tau / MajorSegments;

            Vector3 direction = new(MathF.Cos(u), MathF.Sin(u), 0.0f);

            for (int j = 0; j <= MinorSegments; j++)
            {
                float v = j * MathF.Tau / MinorSegments;

                Vector3 normal = (direction * MathF.Cos(v)) + (Vector3.UnitZ * MathF.Sin(v));

                vertices.Add(new()
                {
                    Position = (direction * majorRadius) + (normal * minorRadius),
                    Normal = normal,
                    MaterialId = materialId
                });
            }
        }

        for (int i = 0; i < MajorSegments; i++)
        {
            for (int j = 0; j < MinorSegments; j++)
            {
                uint current = startIndex + (uint)((i * (MinorSegments + 1)) + j);
                uint next = current + MinorSegments + 1;

                indices.AddRange([current, next, next + 1, current, next + 1, current + 1]);
            }
        }
    }
}
