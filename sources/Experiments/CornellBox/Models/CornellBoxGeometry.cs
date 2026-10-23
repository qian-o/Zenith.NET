using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Models;

internal class CornellBoxGeometry
{
    private const int SculpturePartCount = 3;
    private const int FirstCubeMesh = 5;
    private const int FirstSphereMesh = FirstCubeMesh + SculpturePartCount;

    private const int InitialVertexCapacity = 40_000;
    private const int InitialIndexCapacity = 200_000;

    private static readonly Material[] MaterialTable =
    [
        new()
        {
            Albedo = new(0.63f, 0.06f, 0.06f),
            Roughness = 0.90f
        },
        new()
        {
            Albedo = new(0.14f, 0.45f, 0.09f),
            Roughness = 0.90f
        },
        new()
        {
            Albedo = new(0.73f, 0.71f, 0.68f),
            Roughness = 0.90f
        },
        new()
        {
            Albedo = new(1.00f, 0.85f, 0.60f),
            Emission = 25.0f,
            Roughness = 0.50f
        },
        new()
        {
            Albedo = new(0.73f, 0.71f, 0.68f),
            Roughness = 0.30f
        },
        new()
        {
            Albedo = new(0.95f, 0.93f, 0.88f),
            Metallic = 1.0f,
            Roughness = 0.05f
        },
        new()
        {
            Albedo = new(0.73f, 0.71f, 0.68f),
            Roughness = 0.20f,
            Pattern = SurfacePattern.Checker,
            PatternScale = 35.0f
        },
        new()
        {
            Albedo = new(0.73f, 0.71f, 0.68f),
            Roughness = 0.90f
        },
        new()
        {
            Albedo = new(1.00f, 0.78f, 0.34f),
            Metallic = 1.0f,
            Roughness = 0.30f
        },
        new()
        {
            Albedo = new(0.95f, 0.95f, 0.95f),
            Metallic = 1.0f,
            Roughness = 0.00f
        },
        new()
        {
            Albedo = new(0.95f, 0.64f, 0.54f),
            Metallic = 1.0f,
            Roughness = 0.25f
        },
        new()
        {
            Albedo = new(0.85f, 0.85f, 0.82f),
            Roughness = 0.15f
        },
        new()
        {
            Albedo = new(0.10f, 0.25f, 0.80f),
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

    private static readonly Matrix4x4[] CubeTilts =
    [
        Matrix4x4.CreateRotationX(0.35f) * Matrix4x4.CreateRotationZ(0.25f),
        Matrix4x4.CreateRotationX(0.35f + 0.3f) * Matrix4x4.CreateRotationZ(0.25f + 0.2f),
        Matrix4x4.CreateRotationX(0.35f + (2 * 0.3f)) * Matrix4x4.CreateRotationZ(0.25f + (2 * 0.2f))
    ];

    private readonly List<Vertex> vertices = new(InitialVertexCapacity);
    private readonly List<uint> indices = new(InitialIndexCapacity);
    private readonly List<Mesh> meshes = [];

    public CornellBoxGeometry()
    {
        AddRoom();
        AddCourtyard();

        FinishMesh();

        AddSphere(Vector3.Zero, 45.0f, MaterialId.Mirror);
        FinishMesh();

        AddTorus(75.0f, 2.4f, MaterialId.Copper);
        FinishMesh();

        AddTorus(62.0f, 2.1f, MaterialId.Porcelain);
        FinishMesh();

        AddTorus(49.0f, 1.8f, MaterialId.BlueMetal);
        FinishMesh();

        ReadOnlySpan<float> cubeSizes = [35.0f, 28.0f, 22.0f];
        ReadOnlySpan<MaterialId> cubeMaterials = [MaterialId.Gold, MaterialId.Mirror, MaterialId.Copper];
        for (int i = 0; i < SculpturePartCount; i++)
        {
            Vector3 halfSize = new(cubeSizes[i]);
            AddBox(-halfSize, halfSize, cubeMaterials[i]);
            FinishMesh();
        }

        ReadOnlySpan<float> sphereRadii = [48.0f, 32.0f, 22.0f];
        ReadOnlySpan<MaterialId> sphereMaterials = [MaterialId.BlueMetal, MaterialId.Gold, MaterialId.BlueMetal];
        for (int i = 0; i < SculpturePartCount; i++)
        {
            AddSphere(Vector3.Zero, sphereRadii[i], sphereMaterials[i]);
            FinishMesh();
        }
    }

    public ReadOnlySpan<Vertex> Vertices => CollectionsMarshal.AsSpan(vertices);

    public ReadOnlySpan<uint> Indices => CollectionsMarshal.AsSpan(indices);

    public ReadOnlySpan<Material> Materials => MaterialTable;

    public ReadOnlySpan<Mesh> Meshes => CollectionsMarshal.AsSpan(meshes);

    private void FinishMesh()
    {
        uint firstIndex = meshes.Count is 0 ? 0 : meshes[^1].FirstIndex + meshes[^1].IndexCount;

        meshes.Add(new(firstIndex, (uint)indices.Count - firstIndex));
    }

    private void AddRoom()
    {
        AddQuad(new(552.8f, 0.0f, 0.0f), new(549.6f, 0.0f, 559.2f), new(556.0f, 548.8f, 559.2f), new(556.0f, 548.8f, 0.0f), MaterialId.RedWall);

        AddQuad(new(0.0f, 0.0f, 559.2f), new(0.0f, 0.0f, 0.0f), new(0.0f, 548.8f, 0.0f), new(0.0f, 548.8f, 559.2f), MaterialId.GreenWall);

        AddQuad(new(556.0f, 548.8f, 0.0f), new(556.0f, 548.8f, 559.2f), new(0.0f, 548.8f, 559.2f), new(0.0f, 548.8f, 0.0f), MaterialId.Plaster);

        AddQuad(new(552.8f, 0.0f, 0.0f), new(0.0f, 0.0f, 0.0f), new(0.0f, 0.0f, 559.2f), new(549.6f, 0.0f, 559.2f), MaterialId.Checkerboard);

        AddQuad(new(549.6f, 0.0f, 559.2f), new(0.0f, 0.0f, 559.2f), new(0.0f, 548.8f, 559.2f), new(556.0f, 548.8f, 559.2f), MaterialId.BackWall);

        AddQuad(new(130.0f, 165.0f, 65.0f), new(82.0f, 165.0f, 225.0f), new(240.0f, 165.0f, 272.0f), new(290.0f, 165.0f, 114.0f), MaterialId.SatinWhite);
        AddQuad(new(290.0f, 0.0f, 114.0f), new(290.0f, 165.0f, 114.0f), new(240.0f, 165.0f, 272.0f), new(240.0f, 0.0f, 272.0f), MaterialId.SatinWhite);
        AddQuad(new(130.0f, 0.0f, 65.0f), new(130.0f, 165.0f, 65.0f), new(290.0f, 165.0f, 114.0f), new(290.0f, 0.0f, 114.0f), MaterialId.SatinWhite);
        AddQuad(new(82.0f, 0.0f, 225.0f), new(82.0f, 165.0f, 225.0f), new(130.0f, 165.0f, 65.0f), new(130.0f, 0.0f, 65.0f), MaterialId.SatinWhite);
        AddQuad(new(240.0f, 0.0f, 272.0f), new(240.0f, 165.0f, 272.0f), new(82.0f, 165.0f, 225.0f), new(82.0f, 0.0f, 225.0f), MaterialId.SatinWhite);

        AddQuad(new(423.0f, 330.0f, 247.0f), new(265.0f, 330.0f, 296.0f), new(314.0f, 330.0f, 456.0f), new(472.0f, 330.0f, 406.0f), MaterialId.PolishedSilver);
        AddQuad(new(423.0f, 0.0f, 247.0f), new(423.0f, 330.0f, 247.0f), new(472.0f, 330.0f, 406.0f), new(472.0f, 0.0f, 406.0f), MaterialId.PolishedSilver);
        AddQuad(new(472.0f, 0.0f, 406.0f), new(472.0f, 330.0f, 406.0f), new(314.0f, 330.0f, 456.0f), new(314.0f, 0.0f, 456.0f), MaterialId.PolishedSilver);
        AddQuad(new(314.0f, 0.0f, 456.0f), new(314.0f, 330.0f, 456.0f), new(265.0f, 330.0f, 296.0f), new(265.0f, 0.0f, 296.0f), MaterialId.PolishedSilver);
        AddQuad(new(265.0f, 0.0f, 296.0f), new(265.0f, 330.0f, 296.0f), new(423.0f, 330.0f, 247.0f), new(423.0f, 0.0f, 247.0f), MaterialId.PolishedSilver);

        AddQuad(new(343.0f, 547.0f, 227.0f), new(343.0f, 547.0f, 332.0f), new(213.0f, 547.0f, 332.0f), new(213.0f, 547.0f, 227.0f), MaterialId.Light);

        AddSphere(new(185.5f, 205.0f, 169.0f), 40.0f, MaterialId.Gold);
    }

    private void AddCourtyard()
    {
        AddQuad(new(-1350.0f, -32.0f, -1350.0f), new(-1350.0f, -32.0f, 1500.0f), new(1900.0f, -32.0f, 1500.0f), new(1900.0f, -32.0f, -1350.0f), MaterialId.Paving);

        AddBox(new(-1350.0f, -55.0f, -1350.0f), new(-1290.0f, -24.0f, 1500.0f), MaterialId.Sandstone);
        AddBox(new(1840.0f, -55.0f, -1350.0f), new(1900.0f, -24.0f, 1500.0f), MaterialId.Sandstone);
        AddBox(new(-1290.0f, -55.0f, 1440.0f), new(1840.0f, -24.0f, 1500.0f), MaterialId.Sandstone);
        AddBox(new(-1290.0f, -55.0f, -1350.0f), new(1840.0f, -24.0f, -1290.0f), MaterialId.Sandstone);

        AddExhibitPaving();

        AddBox(new(-35.0f, -32.0f, -30.0f), new(590.0f, -1.0f, 590.0f), MaterialId.DarkMetal);
        AddBox(new(60.0f, -32.0f, -330.0f), new(496.0f, -24.0f, -230.0f), MaterialId.Sandstone);
        AddBox(new(60.0f, -32.0f, -230.0f), new(496.0f, -16.0f, -130.0f), MaterialId.Sandstone);
        AddBox(new(60.0f, -32.0f, -130.0f), new(496.0f, -8.0f, -30.0f), MaterialId.Sandstone);

        for (int i = 0; i < 3; i++)
        {
            float z = -330.0f + (i * 100.0f);
            float y = -24.0f + (i * 8.0f);
            AddBox(new(60.0f, y, z), new(496.0f, y + 1.0f, z + 5.0f), MaterialId.Gold);
        }

        ReadOnlySpan<float> columnPositions = [-1080.0f, 1636.0f];
        for (int i = 0; i < 4; i++)
        {
            float z = -150.0f + (i * 380.0f);

            foreach (float x in columnPositions)
            {
                AddBox(new(x - 65.0f, -32.0f, z - 65.0f), new(x + 65.0f, 0.0f, z + 65.0f), MaterialId.DarkMetal);
                AddBox(new(x - 38.0f, 0.0f, z - 38.0f), new(x + 38.0f, 620.0f, z + 38.0f), MaterialId.Sandstone);
                AddBox(new(x - 65.0f, 620.0f, z - 65.0f), new(x + 65.0f, 650.0f, z + 65.0f), MaterialId.Sandstone);
                AddBox(new(x - 51.0f, 0.0f, z - 51.0f), new(x + 51.0f, 24.0f, z + 51.0f), MaterialId.Sandstone);
                AddBox(new(x - 44.0f, 24.0f, z - 44.0f), new(x + 44.0f, 42.0f, z + 44.0f), MaterialId.Sandstone);
                AddBox(new(x - 47.0f, 584.0f, z - 47.0f), new(x + 47.0f, 604.0f, z + 47.0f), MaterialId.Sandstone);
                AddBox(new(x - 55.0f, 604.0f, z - 55.0f), new(x + 55.0f, 620.0f, z + 55.0f), MaterialId.DarkMetal);

                AddBox(new(x - 25.0f, 65.0f, z - 39.0f), new(x + 25.0f, 550.0f, z - 38.0f), MaterialId.Limestone);
                AddBox(new(x - 25.0f, 65.0f, z + 38.0f), new(x + 25.0f, 550.0f, z + 39.0f), MaterialId.Limestone);
                AddBox(new(x - 40.0f, 558.0f, z - 40.0f), new(x + 40.0f, 566.0f, z + 40.0f), MaterialId.Bronze);
            }
        }

        AddBox(new(-1150.0f, 650.0f, -220.0f), new(-1010.0f, 705.0f, 1060.0f), MaterialId.Sandstone);
        AddBox(new(1566.0f, 650.0f, -220.0f), new(1706.0f, 705.0f, 1060.0f), MaterialId.Sandstone);
        AddBox(new(-1010.0f, 650.0f, 920.0f), new(1566.0f, 705.0f, 1060.0f), MaterialId.Sandstone);
        AddBox(new(-1162.0f, 705.0f, -232.0f), new(-998.0f, 723.0f, 1072.0f), MaterialId.DarkMetal);
        AddBox(new(1554.0f, 705.0f, -232.0f), new(1718.0f, 723.0f, 1072.0f), MaterialId.DarkMetal);
        AddBox(new(-998.0f, 705.0f, 908.0f), new(1554.0f, 723.0f, 1072.0f), MaterialId.DarkMetal);

        ReadOnlySpan<MaterialId> goldMaterials = [MaterialId.PolishedGold, MaterialId.BrushedGold, MaterialId.RoughGold];
        ReadOnlySpan<MaterialId> exhibitMaterials = [MaterialId.Mirror, MaterialId.BlueMetal, MaterialId.Gold];
        for (int i = 0; i < 3; i++)
        {
            float z = -380.0f + (i * 380.0f);

            AddPlinth(new(-670.0f, z), 95.0f, 70.0f);
            AddSphere(new(-670.0f, 140.0f, z), 70.0f, goldMaterials[i]);

            AddPlinth(new(1226.0f, z), 95.0f, 70.0f);
            AddSphere(new(1226.0f, 140.0f, z), 70.0f, exhibitMaterials[i]);
        }

        AddPlinth(new(-320.0f, 840.0f), 140.0f, 45.0f);
        AddPlinth(new(876.0f, 840.0f), 140.0f, 45.0f);

        ReadOnlySpan<float> benchPositions = [-320.0f, 876.0f];
        foreach (float x in benchPositions)
        {
            AddBench(new(x, -700.0f));
        }

        ReadOnlySpan<float> planterPositionsZ = [-860.0f, 1260.0f];
        foreach (float x in columnPositions)
        {
            foreach (float z in planterPositionsZ)
            {
                AddPlanter(new(x, z));
            }
        }
    }

    private void AddExhibitPaving()
    {
        ReadOnlySpan<float> pavingCenters = [-670.0f, 1226.0f];
        foreach (float x in pavingCenters)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = -380.0f + (i * 380.0f);
                AddBox(new(x - 125.0f, -32.0f, z - 125.0f), new(x + 125.0f, -30.0f, z + 125.0f), MaterialId.Limestone);
            }
        }
    }

