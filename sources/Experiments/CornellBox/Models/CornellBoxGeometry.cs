using System.Numerics;

namespace CornellBox.Models;

internal static class CornellBoxGeometry
{
    private const int DynamicTrefoilSlot = 6;
    private const int DynamicRingOuterSlot = 7;
    private const int DynamicRingMiddleSlot = 8;
    private const int DynamicRingInnerSlot = 9;
    private const int DynamicFloorOrbSlot = 10;
    private const int FirstPendulumSlot = 11;
    private const int PendulumCount = 5;
    private const int DynamicLeftSculptureSlot = FirstPendulumSlot + PendulumCount;

    public static void CreateScene(out SceneVertex[] vertices,
                                   out uint[] indices,
                                   out (uint FirstIndex, uint IndexCount)[] meshes,
                                   out (int MeshIndex, bool IsDynamic, uint MaterialOverride, byte VisibilityMask)[] placements,
                                   out SceneMaterial[] materials)
    {
        MeshBuilder builder = new();

        BuildSanctuaryShell(builder);
        int sanctuaryShellMesh = builder.CommitMesh();

        BuildSanctuaryStaticExhibits(builder);
        int sanctuaryExhibitsMesh = builder.CommitMesh();

        builder.AddSphere(new(-168.0f, 190.0f, 195.0f), 48.0f, 12, 52, 26);
        AddSculptedUrn(builder, new(-548.0f, 87.2f, 425.0f), 1.10f, 13);
        int glassExhibitsMesh = builder.CommitMesh();

        BuildCourtyardGroundAndPools(builder);
        int courtyardBaseMesh = builder.CommitMesh();

        BuildClassicalColonnades(builder);
        int colonnadeMesh = builder.CommitMesh();

        BuildFlankingGalleries(builder);
        int galleriesMesh = builder.CommitMesh();

        builder.AddTorusKnot(2, 3, 27.0f, 11.5f, 6.2f, 6, 144, 20);
        int trefoilKnotMesh = builder.CommitMesh();

        builder.AddTorus(82.0f, 3.0f, 7, 68, 16);
        int outerRingMesh = builder.CommitMesh();

        builder.AddTorus(69.0f, 2.6f, 8, 64, 14);
        int middleRingMesh = builder.CommitMesh();

        builder.AddTorus(56.0f, 2.4f, 9, 60, 14);
        int innerRingMesh = builder.CommitMesh();

        builder.AddSphere(Vector3.Zero, 32.0f, 7, 44, 22);
        int mirrorOrbMesh = builder.CommitMesh();

        builder.AddSphere(Vector3.Zero, 6.8f, 6, 20, 12);
        builder.AddCylinderY(new(0.0f, -165.0f, 0.0f), 1.8f, 159.0f, 4, 16);
        builder.AddSphere(new(0.0f, -180.0f, 0.0f), 16.5f, 6, 36, 18);
        int pendulumMesh = builder.CommitMesh();

        builder.AddTorusKnot(2, 3, 26.0f, 11.0f, 6.2f, 10, 128, 18);
        int rubyKnotMesh = builder.CommitMesh();

        (vertices, indices, meshes) = builder.ToArrays();

        List<(int MeshIndex, bool IsDynamic, uint MaterialOverride, byte VisibilityMask)> placementList =
        [
            (sanctuaryShellMesh, false, uint.MaxValue, 0x01),
            (sanctuaryExhibitsMesh, false, uint.MaxValue, 0x01),
            (glassExhibitsMesh, false, uint.MaxValue, 0x02),
            (courtyardBaseMesh, false, uint.MaxValue, 0x01),
            (colonnadeMesh, false, uint.MaxValue, 0x01),
            (galleriesMesh, false, uint.MaxValue, 0x01),
            (trefoilKnotMesh, true, uint.MaxValue, 0x01),
            (outerRingMesh, true, uint.MaxValue, 0x01),
            (middleRingMesh, true, uint.MaxValue, 0x01),
            (innerRingMesh, true, uint.MaxValue, 0x01),
            (mirrorOrbMesh, true, uint.MaxValue, 0x01)
        ];

        for (int i = 0; i < PendulumCount; i++)
        {
            uint materialOverride = (i % 2) is 0 ? uint.MaxValue : 7u;
            placementList.Add((pendulumMesh, true, materialOverride, 0x01));
        }

        placementList.Add((rubyKnotMesh, true, uint.MaxValue, 0x01));
        placements = [.. placementList];

        materials =
        [
            SceneMaterial.Coated(new(0.82f, 0.79f, 0.73f), 0.20f, 0.70f, 0.055f, SceneMaterial.Pattern.Checker, 58.0f),
            SceneMaterial.Matte(new(0.82f, 0.78f, 0.72f), 0.78f),
            SceneMaterial.Matte(new(0.66f, 0.062f, 0.055f), 0.86f),
            SceneMaterial.Matte(new(0.10f, 0.44f, 0.12f), 0.86f),
            SceneMaterial.Metal(new(0.15f, 0.14f, 0.13f), 0.24f),
            SceneMaterial.Coated(new(0.40f, 0.22f, 0.10f), 0.44f, 0.35f, 0.14f, SceneMaterial.Pattern.Stripes, 38.0f),
            SceneMaterial.Metal(new(0.98f, 0.78f, 0.35f), 0.12f),
            SceneMaterial.Metal(new(0.96f, 0.97f, 0.98f), 0.035f),
            SceneMaterial.Metal(new(0.96f, 0.56f, 0.40f), 0.11f),
            SceneMaterial.Metal(new(0.18f, 0.40f, 0.92f), 0.12f),
            SceneMaterial.Coated(new(0.62f, 0.025f, 0.045f), 0.20f, 1.0f, 0.035f, metallic: 0.30f),
            SceneMaterial.Coated(new(0.032f, 0.10f, 0.44f), 0.18f, 1.0f, 0.035f),
            SceneMaterial.Glass(new(0.96f, 0.99f, 1.00f), 1.52f, 0.02f),
            SceneMaterial.Glass(new(0.38f, 0.92f, 0.66f), 1.50f, 0.025f),
            SceneMaterial.Emissive(new(1.00f, 0.89f, 0.70f), 22.0f),
            SceneMaterial.Coated(new(0.055f, 0.058f, 0.065f), 0.18f, 0.85f, 0.04f, SceneMaterial.Pattern.Marble, 68.0f),
            SceneMaterial.Coated(new(0.84f, 0.80f, 0.73f), 0.22f, 0.88f, 0.045f, SceneMaterial.Pattern.Marble, 92.0f),
            SceneMaterial.Matte(new(0.60f, 0.56f, 0.49f), 0.60f, SceneMaterial.Pattern.Grid, 128.0f),
            SceneMaterial.Matte(new(0.77f, 0.72f, 0.63f), 0.66f),
            SceneMaterial.Matte(new(0.56f, 0.25f, 0.14f), 0.74f),
            SceneMaterial.Matte(new(0.12f, 0.28f, 0.11f), 0.88f),
            SceneMaterial.Coated(new(0.025f, 0.072f, 0.082f), 0.06f, 1.0f, 0.035f)
        ];
    }

