using System.Numerics;

namespace CornellBox.Models;

internal static class CornellBoxGeometry
{
    internal const int SculpturePartCount = 3;
    internal const int FirstCubeMesh = 5;
    internal const int FirstSphereMesh = FirstCubeMesh + SculpturePartCount;
    internal const int MeshCount = FirstSphereMesh + SculpturePartCount;

    private const int InitialVertexCapacity = 40_000;
    private const int InitialIndexCapacity = 200_000;
    private const uint LimestoneMaterial = 22;
    private const uint LightWoodMaterial = 23;
    private const uint SageMaterial = 24;
    private const uint BronzeMaterial = 25;
    private const uint TerracottaMaterial = 26;

    public static void Create(out Vertex[] vertices, out uint[] indices, out Material[] materials, out Mesh[] meshes)
    {
        List<Vertex> verticesList = new(InitialVertexCapacity);
        List<uint> indicesList = new(InitialIndexCapacity);
        meshes = new Mesh[MeshCount];

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

        AddCourtyard(verticesList, indicesList);

        meshes[0] = new(0, (uint)indicesList.Count);

        uint firstIndex = (uint)indicesList.Count;
        AddSphere(verticesList, indicesList, Vector3.Zero, 45.0f, 9);
        meshes[1] = new(firstIndex, (uint)indicesList.Count - firstIndex);

        firstIndex = (uint)indicesList.Count;
        AddTorus(verticesList, indicesList, 75.0f, 2.4f, 10);
        meshes[2] = new(firstIndex, (uint)indicesList.Count - firstIndex);

        firstIndex = (uint)indicesList.Count;
        AddTorus(verticesList, indicesList, 62.0f, 2.1f, 11);
        meshes[3] = new(firstIndex, (uint)indicesList.Count - firstIndex);

        firstIndex = (uint)indicesList.Count;
        AddTorus(verticesList, indicesList, 49.0f, 1.8f, 12);
        meshes[4] = new(firstIndex, (uint)indicesList.Count - firstIndex);

        ReadOnlySpan<float> cubeSizes = [35.0f, 28.0f, 22.0f];
        ReadOnlySpan<uint> cubeMaterials = [8, 9, 10];
        for (int i = 0; i < SculpturePartCount; i++)
        {
            firstIndex = (uint)indicesList.Count;
            Vector3 halfSize = new(cubeSizes[i]);
            AddBox(verticesList, indicesList, -halfSize, halfSize, cubeMaterials[i]);
            meshes[FirstCubeMesh + i] = new(firstIndex, (uint)indicesList.Count - firstIndex);
        }

        ReadOnlySpan<float> sphereRadii = [48.0f, 32.0f, 22.0f];
        ReadOnlySpan<uint> sphereMaterials = [12, 8, 12];
        for (int i = 0; i < SculpturePartCount; i++)
        {
            firstIndex = (uint)indicesList.Count;
            AddSphere(verticesList, indicesList, Vector3.Zero, sphereRadii[i], sphereMaterials[i]);
            meshes[FirstSphereMesh + i] = new(firstIndex, (uint)indicesList.Count - firstIndex);
        }

        vertices = [.. verticesList];
        indices = [.. indicesList];
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
                Pattern = SurfacePattern.None
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
                Metallic = 1.0f,
                Roughness = 0.12f
            },
            new()
            {
                Albedo = new(0.36f, 0.42f, 0.46f),
                Roughness = 0.58f,
                Pattern = SurfacePattern.Grid,
                PatternScale = 140.0f
            },
            new()
            {
                Albedo = new(0.76f, 0.70f, 0.58f),
                Roughness = 0.72f
            },
            new()
            {
                Albedo = new(0.08f, 0.11f, 0.15f),
                Metallic = 0.80f,
                Roughness = 0.28f
            },
            new()
            {
                Albedo = new(0.85f, 0.55f, 0.20f),
                Metallic = 1.0f,
                Roughness = 0.08f
            },
            new()
            {
                Albedo = new(0.85f, 0.55f, 0.20f),
                Metallic = 1.0f,
                Roughness = 0.35f
            },
            new()
            {
                Albedo = new(0.85f, 0.55f, 0.20f),
                Metallic = 1.0f,
                Roughness = 0.70f
            },
            new()
            {
                Albedo = new(0.30f, 0.12f, 0.045f),
                Roughness = 0.65f
            },
            new()
            {
                Albedo = new(0.07f, 0.22f, 0.055f),
                Roughness = 0.95f
            },
            new()
            {
                Albedo = new(0.12f, 0.075f, 0.035f),
                Roughness = 1.0f
            },
            new()
            {
                Albedo = new(0.64f, 0.59f, 0.48f),
                Roughness = 0.48f
            },
            new()
            {
                Albedo = new(0.39f, 0.19f, 0.075f),
                Roughness = 0.52f
            },
            new()
            {
                Albedo = new(0.20f, 0.32f, 0.10f),
                Roughness = 0.88f
            },
            new()
            {
                Albedo = new(0.55f, 0.30f, 0.12f),
                Metallic = 1.0f,
                Roughness = 0.32f
            },
            new()
            {
                Albedo = new(0.42f, 0.18f, 0.10f),
                Roughness = 0.82f
            }
        ];
    }

    private static void AddCourtyard(List<Vertex> vertices, List<uint> indices)
    {
        AddQuad(vertices,
                indices,
                new(-1350.0f, -32.0f, -1350.0f),
                new(-1350.0f, -32.0f, 1500.0f),
                new(1900.0f, -32.0f, 1500.0f),
                new(1900.0f, -32.0f, -1350.0f), 13);

        AddBox(vertices, indices, new(-1350.0f, -55.0f, -1350.0f), new(-1290.0f, -24.0f, 1500.0f), 14);
        AddBox(vertices, indices, new(1840.0f, -55.0f, -1350.0f), new(1900.0f, -24.0f, 1500.0f), 14);
        AddBox(vertices, indices, new(-1290.0f, -55.0f, 1440.0f), new(1840.0f, -24.0f, 1500.0f), 14);
        AddBox(vertices, indices, new(-1290.0f, -55.0f, -1350.0f), new(1840.0f, -24.0f, -1290.0f), 14);

        AddExhibitPaving(vertices, indices);

        AddBox(vertices, indices, new(-35.0f, -32.0f, -30.0f), new(590.0f, -1.0f, 590.0f), 15);
        AddBox(vertices, indices, new(60.0f, -32.0f, -330.0f), new(496.0f, -24.0f, -230.0f), 14);
        AddBox(vertices, indices, new(60.0f, -32.0f, -230.0f), new(496.0f, -16.0f, -130.0f), 14);
        AddBox(vertices, indices, new(60.0f, -32.0f, -130.0f), new(496.0f, -8.0f, -30.0f), 14);

        for (int i = 0; i < 3; i++)
        {
            float z = -330.0f + (i * 100.0f);
            float y = -24.0f + (i * 8.0f);
            AddBox(vertices, indices, new(60.0f, y, z), new(496.0f, y + 1.0f, z + 5.0f), 8);
        }

        ReadOnlySpan<float> columnPositions = [-1080.0f, 1636.0f];
        for (int i = 0; i < 4; i++)
        {
            float z = -150.0f + (i * 380.0f);

            foreach (float x in columnPositions)
            {
                AddBox(vertices, indices, new(x - 65.0f, -32.0f, z - 65.0f), new(x + 65.0f, 0.0f, z + 65.0f), 15);
                AddBox(vertices, indices, new(x - 38.0f, 0.0f, z - 38.0f), new(x + 38.0f, 620.0f, z + 38.0f), 14);
                AddBox(vertices, indices, new(x - 65.0f, 620.0f, z - 65.0f), new(x + 65.0f, 650.0f, z + 65.0f), 14);
                AddBox(vertices, indices, new(x - 51.0f, 0.0f, z - 51.0f), new(x + 51.0f, 24.0f, z + 51.0f), 14);
                AddBox(vertices, indices, new(x - 44.0f, 24.0f, z - 44.0f), new(x + 44.0f, 42.0f, z + 44.0f), 14);
                AddBox(vertices, indices, new(x - 47.0f, 584.0f, z - 47.0f), new(x + 47.0f, 604.0f, z + 47.0f), 14);
                AddBox(vertices, indices, new(x - 55.0f, 604.0f, z - 55.0f), new(x + 55.0f, 620.0f, z + 55.0f), 15);

                AddBox(vertices, indices, new(x - 25.0f, 65.0f, z - 39.0f), new(x + 25.0f, 550.0f, z - 38.0f), LimestoneMaterial);
                AddBox(vertices, indices, new(x - 25.0f, 65.0f, z + 38.0f), new(x + 25.0f, 550.0f, z + 39.0f), LimestoneMaterial);
                AddBox(vertices, indices, new(x - 40.0f, 558.0f, z - 40.0f), new(x + 40.0f, 566.0f, z + 40.0f), BronzeMaterial);
            }
        }

        AddBox(vertices, indices, new(-1150.0f, 650.0f, -220.0f), new(-1010.0f, 705.0f, 1060.0f), 14);
        AddBox(vertices, indices, new(1566.0f, 650.0f, -220.0f), new(1706.0f, 705.0f, 1060.0f), 14);
        AddBox(vertices, indices, new(-1010.0f, 650.0f, 920.0f), new(1566.0f, 705.0f, 1060.0f), 14);
        AddBox(vertices, indices, new(-1162.0f, 705.0f, -232.0f), new(-998.0f, 723.0f, 1072.0f), 15);
        AddBox(vertices, indices, new(1554.0f, 705.0f, -232.0f), new(1718.0f, 723.0f, 1072.0f), 15);
        AddBox(vertices, indices, new(-998.0f, 705.0f, 908.0f), new(1554.0f, 723.0f, 1072.0f), 15);

        ReadOnlySpan<uint> exhibitMaterials = [9, 12, 8];
        for (int i = 0; i < 3; i++)
        {
            float z = -380.0f + (i * 380.0f);

            AddPlinth(vertices, indices, new(-670.0f, 0.0f, z), 95.0f, 70.0f);
            AddSphere(vertices, indices, new(-670.0f, 140.0f, z), 70.0f, (uint)(16 + i));

            AddPlinth(vertices, indices, new(1226.0f, 0.0f, z), 95.0f, 70.0f);
            AddSphere(vertices, indices, new(1226.0f, 140.0f, z), 70.0f, exhibitMaterials[i]);
        }

        AddPlinth(vertices, indices, new(-320.0f, 0.0f, 840.0f), 140.0f, 45.0f);
        AddPlinth(vertices, indices, new(876.0f, 0.0f, 840.0f), 140.0f, 45.0f);

        ReadOnlySpan<float> benchPositions = [-320.0f, 876.0f];
        foreach (float x in benchPositions)
        {
            AddBench(vertices, indices, new(x, -700.0f));
        }

        ReadOnlySpan<float> planterPositionsZ = [-860.0f, 1260.0f];
        foreach (float x in columnPositions)
        {
            foreach (float z in planterPositionsZ)
            {
                AddPlanter(vertices, indices, new(x, 0.0f, z));
            }
        }
    }

    private static void AddExhibitPaving(List<Vertex> vertices, List<uint> indices)
    {
        ReadOnlySpan<float> pavingCenters = [-670.0f, 1226.0f];
        foreach (float x in pavingCenters)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = -380.0f + (i * 380.0f);
                AddBox(vertices, indices, new(x - 125.0f, -32.0f, z - 125.0f), new(x + 125.0f, -30.0f, z + 125.0f), LimestoneMaterial);
            }
        }
    }

    private static void AddBench(List<Vertex> vertices, List<uint> indices, Vector2 center)
    {
        float x = center.X;
        float z = center.Y;
        ReadOnlySpan<float> endPositions = [-160.0f, 160.0f];
        foreach (float offset in endPositions)
        {
            AddBox(vertices, indices, new(x - 77.0f, -32.0f, z + offset - 14.0f), new(x + 77.0f, 55.0f, z + offset + 14.0f), 14);
        }

        AddBox(vertices, indices, new(x - 62.0f, 35.0f, z - 170.0f), new(x + 62.0f, 54.0f, z + 170.0f), 15);

        ReadOnlySpan<float> fastenerPositions = [-65.0f, 65.0f];
        for (int i = 0; i < 9; i++)
        {
            float slatZ = z - 176.0f + (i * 40.0f);
            uint material = (i % 3) == 1 ? LightWoodMaterial : 19;
            AddBox(vertices, indices, new(x - 86.0f, 55.0f, slatZ), new(x + 86.0f, 70.0f, slatZ + 32.0f), material);

            foreach (float offset in fastenerPositions)
            {
                AddBox(vertices, indices, new(x + offset - 2.0f, 70.0f, slatZ + 7.0f), new(x + offset + 2.0f, 71.0f, slatZ + 11.0f), BronzeMaterial);
            }
        }

        foreach (float offset in endPositions)
        {
            float armrestZ = z + offset;
            AddBox(vertices, indices, new(x - 62.0f, 70.0f, armrestZ - 5.0f), new(x - 52.0f, 96.0f, armrestZ + 5.0f), 15);
            AddBox(vertices, indices, new(x + 52.0f, 70.0f, armrestZ - 5.0f), new(x + 62.0f, 96.0f, armrestZ + 5.0f), 15);
            AddBox(vertices, indices, new(x - 65.0f, 96.0f, armrestZ - 7.0f), new(x + 65.0f, 104.0f, armrestZ + 7.0f), BronzeMaterial);
        }
    }

    private static void AddPlanter(List<Vertex> vertices, List<uint> indices, Vector3 center)
    {
        float x = center.X;
        float z = center.Z;
        AddBox(vertices, indices, new(x - 118.0f, -32.0f, z - 118.0f), new(x + 118.0f, -12.0f, z + 118.0f), 15);
        AddBox(vertices, indices, new(x - 110.0f, -12.0f, z - 110.0f), new(x + 110.0f, 55.0f, z + 110.0f), TerracottaMaterial);
        AddBox(vertices, indices, new(x - 96.0f, 55.0f, z - 96.0f), new(x + 96.0f, 58.0f, z + 96.0f), 21);
        AddBox(vertices, indices, new(x - 118.0f, 55.0f, z - 118.0f), new(x - 96.0f, 70.0f, z + 118.0f), 14);
        AddBox(vertices, indices, new(x + 96.0f, 55.0f, z - 118.0f), new(x + 118.0f, 70.0f, z + 118.0f), 14);
        AddBox(vertices, indices, new(x - 96.0f, 55.0f, z - 118.0f), new(x + 96.0f, 70.0f, z - 96.0f), 14);
        AddBox(vertices, indices, new(x - 96.0f, 55.0f, z + 96.0f), new(x + 96.0f, 70.0f, z + 118.0f), 14);
        AddSphere(vertices, indices, new(x - 42.0f, 105.0f, z - 20.0f), 60.0f, SageMaterial, 12, 8);
        AddSphere(vertices, indices, new(x + 40.0f, 100.0f, z + 28.0f), 56.0f, 20, 12, 8);
        AddSphere(vertices, indices, new(x, 145.0f, z + 5.0f), 62.0f, 20, 12, 8);

        ReadOnlySpan<Vector2> pebbleOffsets = [new(-77.0f, -72.0f), new(-28.0f, -80.0f), new(30.0f, -78.0f), new(78.0f, -65.0f)];
        foreach (Vector2 offset in pebbleOffsets)
        {
            AddSphere(vertices, indices, new(x + offset.X, 62.0f, z + offset.Y), 6.0f, LimestoneMaterial, 8, 6);
        }
    }

    private static void AddPlinth(List<Vertex> vertices, List<uint> indices, Vector3 center, float halfSize, float height)
    {
        Vector3 minimum = new(center.X - halfSize, -32.0f, center.Z - halfSize);
        Vector3 maximum = new(center.X + halfSize, height, center.Z + halfSize);
        AddBox(vertices, indices, new(minimum.X, -18.0f, minimum.Z), new(maximum.X, height - 10.0f, maximum.Z), 15);
        AddBox(vertices, indices, minimum - new Vector3(8.0f, 0.0f, 8.0f), new(maximum.X + 8.0f, -18.0f, maximum.Z + 8.0f), 14);
        AddBox(vertices, indices, new(minimum.X - 5.0f, height - 10.0f, minimum.Z - 5.0f), new(maximum.X + 5.0f, height, maximum.Z + 5.0f), 14);
        AddBox(vertices, indices, new(center.X - 26.0f, height - 32.0f, minimum.Z - 1.0f), new(center.X + 26.0f, height - 17.0f, minimum.Z), 8);

        ReadOnlySpan<float> cornerOffsets = [-1.0f, 1.0f];
        foreach (float x in cornerOffsets)
        {
            foreach (float z in cornerOffsets)
            {
                float cornerX = center.X + (x * (halfSize - 9.0f));
                float cornerZ = center.Z + (z * (halfSize - 9.0f));
                AddBox(vertices, indices, new(cornerX - 2.0f, height, cornerZ - 2.0f), new(cornerX + 2.0f, height + 1.0f, cornerZ + 2.0f), BronzeMaterial);
            }
        }
    }

    private static void AddBox(List<Vertex> vertices, List<uint> indices, Vector3 minimum, Vector3 maximum, uint materialId)
    {
        AddQuad(vertices, indices, new(minimum.X, maximum.Y, minimum.Z), new(minimum.X, maximum.Y, maximum.Z), maximum, new(maximum.X, maximum.Y, minimum.Z), materialId);
        AddQuad(vertices, indices, minimum, new(maximum.X, minimum.Y, minimum.Z), new(maximum.X, minimum.Y, maximum.Z), new(minimum.X, minimum.Y, maximum.Z), materialId);
        AddQuad(vertices, indices, minimum, new(minimum.X, maximum.Y, minimum.Z), new(maximum.X, maximum.Y, minimum.Z), new(maximum.X, minimum.Y, minimum.Z), materialId);
        AddQuad(vertices, indices, new(minimum.X, minimum.Y, maximum.Z), new(maximum.X, minimum.Y, maximum.Z), maximum, new(minimum.X, maximum.Y, maximum.Z), materialId);
        AddQuad(vertices, indices, minimum, new(minimum.X, minimum.Y, maximum.Z), new(minimum.X, maximum.Y, maximum.Z), new(minimum.X, maximum.Y, minimum.Z), materialId);
        AddQuad(vertices, indices, new(maximum.X, minimum.Y, minimum.Z), new(maximum.X, maximum.Y, minimum.Z), maximum, new(maximum.X, minimum.Y, maximum.Z), materialId);
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

    private static void AddSphere(List<Vertex> vertices, List<uint> indices, Vector3 center, float radius, uint materialId, int segments = 64, int rings = 32)
    {
        uint startIndex = (uint)vertices.Count;
        Span<Vector2> longitudeDirections = stackalloc Vector2[segments + 1];
        for (int x = 0; x <= segments; x++)
        {
            float phi = x * MathF.Tau / segments;
            longitudeDirections[x] = new(MathF.Cos(phi), MathF.Sin(phi));
        }

        for (int y = 0; y <= rings; y++)
        {
            float theta = y * MathF.PI / rings;
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (int x = 0; x <= segments; x++)
            {
                Vector2 longitude = longitudeDirections[x];
                Vector3 normal = new(sinTheta * longitude.X, cosTheta, sinTheta * longitude.Y);

                vertices.Add(new()
                {
                    Position = center + (normal * radius),
                    Normal = normal,
                    MaterialId = materialId
                });
            }
        }

        for (int y = 0; y < rings; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                uint top = startIndex + (uint)((y * (segments + 1)) + x);
                uint bottom = top + (uint)segments + 1;

                if (y > 0)
                {
                    indices.Add(top);
                    indices.Add(top + 1);
                    indices.Add(bottom + 1);
                }

                if (y < rings - 1)
                {
                    indices.Add(top);
                    indices.Add(bottom + 1);
                    indices.Add(bottom);
                }
            }
        }
    }

    private static void AddTorus(List<Vertex> vertices, List<uint> indices, float majorRadius, float minorRadius, uint materialId)
    {
        const int MajorSegments = 128;
        const int MinorSegments = 16;

        uint startIndex = (uint)vertices.Count;
        Span<Vector2> tubeDirections = stackalloc Vector2[MinorSegments + 1];
        for (int j = 0; j <= MinorSegments; j++)
        {
            float v = j * MathF.Tau / MinorSegments;
            tubeDirections[j] = new(MathF.Cos(v), MathF.Sin(v));
        }

        for (int i = 0; i <= MajorSegments; i++)
        {
            float u = i * MathF.Tau / MajorSegments;
            Vector3 direction = new(MathF.Cos(u), MathF.Sin(u), 0.0f);
            Vector3 ringCenter = direction * majorRadius;

            for (int j = 0; j <= MinorSegments; j++)
            {
                Vector2 tube = tubeDirections[j];
                Vector3 normal = (direction * tube.X) + (Vector3.UnitZ * tube.Y);

                vertices.Add(new()
                {
                    Position = ringCenter + (normal * minorRadius),
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

                indices.Add(current);
                indices.Add(next);
                indices.Add(next + 1);
                indices.Add(current);
                indices.Add(next + 1);
                indices.Add(current + 1);
            }
        }
    }
}