    private void AddBench(Vector2 center)
    {
        float x = center.X;
        float z = center.Y;
        ReadOnlySpan<float> endPositions = [-160.0f, 160.0f];
        foreach (float offset in endPositions)
        {
            AddBox(new(x - 77.0f, -32.0f, z + offset - 14.0f), new(x + 77.0f, 55.0f, z + offset + 14.0f), MaterialId.Sandstone);
        }

        AddBox(new(x - 62.0f, 35.0f, z - 170.0f), new(x + 62.0f, 54.0f, z + 170.0f), MaterialId.DarkMetal);

        ReadOnlySpan<float> fastenerPositions = [-65.0f, 65.0f];
        for (int i = 0; i < 9; i++)
        {
            float slatZ = z - 176.0f + (i * 40.0f);
            MaterialId material = (i % 3) is 1 ? MaterialId.LightWood : MaterialId.Wood;
            AddBox(new(x - 86.0f, 55.0f, slatZ), new(x + 86.0f, 70.0f, slatZ + 32.0f), material);

            foreach (float offset in fastenerPositions)
            {
                AddBox(new(x + offset - 2.0f, 70.0f, slatZ + 7.0f), new(x + offset + 2.0f, 71.0f, slatZ + 11.0f), MaterialId.Bronze);
            }
        }

        foreach (float offset in endPositions)
        {
            float armrestZ = z + offset;
            AddBox(new(x - 62.0f, 70.0f, armrestZ - 5.0f), new(x - 52.0f, 96.0f, armrestZ + 5.0f), MaterialId.DarkMetal);
            AddBox(new(x + 52.0f, 70.0f, armrestZ - 5.0f), new(x + 62.0f, 96.0f, armrestZ + 5.0f), MaterialId.DarkMetal);
            AddBox(new(x - 65.0f, 96.0f, armrestZ - 7.0f), new(x + 65.0f, 104.0f, armrestZ + 7.0f), MaterialId.Bronze);
        }
    }