    public static void WriteTransforms(float elapsedSeconds, Span<Matrix4x4> transforms)
    {
        for (int i = 0; i < DynamicTrefoilSlot; i++)
        {
            transforms[i] = Matrix4x4.Identity;
        }

        float armillaryBob = 4.0f * MathF.Sin(elapsedSeconds * 1.10f);
        Matrix4x4 ringTranslation = Matrix4x4.CreateTranslation(new(0.0f, 250.0f + armillaryBob, 305.0f));

        transforms[DynamicTrefoilSlot] = Matrix4x4.CreateRotationX(0.30f * MathF.Sin(elapsedSeconds * 0.72f)) * Matrix4x4.CreateRotationY(elapsedSeconds * 0.85f) * ringTranslation;

        Matrix4x4 outerRotation = Matrix4x4.CreateRotationY(elapsedSeconds * 0.62f) * Matrix4x4.CreateRotationZ(0.22f);
        Matrix4x4 middleRotation = Matrix4x4.CreateRotationX(elapsedSeconds * 1.02f) * outerRotation;
        Matrix4x4 innerRotation = Matrix4x4.CreateRotationY(elapsedSeconds * 1.54f) * middleRotation;

        transforms[DynamicRingOuterSlot] = outerRotation * ringTranslation;
        transforms[DynamicRingMiddleSlot] = middleRotation * ringTranslation;
        transforms[DynamicRingInnerSlot] = innerRotation * ringTranslation;

        transforms[DynamicFloorOrbSlot] = Matrix4x4.CreateTranslation(new(38.0f + (72.0f * MathF.Cos(elapsedSeconds * 0.78f)), 32.0f, 108.0f + (28.0f * MathF.Sin(elapsedSeconds * 0.78f))));

        for (int i = 0; i < PendulumCount; i++)
        {
            float z = -4.0f + (i * 42.0f);
            float frequency = 1.95f + (i * 0.13f);
            float swing = 0.145f * MathF.Sin(elapsedSeconds * frequency);
            transforms[FirstPendulumSlot + i] = Matrix4x4.CreateRotationZ(swing) * Matrix4x4.CreateTranslation(-548.0f, 274.0f, z);
        }

        float leftBob = 4.5f * MathF.Sin((elapsedSeconds * 1.05f) + 1.2f);
        transforms[DynamicLeftSculptureSlot] = Matrix4x4.CreateRotationZ(0.22f) * Matrix4x4.CreateRotationY(-elapsedSeconds * 0.72f) * Matrix4x4.CreateTranslation(548.0f, 148.0f + leftBob, 185.0f);
    }

    private static void BuildSanctuaryShell(MeshBuilder builder)
    {
        builder.AddQuad(new(290.0f, 0.0f, 1.0f), new(-290.0f, 0.0f, 1.0f), new(-290.0f, 0.0f, 560.0f), new(290.0f, 0.0f, 560.0f), 0);
        builder.AddRoundedBox(new(0.0f, 0.55f, -0.5f), new(288.5f, 0.55f, 2.0f), 0.25f, 0.0f, 6);

        builder.AddQuad(new(290.0f, 0.0f, 1.0f), new(290.0f, 0.0f, 560.0f), new(290.0f, 560.0f, 560.0f), new(290.0f, 560.0f, 1.0f), 2);
        builder.AddBox(new(290.2f, 0.0f, 4.0f), new(324.0f, 560.0f, 588.0f), 18);

        builder.AddQuad(new(-290.0f, 0.0f, 560.0f), new(-290.0f, 0.0f, 1.0f), new(-290.0f, 560.0f, 1.0f), new(-290.0f, 560.0f, 560.0f), 3);
        builder.AddBox(new(-324.0f, 0.0f, 4.0f), new(-290.2f, 560.0f, 588.0f), 18);

        builder.AddQuad(new(290.0f, 0.0f, 560.0f), new(130.0f, 0.0f, 560.0f), new(130.0f, 560.0f, 560.0f), new(290.0f, 560.0f, 560.0f), 1);
        builder.AddQuad(new(-130.0f, 0.0f, 560.0f), new(-290.0f, 0.0f, 560.0f), new(-290.0f, 560.0f, 560.0f), new(-130.0f, 560.0f, 560.0f), 1);
        builder.AddQuad(new(130.0f, 0.0f, 560.0f), new(-130.0f, 0.0f, 560.0f), new(-130.0f, 64.0f, 560.0f), new(130.0f, 64.0f, 560.0f), 1);
        builder.AddQuad(new(130.0f, 456.0f, 560.0f), new(-130.0f, 456.0f, 560.0f), new(-130.0f, 560.0f, 560.0f), new(130.0f, 560.0f, 560.0f), 1);

        builder.AddBox(new(130.0f, 0.0f, 560.2f), new(290.2f, 560.0f, 588.0f), 18);
        builder.AddBox(new(-290.2f, 0.0f, 560.2f), new(-130.0f, 560.0f, 588.0f), 18);
        builder.AddBox(new(-130.0f, 0.0f, 560.2f), new(130.0f, 64.0f, 588.0f), 18);
        builder.AddBox(new(-130.0f, 456.0f, 560.2f), new(130.0f, 560.0f, 588.0f), 18);

        builder.AddRoundedBox(new(0.0f, 453.0f, 574.0f), new(130.0f, 3.0f, 15.5f), 1.0f, 0.0f, 6);
        builder.AddRoundedBox(new(0.0f, 67.0f, 574.0f), new(130.0f, 3.0f, 15.5f), 1.0f, 0.0f, 6);
        builder.AddRoundedBox(new(127.0f, 260.0f, 574.0f), new(3.0f, 190.0f, 15.5f), 1.0f, 0.0f, 6);
        builder.AddRoundedBox(new(-127.0f, 260.0f, 574.0f), new(3.0f, 190.0f, 15.5f), 1.0f, 0.0f, 6);

        for (int i = -5; i <= 5; i++)
        {
            builder.AddRoundedBox(new(i * 20.5f, 260.0f, 574.0f), new(3.4f, 190.0f, 8.5f), 1.2f, 0.34f, 5);
        }

        builder.AddRoundedBox(new(308.5f, 280.0f, -7.0f), new(20.0f, 280.0f, 11.0f), 2.5f, 0.0f, 4);
        builder.AddRoundedBox(new(-308.5f, 280.0f, -7.0f), new(20.0f, 280.0f, 11.0f), 2.5f, 0.0f, 4);
        builder.AddRoundedBox(new(308.5f, 280.0f, 595.0f), new(20.0f, 280.0f, 7.0f), 2.5f, 0.0f, 4);
        builder.AddRoundedBox(new(-308.5f, 280.0f, 595.0f), new(20.0f, 280.0f, 7.0f), 2.5f, 0.0f, 4);

        builder.AddQuad(new(290.0f, 560.0f, 1.0f), new(290.0f, 560.0f, 560.0f), new(-290.0f, 560.0f, 560.0f), new(-290.0f, 560.0f, 1.0f), 1);
        builder.AddRoundedBox(new(0.0f, 576.1f, 290.0f), new(332.0f, 15.9f, 312.0f), 3.0f, 0.0f, 4);
        builder.AddRoundedBox(new(0.0f, 601.0f, 290.0f), new(346.0f, 9.0f, 322.0f), 2.5f, 0.0f, 18);

        builder.AddRoundedBox(new(98.0f, 556.0f, 275.0f), new(5.0f, 4.0f, 93.0f), 1.2f, 0.0f, 4);
        builder.AddRoundedBox(new(-98.0f, 556.0f, 275.0f), new(5.0f, 4.0f, 93.0f), 1.2f, 0.0f, 4);
        builder.AddRoundedBox(new(0.0f, 556.0f, 187.0f), new(93.0f, 4.0f, 5.0f), 1.2f, 0.0f, 4);
        builder.AddRoundedBox(new(0.0f, 556.0f, 363.0f), new(93.0f, 4.0f, 5.0f), 1.2f, 0.0f, 4);
        builder.AddQuad(new(93.0f, 554.0f, 192.0f), new(93.0f, 554.0f, 358.0f), new(-93.0f, 554.0f, 358.0f), new(-93.0f, 554.0f, 192.0f), 14);
    }

