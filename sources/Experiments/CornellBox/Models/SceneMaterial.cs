using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Models;

[StructLayout(LayoutKind.Explicit, Size = 48)]
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

    [FieldOffset(24)]
    public float Transmission;

    [FieldOffset(28)]
    public float Ior;

    [FieldOffset(32)]
    public float Clearcoat;

    [FieldOffset(36)]
    public float ClearcoatRoughness;

    [FieldOffset(40)]
    public MaterialPattern PatternType;

    [FieldOffset(44)]
    public float PatternScale;

    public static SceneMaterial Matte(Vector3 albedo, float roughness = 0.82f, MaterialPattern pattern = MaterialPattern.Solid, float patternScale = 50.0f)
    {
        return new()
        {
            Albedo = albedo,
            Roughness = roughness,
            Ior = 1.5f,
            PatternType = pattern,
            PatternScale = patternScale
        };
    }

    public static SceneMaterial Metal(Vector3 albedo, float roughness, MaterialPattern pattern = MaterialPattern.Solid, float patternScale = 50.0f)
    {
        return new()
        {
            Albedo = albedo,
            Roughness = roughness,
            Metallic = 1.0f,
            Ior = 1.5f,
            PatternType = pattern,
            PatternScale = patternScale
        };
    }

    public static SceneMaterial Coated(Vector3 albedo, float baseRoughness, float clearcoat = 1.0f, float coatRoughness = 0.05f, MaterialPattern pattern = MaterialPattern.Solid, float patternScale = 50.0f)
    {
        return new()
        {
            Albedo = albedo,
            Roughness = baseRoughness,
            Ior = 1.5f,
            Clearcoat = clearcoat,
            ClearcoatRoughness = coatRoughness,
            PatternType = pattern,
            PatternScale = patternScale
        };
    }

    public static SceneMaterial Glass(Vector3 tint, float ior = 1.52f, float roughness = 0.02f)
    {
        return new()
        {
            Albedo = tint,
            Roughness = roughness,
            Transmission = 1.0f,
            Ior = ior
        };
    }

    public static SceneMaterial Emissive(Vector3 color, float intensity)
    {
        return new()
        {
            Albedo = color,
            Roughness = 0.5f,
            Emission = intensity,
            Ior = 1.5f
        };
    }
}