    private void AddPlanter(Vector2 center)
    {
        float x = center.X;
        float z = center.Y;
        AddBox(new(x - 118.0f, -32.0f, z - 118.0f), new(x + 118.0f, -12.0f, z + 118.0f), MaterialId.DarkMetal);
        AddBox(new(x - 110.0f, -12.0f, z - 110.0f), new(x + 110.0f, 55.0f, z + 110.0f), MaterialId.Terracotta);
        AddBox(new(x - 96.0f, 55.0f, z - 96.0f), new(x + 96.0f, 58.0f, z + 96.0f), MaterialId.Soil);
        AddBox(new(x - 118.0f, 55.0f, z - 118.0f), new(x - 96.0f, 70.0f, z + 118.0f), MaterialId.Sandstone);
        AddBox(new(x + 96.0f, 55.0f, z - 118.0f), new(x + 118.0f, 70.0f, z + 118.0f), MaterialId.Sandstone);
        AddBox(new(x - 96.0f, 55.0f, z - 118.0f), new(x + 96.0f, 70.0f, z - 96.0f), MaterialId.Sandstone);
        AddBox(new(x - 96.0f, 55.0f, z + 96.0f), new(x + 96.0f, 70.0f, z + 118.0f), MaterialId.Sandstone);
        AddSphere(new(x - 42.0f, 105.0f, z - 20.0f), 60.0f, MaterialId.Sage, 12, 8);
        AddSphere(new(x + 40.0f, 100.0f, z + 28.0f), 56.0f, MaterialId.Foliage, 12, 8);
        AddSphere(new(x, 145.0f, z + 5.0f), 62.0f, MaterialId.Foliage, 12, 8);

        ReadOnlySpan<Vector2> pebbleOffsets = [new(-77.0f, -72.0f), new(-28.0f, -80.0f), new(30.0f, -78.0f), new(78.0f, -65.0f)];
        foreach (Vector2 offset in pebbleOffsets)
        {
            AddSphere(new(x + offset.X, 62.0f, z + offset.Y), 6.0f, MaterialId.Limestone, 8, 6);
        }
    }