    private static void BuildSanctuaryStaticExhibits(MeshBuilder builder)
    {
        builder.AddRoundedBox(new(172.0f, 4.0f, 392.0f), new(53.0f, 4.0f, 53.0f), 1.5f, -0.30f, 6);
        builder.AddRoundedBox(new(172.0f, 169.0f, 392.0f), new(50.0f, 161.0f, 50.0f), 6.0f, -0.30f, 16);

        builder.AddRoundedBox(new(-168.0f, 68.0f, 195.0f), new(52.0f, 68.0f, 52.0f), 5.5f, 0.28f, 1);
        ReadOnlySpan<Vector2> sphereCradleProfile =
        [
            new(32.0f, 0.0f),
            new(33.5f, 3.0f),
            new(30.5f, 8.0f),
            new(26.5f, 11.6f),
            new(22.6f, 11.4f),
            new(15.0f, 8.2f),
            new(7.5f, 6.3f),
            new(0.0f, 5.75f)
        ];
        builder.AddLathe(new(-168.0f, 136.0f, 195.0f), sphereCradleProfile, 6, 44, true, false);

        ReadOnlySpan<Vector2> daisProfile =
        [
            new(78.0f, 0.0f),
            new(78.0f, 9.0f),
            new(68.0f, 15.0f),
            new(56.0f, 22.0f),
            new(53.0f, 48.0f),
            new(62.0f, 55.0f),
            new(64.0f, 60.0f)
        ];
        builder.AddLathe(new(0.0f, 0.0f, 305.0f), daisProfile, 15, 48, true, false);
        builder.AddLathe(new(0.0f, 60.0f, 305.0f), [new(64.0f, 0.0f), new(66.5f, 3.0f), new(60.0f, 6.0f)], 6, 48, false, true);

        ReadOnlySpan<Vector2> finialProfile =
        [
            new(20.0f, 0.0f),
            new(14.0f, 8.0f),
            new(6.5f, 28.0f),
            new(5.0f, 52.0f),
            new(11.0f, 66.0f),
            new(16.5f, 72.0f),
            new(12.0f, 72.0f),
            new(0.0f, 67.0f)
        ];
        builder.AddLathe(new(0.0f, 66.0f, 305.0f), finialProfile, 6, 36, false, false);
    }

    private static void BuildCourtyardGroundAndPools(MeshBuilder builder)
    {
        builder.AddQuad(new(1240.0f, -27.0f, -1260.0f), new(-1240.0f, -27.0f, -1260.0f), new(-1240.0f, -27.0f, 1080.0f), new(1240.0f, -27.0f, 1080.0f), 17);
        builder.AddBox(new(-1244.0f, -31.0f, -1264.0f), new(1244.0f, -27.02f, 1084.0f), 4);

        builder.AddRoundedBox(new(0.0f, -14.5f, 292.0f), new(342.0f, 12.5f, 316.0f), 2.5f, 0.0f, 4);
        builder.AddBox(new(-338.0f, -2.0f, -22.0f), new(338.0f, 0.0f, 2.0f), 18);
        builder.AddBox(new(-338.0f, -2.0f, 588.0f), new(338.0f, 0.0f, 606.0f), 18);
        builder.AddBox(new(324.0f, -2.0f, 2.0f), new(338.0f, 0.0f, 588.0f), 18);
        builder.AddBox(new(-338.0f, -2.0f, 2.0f), new(-324.0f, 0.0f, 588.0f), 18);

        for (int i = 0; i < 3; i++)
        {
            float yTop = -20.25f + (i * 6.75f);
            float zFront = -178.0f + (i * 52.0f);
            float zBack = zFront + 52.0f;
            float halfWidth = 264.0f - (i * 12.0f);
            float yCenter = (-27.0f + yTop) * 0.5f;
            float yHalf = (yTop + 27.0f) * 0.5f;

            builder.AddRoundedBox(new(0.0f, yCenter, (zFront + zBack) * 0.5f), new(halfWidth, yHalf, 26.0f), 1.6f, 0.0f, 18);
            builder.AddRoundedBox(new(0.0f, yTop + 0.45f, zFront + 2.2f), new(halfWidth - 3.0f, 0.45f, 1.8f), 0.3f, 0.0f, 6);
        }

        foreach (float sign in (ReadOnlySpan<float>)[1.0f, -1.0f])
        {
            builder.AddBox(new((sign * 254.0f) - 10.0f, -27.0f, -1240.0f), new((sign * 254.0f) + 10.0f, -26.3f, -182.0f), 15);
            builder.AddBox(new((sign * 241.0f) - 2.0f, -27.0f, -1240.0f), new((sign * 241.0f) + 2.0f, -26.2f, -182.0f), 6);
        }

        AddFloorMedallion(builder, new(0.0f, -27.0f, -275.0f), 80.0f);

        foreach (float sign in (ReadOnlySpan<float>)[1.0f, -1.0f])
        {
            float poolX = sign * 408.0f;
            builder.AddQuad(new(poolX + 38.0f, -22.5f, -182.0f), new(poolX - 38.0f, -22.5f, -182.0f), new(poolX - 38.0f, -22.5f, 582.0f), new(poolX + 38.0f, -22.5f, 582.0f), 21);

            builder.AddRoundedBox(new(poolX - 42.0f, -23.5f, 200.0f), new(4.0f, 3.5f, 390.0f), 1.0f, 0.0f, 4);
            builder.AddRoundedBox(new(poolX + 42.0f, -23.5f, 200.0f), new(4.0f, 3.5f, 390.0f), 1.0f, 0.0f, 4);
            builder.AddRoundedBox(new(poolX, -23.5f, -186.0f), new(38.0f, 3.5f, 4.0f), 1.0f, 0.0f, 4);
            builder.AddRoundedBox(new(poolX, -23.5f, 586.0f), new(38.0f, 3.5f, 4.0f), 1.0f, 0.0f, 4);
        }

        foreach (float sign in (ReadOnlySpan<float>)[1.0f, -1.0f])
        {
            float x = sign * 548.0f;
            AddCourtyardBench(builder, new(x, -340.0f));

            builder.AddQuad(new(x + 56.0f, -22.5f, -842.0f), new(x - 56.0f, -22.5f, -842.0f), new(x - 56.0f, -22.5f, -538.0f), new(x + 56.0f, -22.5f, -538.0f), 21);

            builder.AddRoundedBox(new(x - 60.0f, -23.5f, -690.0f), new(4.0f, 3.5f, 160.0f), 1.0f, 0.0f, 4);
            builder.AddRoundedBox(new(x + 60.0f, -23.5f, -690.0f), new(4.0f, 3.5f, 160.0f), 1.0f, 0.0f, 4);
            builder.AddRoundedBox(new(x, -23.5f, -846.0f), new(56.0f, 3.5f, 4.0f), 1.0f, 0.0f, 4);
            builder.AddRoundedBox(new(x, -23.5f, -534.0f), new(56.0f, 3.5f, 4.0f), 1.0f, 0.0f, 4);

            AddTurnedPedestal(builder, new(x, -27.0f, -690.0f), 28.0f, 42.0f);
            builder.AddSphere(new(x, 33.0f, -690.0f), 18.0f, sign > 0 ? 6u : 8u, 36, 18);

            AddTerracottaPlanter(builder, new(x, -965.0f));
        }

        foreach (float sign in (ReadOnlySpan<float>)[1.0f, -1.0f])
        {
            float px = sign * 965.0f;
            float innerX = sign * 825.0f;
            float outerX = sign * 1105.0f;

            builder.AddBox(new(innerX - 8.0f, -27.0f, -1052.0f), new(innerX + 8.0f, -26.3f, 932.0f), 15);
            builder.AddBox(new(outerX - 8.0f, -27.0f, -1052.0f), new(outerX + 8.0f, -26.3f, 932.0f), 15);

            float minEdgeX = MathF.Min(innerX, outerX) - 8.0f;
            float maxEdgeX = MathF.Max(innerX, outerX) + 8.0f;
            builder.AddBox(new(minEdgeX, -27.0f, -1068.0f), new(maxEdgeX, -26.3f, -1052.0f), 15);
            builder.AddBox(new(minEdgeX, -27.0f, 932.0f), new(maxEdgeX, -26.3f, 948.0f), 15);

            foreach (float pz in (ReadOnlySpan<float>)[-680.0f, -240.0f, 200.0f, 640.0f])
            {
                AddTerracottaPlanter(builder, new(px, pz));
            }

            foreach (float bz in (ReadOnlySpan<float>)[-460.0f, -20.0f, 420.0f])
            {
                AddCourtyardBench(builder, new(px, bz));
            }

            AddTurnedPedestal(builder, new(px, -27.0f, -950.0f), 46.0f, 96.0f);
            AddSculptedUrn(builder, new(px, 69.0f, -950.0f), 1.04f, sign > 0 ? 8u : 11u);

            AddTurnedPedestal(builder, new(px, -27.0f, 845.0f), 46.0f, 96.0f);
            builder.AddSphere(new(px, 98.0f, 845.0f), 29.0f, sign > 0 ? 6u : 7u, 36, 18);
        }

        foreach (float cx in (ReadOnlySpan<float>)[-680.0f, -250.0f, 250.0f, 680.0f])
        {
            AddTerracottaPlanter(builder, new(cx, 910.0f));
        }

        AddTurnedPedestal(builder, new(0.0f, -27.0f, 815.0f), 52.0f, 96.0f);
        builder.AddLathe(new(0.0f, -27.0f, 815.0f), [new(60.0f, 0.0f), new(60.0f, 0.55f), new(82.0f, 0.55f), new(82.0f, 0.0f)], 6, 48, false, false);
        builder.AddSphere(new(0.0f, 101.0f, 815.0f), 32.0f, 8, 40, 20);

        BuildPerimeterWallsAndPortico(builder);
    }

