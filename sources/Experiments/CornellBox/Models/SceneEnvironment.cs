using System.Numerics;
using System.Runtime.InteropServices;

namespace CornellBox.Models;

[StructLayout(LayoutKind.Explicit, Size = 96)]
internal struct SceneEnvironment
{
    private const uint FlagIsPaused = 1 << 0;
    private const uint FlagIsLunarDisk = 1 << 1;

    [FieldOffset(0)]
    public Vector3 KeyLightDirection;

    [FieldOffset(12)]
    public float KeyLightCosRadius;

    [FieldOffset(16)]
    public Vector3 KeyLightIrradiance;

    [FieldOffset(28)]
    public float SoftKeyCosRadius;

    [FieldOffset(32)]
    public Vector3 SkyZenithColor;

    [FieldOffset(44)]
    public float CloudCoverage;

    [FieldOffset(48)]
    public Vector3 SkyHorizonColor;

    [FieldOffset(60)]
    public float StarIntensity;

    [FieldOffset(64)]
    public Vector3 CeilingLightRadiance;

    [FieldOffset(76)]
    public float NightLightIntensity;

    [FieldOffset(80)]
    public float ElapsedSeconds;

    [FieldOffset(84)]
    public float SunAtmosphereFactor;

    [FieldOffset(88)]
    public float Exposure;

    [FieldOffset(92)]
    public uint EnvironmentFlags;

    public static SceneEnvironment Create(AtmospherePreset preset, float elapsedSeconds, bool isPaused)
    {
        uint flags = isPaused ? FlagIsPaused : 0;

        return preset switch
        {
            AtmospherePreset.Day => new()
            {
                KeyLightDirection = Vector3.Normalize(new(0.44068358f, 0.52080786f, -0.73113406f)),
                KeyLightCosRadius = 0.9996875f,
                KeyLightIrradiance = new(5.80f, 4.42f, 3.00f),
                SoftKeyCosRadius = 0.9992f,
                SkyZenithColor = new(0.065f, 0.205f, 0.565f),
                CloudCoverage = 0.56f,
                SkyHorizonColor = new(0.46f, 0.64f, 0.84f),
                StarIntensity = 0.0f,
                CeilingLightRadiance = new(25.0f, 22.25f, 17.5f),
                NightLightIntensity = 0.0f,
                ElapsedSeconds = elapsedSeconds,
                SunAtmosphereFactor = 1.0f,
                Exposure = 1.00f,
                EnvironmentFlags = flags
            },

            AtmospherePreset.Sunset => new()
            {
                KeyLightDirection = Vector3.Normalize(new(-0.398f, 0.125f, 0.909f)),
                KeyLightCosRadius = 0.99966f,
                KeyLightIrradiance = new(6.20f, 2.55f, 0.82f),
                SoftKeyCosRadius = 0.9988f,
                SkyZenithColor = new(0.048f, 0.085f, 0.280f),
                CloudCoverage = 0.54f,
                SkyHorizonColor = new(0.88f, 0.38f, 0.14f),
                StarIntensity = 0.08f,
                CeilingLightRadiance = new(20.5f, 17.8f, 13.4f),
                NightLightIntensity = 0.62f,
                ElapsedSeconds = elapsedSeconds,
                SunAtmosphereFactor = 0.42f,
                Exposure = 1.04f,
                EnvironmentFlags = flags
            },

            AtmospherePreset.Night => new()
            {
                KeyLightDirection = Vector3.Normalize(new(0.376f, 0.118f, 0.919f)),
                KeyLightCosRadius = 0.99968f,
                KeyLightIrradiance = new(0.42f, 0.58f, 0.92f),
                SoftKeyCosRadius = 0.99935f,
                SkyZenithColor = new(0.0045f, 0.0088f, 0.024f),
                CloudCoverage = 0.22f,
                SkyHorizonColor = new(0.016f, 0.030f, 0.064f),
                StarIntensity = 1.0f,
                CeilingLightRadiance = new(13.5f, 11.2f, 8.1f),
                NightLightIntensity = 1.0f,
                ElapsedSeconds = elapsedSeconds,
                SunAtmosphereFactor = 0.0f,
                Exposure = 1.25f,
                EnvironmentFlags = flags | FlagIsLunarDisk
            },

            _ => default
        };
    }
}