    private void AddPlinth(Vector2 center, float halfSize, float height)
    {
        Vector3 minimum = new(center.X - halfSize, -32.0f, center.Y - halfSize);
        Vector3 maximum = new(center.X + halfSize, height, center.Y + halfSize);
        AddBox(new(minimum.X, -18.0f, minimum.Z), new(maximum.X, height - 10.0f, maximum.Z), MaterialId.DarkMetal);
        AddBox(minimum - new Vector3(8.0f, 0.0f, 8.0f), new(maximum.X + 8.0f, -18.0f, maximum.Z + 8.0f), MaterialId.Sandstone);
        AddBox(new(minimum.X - 5.0f, height - 10.0f, minimum.Z - 5.0f), new(maximum.X + 5.0f, height, maximum.Z + 5.0f), MaterialId.Sandstone);
        AddBox(new(center.X - 26.0f, height - 32.0f, minimum.Z - 1.0f), new(center.X + 26.0f, height - 17.0f, minimum.Z), MaterialId.Gold);

        ReadOnlySpan<float> cornerOffsets = [-1.0f, 1.0f];
        foreach (float x in cornerOffsets)
        {
            foreach (float z in cornerOffsets)
            {
                float cornerX = center.X + (x * (halfSize - 9.0f));
                float cornerZ = center.Y + (z * (halfSize - 9.0f));
                AddBox(new(cornerX - 2.0f, height, cornerZ - 2.0f), new(cornerX + 2.0f, height + 1.0f, cornerZ + 2.0f), MaterialId.Bronze);
            }
        }
    }