    private static void BuildPerimeterWallsAndPortico(MeshBuilder builder)
    {
        const float WallHalfThickness = 18.0f;
        const float CopingHalfThickness = 23.0f;
        const float PierHalfSize = 28.0f;

        ReadOnlySpan<float> northPiersX = [-1180.0f, -710.0f, -240.0f, 240.0f, 710.0f, 1180.0f];
        foreach (float px in northPiersX)
        {
            AddBalustradePier(builder, new(px, -27.0f, 1040.0f), PierHalfSize);
        }

        for (int i = 0; i < northPiersX.Length - 1; i++)
        {
            AddWallSpanX(builder, 1040.0f, northPiersX[i] + PierHalfSize, northPiersX[i + 1] - PierHalfSize);
        }

        ReadOnlySpan<float> sidePiersZ = [-1190.0f, -750.0f, -310.0f, 130.0f, 570.0f, 1040.0f];
        foreach (float sign in (ReadOnlySpan<float>)[1.0f, -1.0f])
        {
            float xWall = sign * 1180.0f;
            for (int i = 0; i < sidePiersZ.Length - 1; i++)
            {
                AddBalustradePier(builder, new(xWall, -27.0f, sidePiersZ[i]), PierHalfSize);
                AddWallSpanZ(builder, xWall, sidePiersZ[i] + PierHalfSize, sidePiersZ[i + 1] - PierHalfSize);
            }
        }

        foreach (float sign in (ReadOnlySpan<float>)[1.0f, -1.0f])
        {
            AddBalustradePier(builder, new(sign * 475.0f, -27.0f, -1190.0f), PierHalfSize);
            AddBalustradePier(builder, new(sign * 825.0f, -27.0f, -1190.0f), PierHalfSize);

            AddWallSpanX(builder, -1190.0f, MathF.Min(sign * 475.0f, sign * 825.0f) + PierHalfSize, MathF.Max(sign * 475.0f, sign * 825.0f) - PierHalfSize);
            AddWallSpanX(builder, -1190.0f, MathF.Min(sign * 825.0f, sign * 1180.0f) + PierHalfSize, MathF.Max(sign * 825.0f, sign * 1180.0f) - PierHalfSize);
        }

        foreach (float cx in (ReadOnlySpan<float>)[-390.0f, -155.0f, 155.0f, 390.0f])
        {
            AddClassicalColumn(builder, new(cx, -27.0f, -1190.0f));
        }

        builder.AddRoundedBox(new(0.0f, 576.0f, -1190.0f), new(448.0f, 16.0f, 54.0f), 3.0f, 0.0f, 18);
        builder.AddRoundedBox(new(0.0f, 598.0f, -1190.0f), new(458.0f, 6.0f, 62.0f), 2.0f, 0.0f, 4);

        static void AddWallSpanX(MeshBuilder meshBuilder, float zCenter, float xStart, float xEnd)
        {
            float spanHalf = (xEnd - xStart) * 0.5f;
            float xMid = (xStart + xEnd) * 0.5f;

            meshBuilder.AddRoundedBox(new(xMid, 4.5f, zCenter), new(spanHalf, 31.5f, WallHalfThickness), 2.5f, 0.0f, 18);
            meshBuilder.AddRoundedBox(new(xMid, 39.5f, zCenter), new(spanHalf, 3.5f, CopingHalfThickness), 1.5f, 0.0f, 4);
        }

        static void AddWallSpanZ(MeshBuilder meshBuilder, float xCenter, float zStart, float zEnd)
        {
            float spanHalf = (zEnd - zStart) * 0.5f;
            float zMid = (zStart + zEnd) * 0.5f;

            meshBuilder.AddRoundedBox(new(xCenter, 4.5f, zMid), new(WallHalfThickness, 31.5f, spanHalf), 2.5f, 0.0f, 18);
            meshBuilder.AddRoundedBox(new(xCenter, 39.5f, zMid), new(CopingHalfThickness, 3.5f, spanHalf), 1.5f, 0.0f, 4);
        }
    }