    private void AddBox(Vector3 minimum, Vector3 maximum, MaterialId materialId)
    {
        AddQuad(new(minimum.X, maximum.Y, minimum.Z), new(minimum.X, maximum.Y, maximum.Z), maximum, new(maximum.X, maximum.Y, minimum.Z), materialId);
        AddQuad(minimum, new(maximum.X, minimum.Y, minimum.Z), new(maximum.X, minimum.Y, maximum.Z), new(minimum.X, minimum.Y, maximum.Z), materialId);
        AddQuad(minimum, new(minimum.X, maximum.Y, minimum.Z), new(maximum.X, maximum.Y, minimum.Z), new(maximum.X, minimum.Y, minimum.Z), materialId);
        AddQuad(new(minimum.X, minimum.Y, maximum.Z), new(maximum.X, minimum.Y, maximum.Z), maximum, new(minimum.X, maximum.Y, maximum.Z), materialId);
        AddQuad(minimum, new(minimum.X, minimum.Y, maximum.Z), new(minimum.X, maximum.Y, maximum.Z), new(minimum.X, maximum.Y, minimum.Z), materialId);
        AddQuad(new(maximum.X, minimum.Y, minimum.Z), new(maximum.X, maximum.Y, minimum.Z), maximum, new(maximum.X, minimum.Y, maximum.Z), materialId);
    }