    private static void BuildClassicalColonnades(MeshBuilder builder)
    {
        ReadOnlySpan<float> sideX = [720.0f, -720.0f];
        for (int i = 0; i < 4; i++)
        {
            float z = -16.0f + (i * 200.0f);
            foreach (float x in sideX)
            {
                AddClassicalColumn(builder, new(x, -27.0f, z));
            }
        }

        foreach (float x in sideX)
        {
            builder.AddRoundedBox(new(x, 576.0f, 284.0f), new(54.0f, 16.0f, 356.0f), 3.0f, 0.0f, 18);
            builder.AddRoundedBox(new(x, 598.0f, 284.0f), new(62.0f, 6.0f, 364.0f), 2.0f, 0.0f, 4);
        }

        for (int i = 0; i <= 10; i++)
        {
            float z = -16.0f + (i * 60.0f);
            builder.AddRoundedBox(new(499.0f, 578.0f, z), new(167.0f, 10.0f, 7.0f), 1.8f, 0.0f, 18);
            builder.AddRoundedBox(new(-499.0f, 578.0f, z), new(167.0f, 10.0f, 7.0f), 1.8f, 0.0f, 18);
        }
    }

    private static void BuildFlankingGalleries(MeshBuilder builder)
    {
        builder.AddRoundedBox(new(548.0f, -21.0f, 200.0f), new(76.0f, 6.0f, 390.0f), 2.0f, 0.0f, 1);
        builder.AddRoundedBox(new(-548.0f, -21.0f, 200.0f), new(76.0f, 6.0f, 390.0f), 2.0f, 0.0f, 1);

        AddTurnedPedestal(builder, new(548.0f, -15.0f, -55.0f), 46.0f, 102.0f);
        AddSculptedUrn(builder, new(548.0f, 87.0f, -55.0f), 1.10f, 11);

        AddTurnedPedestal(builder, new(548.0f, -15.0f, 185.0f), 48.0f, 92.0f);
        builder.AddLathe(new(548.0f, 77.0f, 185.0f), [new(24.0f, 0.0f), new(26.0f, 3.5f), new(18.0f, 6.0f), new(0.0f, 3.5f)], 6, 36, false, false);

        AddTurnedPedestal(builder, new(548.0f, -15.0f, 425.0f), 44.0f, 98.0f);
        AddSculptedUrn(builder, new(548.0f, 83.0f, 425.0f), 1.02f, 8);

        builder.AddRoundedBox(new(-548.0f, -2.0f, 80.0f), new(68.0f, 13.0f, 142.0f), 3.0f, 0.0f, 15);

        foreach (float px in (ReadOnlySpan<float>)[-600.0f, -496.0f])
        {
            foreach (float pz in (ReadOnlySpan<float>)[-44.0f, 204.0f])
            {
                builder.AddRoundedBox(new(px, 139.0f, pz), new(8.0f, 128.0f, 8.0f), 2.0f, 0.0f, 4);
            }
        }

        builder.AddRoundedBox(new(-548.0f, 274.0f, -44.0f), new(62.0f, 7.0f, 9.5f), 2.0f, 0.0f, 4);
        builder.AddRoundedBox(new(-548.0f, 274.0f, 204.0f), new(62.0f, 7.0f, 9.5f), 2.0f, 0.0f, 4);
        builder.AddRoundedBox(new(-600.0f, 274.0f, 80.0f), new(4.5f, 4.5f, 114.5f), 1.4f, 0.0f, 4);
        builder.AddRoundedBox(new(-496.0f, 274.0f, 80.0f), new(4.5f, 4.5f, 114.5f), 1.4f, 0.0f, 4);
        builder.AddRoundedBox(new(-548.0f, 274.0f, 80.0f), new(4.2f, 4.2f, 114.5f), 1.5f, 0.0f, 6);

        AddTurnedPedestal(builder, new(-548.0f, -15.0f, 425.0f), 46.0f, 102.0f);
    }

    private static void AddFloorMedallion(MeshBuilder builder, Vector3 baseCenter, float outerRadius)
    {
        builder.AddLathe(baseCenter, [new(outerRadius, 0.0f), new(outerRadius, 0.40f)], 15, 48, false, true);
        builder.AddLathe(baseCenter + new Vector3(0.0f, 0.40f, 0.0f), [new(outerRadius * 0.85f, 0.0f), new(outerRadius * 0.85f, 0.30f)], 6, 48, false, true);
        builder.AddLathe(baseCenter + new Vector3(0.0f, 0.70f, 0.0f), [new(outerRadius * 0.78f, 0.0f), new(outerRadius * 0.78f, 0.30f)], 18, 48, false, true);
        builder.AddLathe(baseCenter + new Vector3(0.0f, 1.00f, 0.0f), [new(outerRadius * 0.36f, 0.0f), new(outerRadius * 0.36f, 0.30f)], 6, 48, false, true);
        builder.AddLathe(baseCenter + new Vector3(0.0f, 1.30f, 0.0f), [new(outerRadius * 0.28f, 0.0f), new(outerRadius * 0.28f, 0.30f)], 15, 48, false, true);
    }

    private static void AddBalustradePier(MeshBuilder builder, Vector3 baseCenter, float halfSize)
    {
        builder.AddRoundedBox(baseCenter + new Vector3(0.0f, 40.0f, 0.0f), new(halfSize, 40.0f, halfSize), 2.5f, 0.0f, 18);
        builder.AddRoundedBox(baseCenter + new Vector3(0.0f, 83.0f, 0.0f), new(halfSize + 3.0f, 3.0f, halfSize + 3.0f), 1.0f, 0.0f, 4);
        builder.AddLathe(baseCenter + new Vector3(0.0f, 86.0f, 0.0f), [new(12.0f, 0.0f), new(12.0f, 4.0f), new(7.5f, 7.0f)], 6, 24, false, false);
        builder.AddSphere(baseCenter + new Vector3(0.0f, 105.0f, 0.0f), 13.0f, 6, 24, 14);
    }

    private static void AddClassicalColumn(MeshBuilder builder, Vector3 baseCenter)
    {
        builder.AddRoundedBox(baseCenter + new Vector3(0.0f, 13.0f, 0.0f), new(50.0f, 13.0f, 50.0f), 3.0f, 0.0f, 4);

        ReadOnlySpan<Vector2> lowerShaft =
        [
            new(45.0f, 26.0f),
            new(47.0f, 34.0f),
            new(41.0f, 42.0f),
            new(37.0f, 50.0f),
            new(40.0f, 58.0f),
            new(35.0f, 66.0f),
            new(34.5f, 220.0f),
            new(32.5f, 385.0f),
            new(30.0f, 530.0f)
        ];
        builder.AddLathe(baseCenter, lowerShaft, 18, 40, false, false);
        builder.AddLathe(baseCenter, [new(30.0f, 530.0f), new(34.5f, 534.0f), new(30.5f, 538.0f)], 6, 40, false, false);
        builder.AddLathe(baseCenter, [new(30.5f, 538.0f), new(32.0f, 545.0f), new(44.0f, 562.0f), new(46.0f, 569.0f)], 18, 40, false, false);
        builder.AddRoundedBox(baseCenter + new Vector3(0.0f, 578.0f, 0.0f), new(52.0f, 9.0f, 52.0f), 2.2f, 0.0f, 18);
    }

    private static void AddTurnedPedestal(MeshBuilder builder, Vector3 baseCenter, float radius, float height)
    {
        ReadOnlySpan<Vector2> bodyProfile =
        [
            new(radius * 1.10f, 0.0f),
            new(radius * 1.10f, 10.0f),
            new(radius * 0.95f, 18.0f),
            new(radius * 0.83f, 24.0f),
            new(radius * 0.81f, height - 16.0f),
            new(radius * 0.96f, height - 8.0f),
            new(radius * 1.02f, height - 4.0f)
        ];
        builder.AddLathe(baseCenter, bodyProfile, 15, 40, true, false);
        builder.AddLathe(baseCenter + new Vector3(0.0f, height - 4.0f, 0.0f), [new(radius * 1.02f, 0.0f), new(radius * 1.05f, 2.0f), new(radius * 1.03f, 4.0f)], 6, 40, false, true);
    }

    private static void AddSculptedUrn(MeshBuilder builder, Vector3 baseCenter, float scale, uint materialId)
    {
        const int BodySamples = 30;
        Span<Vector2> profile = stackalloc Vector2[BodySamples + 7];

        profile[0] = new Vector2(20.5f, 0.0f) * scale;
        profile[1] = new Vector2(23.0f, 4.5f) * scale;
        profile[2] = new Vector2(13.5f, 12.0f) * scale;

        for (int i = 0; i < BodySamples; i++)
        {
            float t = i / (float)(BodySamples - 1);
            float y = 16.0f + (t * 126.0f);
            float belly = 26.0f * MathF.Exp(-MathF.Pow((t - 0.44f) / 0.28f, 2.0f));
            float neckPinch = -5.5f * MathF.Exp(-MathF.Pow((t - 0.86f) / 0.10f, 2.0f));
            float flare = 10.5f * MathF.Pow(Math.Max(0.0f, (t - 0.84f) / 0.16f), 2.2f);
            profile[3 + i] = new Vector2(13.0f + belly + neckPinch + flare, y) * scale;
        }

        profile[3 + BodySamples] = new Vector2(21.5f, 144.0f) * scale;
        profile[4 + BodySamples] = new Vector2(16.5f, 143.5f) * scale;
        profile[5 + BodySamples] = new Vector2(10.5f, 134.0f) * scale;
        profile[6 + BodySamples] = new Vector2(0.0f, 129.0f) * scale;

        builder.AddLathe(baseCenter, profile, materialId, 48, true, false);
    }

    private static void AddCourtyardBench(MeshBuilder builder, Vector2 center)
    {
        float x = center.X;
        float z = center.Y;

        builder.AddRoundedBox(new(x, -17.0f, z - 74.0f), new(28.0f, 10.0f, 10.0f), 2.0f, 0.0f, 4);
        builder.AddRoundedBox(new(x, -17.0f, z + 74.0f), new(28.0f, 10.0f, 10.0f), 2.0f, 0.0f, 4);

        for (int i = 0; i < 4; i++)
        {
            builder.AddRoundedBox(new(x - 21.0f + (i * 14.0f), -3.5f, z), new(5.6f, 3.5f, 92.0f), 1.2f, 0.0f, 5);
        }
    }

    private static void AddTerracottaPlanter(MeshBuilder builder, Vector2 center)
    {
        Vector3 basePosition = new(center.X, -27.0f, center.Y);
        ReadOnlySpan<Vector2> potProfile =
        [
            new(34.0f, 0.0f),
            new(42.0f, 22.0f),
            new(52.0f, 58.0f),
            new(49.0f, 76.0f),
            new(55.0f, 84.0f),
            new(46.0f, 86.0f),
            new(44.0f, 74.0f)
        ];
        builder.AddLathe(basePosition, potProfile, 19, 36, true, true);

        builder.AddSphere(basePosition + new Vector3(0.0f, 114.0f, 0.0f), 39.0f, 20, 24, 14);
        builder.AddSphere(basePosition + new Vector3(0.0f, 152.0f, 0.0f), 29.0f, 20, 22, 12);
        builder.AddSphere(basePosition + new Vector3(0.0f, 182.0f, 0.0f), 19.0f, 20, 18, 10);
    }

    private class MeshBuilder
    {
        private readonly List<SceneVertex> vertices = [with(65536)];
        private readonly List<uint> indices = [with(262144)];
        private readonly List<(uint FirstIndex, uint IndexCount)> meshes = [with(32)];

        private uint currentMeshStartIndex;

        public int CommitMesh()
        {
            uint count = (uint)indices.Count - currentMeshStartIndex;
            int meshIndex = meshes.Count;
            meshes.Add((currentMeshStartIndex, count));
            currentMeshStartIndex = (uint)indices.Count;

            return meshIndex;
        }

        public (SceneVertex[] Vertices, uint[] Indices, (uint FirstIndex, uint IndexCount)[] Meshes) ToArrays()
        {
            return ([.. vertices], [.. indices], [.. meshes]);
        }