    private void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, MaterialId materialId)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));

        uint startIndex = (uint)vertices.Count;

        ReadOnlySpan<Vector3> positions = [v0, v1, v2, v3];
        foreach (Vector3 position in positions)
        {
            vertices.Add(new()
            {
                Position = position,
                Normal = normal,
                MaterialId = (uint)materialId
            });
        }

        indices.Add(startIndex);
        indices.Add(startIndex + 1);
        indices.Add(startIndex + 2);
        indices.Add(startIndex);
        indices.Add(startIndex + 2);
        indices.Add(startIndex + 3);
    }

    private void AddSphere(Vector3 center, float radius, MaterialId materialId, int segments = 64, int rings = 32)
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
                    MaterialId = (uint)materialId
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

    private void AddTorus(float majorRadius, float minorRadius, MaterialId materialId)
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
                    MaterialId = (uint)materialId
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

    public static void WriteTransforms(float elapsedSeconds, Span<Matrix4x4> transforms)
    {
        Vector3 sphere = new(425.0f + (75.0f * MathF.Cos(0.8f * elapsedSeconds)), 45.0f, 125.0f + (70.0f * MathF.Sin(0.8f * elapsedSeconds)));

        Matrix4x4 outer = Matrix4x4.CreateRotationY(0.6f * elapsedSeconds);
        Matrix4x4 middle = Matrix4x4.CreateRotationX(1.0f * elapsedSeconds) * outer;
        Matrix4x4 inner = Matrix4x4.CreateRotationY(1.6f * elapsedSeconds) * middle;
        Matrix4x4 gyroscope = Matrix4x4.CreateTranslation(185.5f, 350.0f, 169.0f);

        transforms[0] = Matrix4x4.Identity;
        transforms[1] = Matrix4x4.CreateTranslation(sphere);
        transforms[2] = outer * gyroscope;
        transforms[3] = middle * gyroscope;
        transforms[4] = inner * gyroscope;

        Matrix4x4 cubeOrbit = Matrix4x4.CreateRotationY(0.18f * elapsedSeconds);
        Matrix4x4 sphereOrbit = Matrix4x4.CreateRotationY(-0.14f * elapsedSeconds);
        ReadOnlySpan<Vector3> cubeOffsets = [new(-60.0f, 125.0f, -15.0f), new(45.0f, 220.0f, 20.0f), new(-10.0f, 310.0f, -5.0f)];
        ReadOnlySpan<Vector3> sphereOffsets = [new(-45.0f, 125.0f, -10.0f), new(55.0f, 220.0f, 15.0f), new(5.0f, 300.0f, -5.0f)];
        for (int i = 0; i < SculpturePartCount; i++)
        {
            float phase = i * 1.7f;
            float cubeBob = 8.0f * MathF.Sin((0.55f * elapsedSeconds) + phase);
            Vector3 cubePosition = Vector3.Transform(cubeOffsets[i], cubeOrbit) + new Vector3(-320.0f, cubeBob, 840.0f);
            Matrix4x4 cubeRotation = CubeTilts[i] * Matrix4x4.CreateRotationY((0.24f * elapsedSeconds) + phase);
            transforms[FirstCubeMesh + i] = cubeRotation * Matrix4x4.CreateTranslation(cubePosition);

            float sphereBob = 7.0f * MathF.Sin((0.45f * elapsedSeconds) + phase + 0.8f);
            Vector3 spherePosition = Vector3.Transform(sphereOffsets[i], sphereOrbit) + new Vector3(876.0f, sphereBob, 840.0f);
            transforms[FirstSphereMesh + i] = Matrix4x4.CreateTranslation(spherePosition);
        }
    }

    private enum MaterialId : uint
    {
        RedWall,

        GreenWall,

        Plaster,

        Light,

        SatinWhite,

        PolishedSilver,

        Checkerboard,

        BackWall,

        Gold,

        Mirror,

        Copper,

        Porcelain,

        BlueMetal,

        Paving,

        Sandstone,

        DarkMetal,

        PolishedGold,

        BrushedGold,

        RoughGold,

        Wood,

        Foliage,

        Soil,

        Limestone,

        LightWood,

        Sage,

        Bronze,

        Terracotta
    }
}