        public void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, uint materialId)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
            uint start = (uint)vertices.Count;

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

            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }

        public void AddBox(Vector3 min, Vector3 max, uint materialId)
        {
            AddQuad(new(min.X, max.Y, min.Z), new(min.X, max.Y, max.Z), max, new(max.X, max.Y, min.Z), materialId);
            AddQuad(min, new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z), materialId);
            AddQuad(min, new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, min.Y, min.Z), materialId);
            AddQuad(new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z), max, new(min.X, max.Y, max.Z), materialId);
            AddQuad(min, new(min.X, min.Y, max.Z), new(min.X, max.Y, max.Z), new(min.X, max.Y, min.Z), materialId);
            AddQuad(new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), max, new(max.X, min.Y, max.Z), materialId);
        }

        public void AddRoundedBox(Vector3 center, Vector3 halfExtents, float bevel, float yawRadians, uint materialId)
        {
            float maxBevel = MathF.Min(halfExtents.X, MathF.Min(halfExtents.Y, halfExtents.Z)) * 0.48f;
            float bevelRadius = Math.Clamp(bevel, 0.0f, maxBevel);
            if (bevelRadius <= 0.0001f)
            {
                Matrix4x4 flatRotation = Matrix4x4.CreateRotationY(yawRadians);
                Span<Vector3> corners =
                [
                    Vector3.Transform(new(-halfExtents.X, -halfExtents.Y, -halfExtents.Z), flatRotation) + center,
                    Vector3.Transform(new(halfExtents.X, -halfExtents.Y, -halfExtents.Z), flatRotation) + center,
                    Vector3.Transform(new(halfExtents.X, -halfExtents.Y, halfExtents.Z), flatRotation) + center,
                    Vector3.Transform(new(-halfExtents.X, -halfExtents.Y, halfExtents.Z), flatRotation) + center,
                    Vector3.Transform(new(-halfExtents.X, halfExtents.Y, -halfExtents.Z), flatRotation) + center,
                    Vector3.Transform(new(halfExtents.X, halfExtents.Y, -halfExtents.Z), flatRotation) + center,
                    Vector3.Transform(new(halfExtents.X, halfExtents.Y, halfExtents.Z), flatRotation) + center,
                    Vector3.Transform(new(-halfExtents.X, halfExtents.Y, halfExtents.Z), flatRotation) + center
                ];
                AddQuad(corners[4], corners[7], corners[6], corners[5], materialId);
                AddQuad(corners[0], corners[1], corners[2], corners[3], materialId);
                AddQuad(corners[0], corners[4], corners[5], corners[1], materialId);
                AddQuad(corners[3], corners[2], corners[6], corners[7], materialId);
                AddQuad(corners[0], corners[3], corners[7], corners[4], materialId);
                AddQuad(corners[1], corners[5], corners[6], corners[2], materialId);

                return;
            }

            Vector3 inner = halfExtents - new Vector3(bevelRadius);
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(yawRadians);

            Span<float> xStops = stackalloc float[6];
            Span<float> yStops = stackalloc float[6];
            Span<float> zStops = stackalloc float[6];
            FillStops(inner.X, bevelRadius, xStops);
            FillStops(inner.Y, bevelRadius, yStops);
            FillStops(inner.Z, bevelRadius, zStops);

            EmitRoundedFace(center, inner, bevelRadius, rotation, xStops, zStops, halfExtents.Y, Vector3.UnitY, 0, false, materialId);
            EmitRoundedFace(center, inner, bevelRadius, rotation, xStops, zStops, -halfExtents.Y, -Vector3.UnitY, 0, true, materialId);
            EmitRoundedFace(center, inner, bevelRadius, rotation, xStops, yStops, -halfExtents.Z, -Vector3.UnitZ, 1, false, materialId);
            EmitRoundedFace(center, inner, bevelRadius, rotation, xStops, yStops, halfExtents.Z, Vector3.UnitZ, 1, true, materialId);
            EmitRoundedFace(center, inner, bevelRadius, rotation, zStops, yStops, -halfExtents.X, -Vector3.UnitX, 2, false, materialId);
            EmitRoundedFace(center, inner, bevelRadius, rotation, zStops, yStops, halfExtents.X, Vector3.UnitX, 2, true, materialId);

            static void FillStops(float innerHalf, float radius, Span<float> stops)
            {
                stops[0] = -(innerHalf + radius);
                stops[1] = -(innerHalf + (radius * 0.55f));
                stops[2] = -innerHalf;
                stops[3] = innerHalf;
                stops[4] = innerHalf + (radius * 0.55f);
                stops[5] = innerHalf + radius;
            }
        }

        public void AddSphere(Vector3 center, float radius, uint materialId, int segments = 40, int rings = 20)
        {
            uint startIndex = (uint)vertices.Count;

            for (int i = 0; i <= rings; i++)
            {
                float theta = i * MathF.PI / rings;
                float sinTheta = MathF.Sin(theta);
                float cosTheta = MathF.Cos(theta);

                for (int j = 0; j <= segments; j++)
                {
                    float phi = j * MathF.Tau / segments;
                    Vector3 normal = new(sinTheta * MathF.Cos(phi), cosTheta, sinTheta * MathF.Sin(phi));

                    vertices.Add(new()
                    {
                        Position = center + (normal * radius),
                        Normal = normal,
                        MaterialId = materialId
                    });
                }
            }

            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    uint top = startIndex + (uint)((i * (segments + 1)) + j);
                    uint bottom = top + (uint)segments + 1;

                    if (i > 0)
                    {
                        indices.Add(top);
                        indices.Add(top + 1);
                        indices.Add(bottom + 1);
                    }

                    if (i < (rings - 1))
                    {
                        indices.Add(top);
                        indices.Add(bottom + 1);
                        indices.Add(bottom);
                    }
                }
            }
        }

        public void AddLathe(Vector3 baseCenter, ReadOnlySpan<Vector2> profile, uint materialId, int radialSegments = 44, bool capBottom = true, bool capTop = true)
        {
            if (profile.Length < 2)
            {
                return;
            }

            int count = profile.Length;
            Span<Vector2> radialNormals = stackalloc Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 previous = profile[Math.Max(i - 1, 0)];
                Vector2 next = profile[Math.Min(i + 1, count - 1)];
                Vector2 tangent = next - previous;
                if (tangent.LengthSquared() < 0.000001f)
                {
                    radialNormals[i] = new(1.0f, 0.0f);
                }
                else
                {
                    tangent = Vector2.Normalize(tangent);
                    radialNormals[i] = new(tangent.Y, -tangent.X);
                }
            }

            for (int i = 0; i < count - 1; i++)
            {
                Vector2 p0 = profile[i];
                Vector2 p1 = profile[i + 1];
                Vector2 segmentDelta = p1 - p0;
                if (segmentDelta.LengthSquared() < 0.000001f)
                {
                    continue;
                }

                Vector2 segmentTangent = Vector2.Normalize(segmentDelta);
                Vector2 flatNormal = new(segmentTangent.Y, -segmentTangent.X);
                Vector2 n0 = Vector2.Dot(radialNormals[i], flatNormal) > 0.52f ? radialNormals[i] : flatNormal;
                Vector2 n1 = Vector2.Dot(radialNormals[i + 1], flatNormal) > 0.52f ? radialNormals[i + 1] : flatNormal;

                uint ringStart = (uint)vertices.Count;

                for (int j = 0; j <= radialSegments; j++)
                {
                    float phi = j * MathF.Tau / radialSegments;
                    float cosPhi = MathF.Cos(phi);
                    float sinPhi = MathF.Sin(phi);

                    vertices.Add(new()
                    {
                        Position = baseCenter + new Vector3(cosPhi * p0.X, p0.Y, sinPhi * p0.X),
                        Normal = Vector3.Normalize(new(cosPhi * n0.X, n0.Y, sinPhi * n0.X)),
                        MaterialId = materialId
                    });
                    vertices.Add(new()
                    {
                        Position = baseCenter + new Vector3(cosPhi * p1.X, p1.Y, sinPhi * p1.X),
                        Normal = Vector3.Normalize(new(cosPhi * n1.X, n1.Y, sinPhi * n1.X)),
                        MaterialId = materialId
                    });
                }

                for (int j = 0; j < radialSegments; j++)
                {
                    uint b0 = ringStart + (uint)(j * 2);
                    uint t0 = b0 + 1;
                    uint b1 = b0 + 2;
                    uint t1 = b0 + 3;

                    indices.Add(b0);
                    indices.Add(t0);
                    indices.Add(t1);
                    indices.Add(b0);
                    indices.Add(t1);
                    indices.Add(b1);
                }
            }

            if (capBottom && profile[0].X > 0.001f)
            {
                AddDiscCap(baseCenter + new Vector3(0.0f, profile[0].Y, 0.0f), profile[0].X, -Vector3.UnitY, radialSegments, materialId);
            }

            if (capTop && profile[^1].X > 0.001f)
            {
                AddDiscCap(baseCenter + new Vector3(0.0f, profile[^1].Y, 0.0f), profile[^1].X, Vector3.UnitY, radialSegments, materialId);
            }
        }

        public void AddCylinderY(Vector3 baseCenter, float radius, float height, uint materialId, int segments = 36)
        {
            AddLathe(baseCenter, [new(radius, 0.0f), new(radius, height)], materialId, segments);
        }

        public void AddTorus(float majorRadius, float minorRadius, uint materialId, int majorSegments = 64, int minorSegments = 16)
        {
            uint startIndex = (uint)vertices.Count;

            for (int i = 0; i <= majorSegments; i++)
            {
                float majorAngle = i * MathF.Tau / majorSegments;
                Vector3 direction = new(MathF.Cos(majorAngle), MathF.Sin(majorAngle), 0.0f);
                Vector3 ringCenter = direction * majorRadius;

                for (int j = 0; j <= minorSegments; j++)
                {
                    float minorAngle = j * MathF.Tau / minorSegments;
                    Vector3 normal = (direction * MathF.Cos(minorAngle)) + (Vector3.UnitZ * MathF.Sin(minorAngle));

                    vertices.Add(new()
                    {
                        Position = ringCenter + (normal * minorRadius),
                        Normal = normal,
                        MaterialId = materialId
                    });
                }
            }

            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    uint current = startIndex + (uint)((i * (minorSegments + 1)) + j);
                    uint next = current + (uint)minorSegments + 1;

                    indices.Add(current);
                    indices.Add(next);
                    indices.Add(next + 1);
                    indices.Add(current);
                    indices.Add(next + 1);
                    indices.Add(current + 1);
                }
            }
        }

        public void AddTorusKnot(int p, int q, float majorRadius, float minorRadius, float tubeRadius, uint materialId, int pathSegments = 144, int tubeSegments = 20)
        {
            const float Epsilon = 0.002f;

            uint startIndex = (uint)vertices.Count;

            for (int i = 0; i <= pathSegments; i++)
            {
                float t = i % pathSegments * MathF.Tau / pathSegments;
                Vector3 center = EvaluateCurve(p, q, majorRadius, minorRadius, t);
                Vector3 previous = EvaluateCurve(p, q, majorRadius, minorRadius, t - Epsilon);
                Vector3 next = EvaluateCurve(p, q, majorRadius, minorRadius, t + Epsilon);

                Vector3 tangent = Vector3.Normalize(next - previous);
                Vector3 radialHint = Vector3.Normalize(center + new Vector3(0.0f, majorRadius * 0.35f, 0.0f));
                Vector3 binormal = Vector3.Normalize(Vector3.Cross(tangent, radialHint));
                Vector3 normal = Vector3.Normalize(Vector3.Cross(binormal, tangent));

                for (int j = 0; j <= tubeSegments; j++)
                {
                    float phi = j % tubeSegments * MathF.Tau / tubeSegments;
                    Vector3 ringNormal = (normal * MathF.Cos(phi)) + (binormal * MathF.Sin(phi));

                    vertices.Add(new()
                    {
                        Position = center + (ringNormal * tubeRadius),
                        Normal = ringNormal,
                        MaterialId = materialId
                    });
                }
            }

            for (int i = 0; i < pathSegments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    uint current = startIndex + (uint)((i * (tubeSegments + 1)) + j);
                    uint next = current + (uint)tubeSegments + 1;

                    indices.Add(current);
                    indices.Add(next + 1);
                    indices.Add(next);
                    indices.Add(current);
                    indices.Add(current + 1);
                    indices.Add(next + 1);
                }
            }

            static Vector3 EvaluateCurve(int knotP, int knotQ, float majorR, float minorR, float t)
            {
                float radius = majorR + (minorR * MathF.Cos(knotQ * t));

                return new(radius * MathF.Cos(knotP * t), minorR * MathF.Sin(knotQ * t), radius * MathF.Sin(knotP * t));
            }
        }

        private void EmitRoundedFace(Vector3 center,
                                     Vector3 inner,
                                     float radius,
                                     Matrix4x4 rotation,
                                     ReadOnlySpan<float> uStops,
                                     ReadOnlySpan<float> vStops,
                                     float fixedCoordinate,
                                     Vector3 fallbackNormal,
                                     int axisMode,
                                     bool flipWinding,
                                     uint materialId)
        {
            uint baseVertex = (uint)vertices.Count;
            int uCount = uStops.Length;
            int vCount = vStops.Length;

            for (int i = 0; i < vCount; i++)
            {
                for (int j = 0; j < uCount; j++)
                {
                    Vector3 raw = axisMode switch
                    {
                        0 => new(uStops[j], fixedCoordinate, vStops[i]),
                        1 => new(uStops[j], vStops[i], fixedCoordinate),
                        _ => new(fixedCoordinate, vStops[i], uStops[j])
                    };

                    Vector3 core = Vector3.Clamp(raw, -inner, inner);
                    Vector3 offset = raw - core;
                    Vector3 localNormal = offset.LengthSquared() > 0.000001f ? Vector3.Normalize(offset) : fallbackNormal;
                    Vector3 localPosition = core + (localNormal * radius);

                    vertices.Add(new()
                    {
                        Position = Vector3.Transform(localPosition, rotation) + center,
                        Normal = Vector3.Normalize(Vector3.TransformNormal(localNormal, rotation)),
                        MaterialId = materialId
                    });
                }
            }

            for (int i = 0; i < vCount - 1; i++)
            {
                for (int j = 0; j < uCount - 1; j++)
                {
                    uint v00 = baseVertex + (uint)((i * uCount) + j);
                    uint v10 = v00 + 1;
                    uint v01 = v00 + (uint)uCount;
                    uint v11 = v01 + 1;

                    if (flipWinding)
                    {
                        indices.Add(v00);
                        indices.Add(v10);
                        indices.Add(v11);
                        indices.Add(v00);
                        indices.Add(v11);
                        indices.Add(v01);
                    }
                    else
                    {
                        indices.Add(v00);
                        indices.Add(v01);
                        indices.Add(v11);
                        indices.Add(v00);
                        indices.Add(v11);
                        indices.Add(v10);
                    }
                }
            }
        }

        private void AddDiscCap(Vector3 center, float radius, Vector3 normal, int segments, uint materialId)
        {
            uint centerIndex = (uint)vertices.Count;

            vertices.Add(new()
            {
                Position = center,
                Normal = normal,
                MaterialId = materialId
            });

            for (int i = 0; i <= segments; i++)
            {
                float phi = i * MathF.Tau / segments;

                vertices.Add(new()
                {
                    Position = center + new Vector3(MathF.Cos(phi) * radius, 0.0f, MathF.Sin(phi) * radius),
                    Normal = normal,
                    MaterialId = materialId
                });
            }

            bool up = normal.Y > 0.0f;
            for (int i = 0; i < segments; i++)
            {
                uint r0 = centerIndex + 1 + (uint)i;
                uint r1 = r0 + 1;

                if (up)
                {
                    indices.Add(centerIndex);
                    indices.Add(r1);
                    indices.Add(r0);
                }
                else
                {
                    indices.Add(centerIndex);
                    indices.Add(r0);
                    indices.Add(r1);
                }
            }
        }
    }
}
