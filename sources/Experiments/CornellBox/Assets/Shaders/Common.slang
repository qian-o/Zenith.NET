static const float PI = 3.14159265359;
static const float TwoPI = 6.28318530718;
static const float InvPI = 0.31830988618;
static const float RayOffset = 0.05;
static const float RayMinDistance = 0.01;
static const float RayMaxDistance = 50000.0;
static const float MinimumRoughness = 0.035;

static const uint InvalidIndex = 0xFFFFFFFFu;
static const uint RayMaskOpaque = 0x01u;
static const uint RayMaskAll = 0xFFu;
static const uint CeilingLightMaterialId = 14u;
static const uint LimestoneMaterialId = 18u;
static const uint TerracottaMaterialId = 19u;
static const uint FoliageMaterialId = 20u;
static const uint WaterMaterialId = 21u;
static const uint LanternCoreMaterialId = 22u;
static const uint RecessedStripMaterialId = 23u;
static const uint MaxMaterialId = 24u;

static const float3 GlassSphereCenter = float3(-168.0, 190.0, 195.0);
static const float GlassSphereRadius = 48.0;
static const float3 EmeraldUrnCenter = float3(-548.0, 148.0, 425.0);
static const float EmeraldUrnRadius = 38.0;

static const uint PatternSolid = 0u;
static const uint PatternChecker = 1u;
static const uint PatternGrid = 2u;
static const uint PatternStripes = 3u;
static const uint PatternMarble = 4u;
static const uint PatternFluted = 5u;

static const float3 CeilingLightCenter = float3(0.0, 553.5, 275.0);
static const float2 CeilingLightHalfSize = float2(93.0, 83.0);
static const float CeilingLightArea = 30876.0;
static const float3 DefaultCeilingLightRadiance = float3(25.0, 22.25, 17.5);

static const uint EnvFlagIsPaused = 1u;
static const uint EnvFlagIsLunarDisk = 2u;

static const uint NightLanternCount = 10u;
static const float NightLanternRange = 640.0;
static const float3 NightLanternPositions[NightLanternCount] =
{
    float3( 310.0, 125.0, -152.0),
    float3(-310.0, 125.0, -152.0),
    float3( 445.0, 125.0, -690.0),
    float3(-445.0, 125.0, -690.0),
    float3( 648.0, 125.0,   84.0),
    float3(-648.0, 125.0,   84.0),
    float3( 648.0, 125.0,  484.0),
    float3(-648.0, 125.0,  484.0),
    float3( 320.0, 125.0,  740.0),
    float3(-320.0, 125.0,  740.0)
};

struct SceneEnvironment
{
    private float4 KeyLightDirectionAndCosRadius;

    private float4 KeyLightIrradianceAndSoftCos;

    private float4 SkyZenithAndCloudCoverage;

    private float4 SkyHorizonAndStarIntensity;

    private float4 CeilingRadianceAndNightIntensity;

    private float4 TimingAndFlags;

    property float3 KeyLightDirection
    {
        get
        {
            return KeyLightDirectionAndCosRadius.xyz;
        }
    }

    property float KeyLightCosRadius
    {
        get
        {
            return KeyLightDirectionAndCosRadius.w;
        }
    }

    property float3 KeyLightIrradiance
    {
        get
        {
            return KeyLightIrradianceAndSoftCos.xyz;
        }
    }

    property float SoftKeyCosRadius
    {
        get
        {
            return KeyLightIrradianceAndSoftCos.w;
        }
    }

    property float3 SkyZenithColor
    {
        get
        {
            return SkyZenithAndCloudCoverage.xyz;
        }
    }

    property float CloudCoverage
    {
        get
        {
            return SkyZenithAndCloudCoverage.w;
        }
    }

    property float3 SkyHorizonColor
    {
        get
        {
            return SkyHorizonAndStarIntensity.xyz;
        }
    }

    property float StarIntensity
    {
        get
        {
            return SkyHorizonAndStarIntensity.w;
        }
    }

    property float3 CeilingLightRadiance
    {
        get
        {
            return CeilingRadianceAndNightIntensity.xyz;
        }
    }

    property float NightLightIntensity
    {
        get
        {
            return CeilingRadianceAndNightIntensity.w;
        }
    }

    property float ElapsedSeconds
    {
        get
        {
            return TimingAndFlags.x;
        }
    }

    property float SunAtmosphereFactor
    {
        get
        {
            return TimingAndFlags.y;
        }
    }

    property bool IsPaused
    {
        get
        {
            return (asuint(TimingAndFlags.w) & EnvFlagIsPaused) != 0u;
        }
    }

    property bool IsLunarDisk
    {
        get
        {
            return (asuint(TimingAndFlags.w) & EnvFlagIsLunarDisk) != 0u;
        }
    }
};

static const uint DDGICascade0Probes = 1920u;
static const uint DDGITotalProbes = 2784u;
static const uint DDGIAtlasColumns = 64u;
static const float2 DDGIIrradianceAtlasSize = float2(512.0, 352.0);
static const float2 DDGIDistanceAtlasSize = float2(1024.0, 704.0);

static const float3 DDGICascade0Origin = float3(-600.0, 10.0, -92.0);
static const float3 DDGICascade0Spacing = float3(80.0, 58.0, 64.0);
static const int3 DDGICascade0Counts = int3(16, 10, 12);

static const float3 DDGICascade1Origin = float3(-1155.0, 10.0, -1150.0);
static const float3 DDGICascade1Spacing = float3(210.0, 126.0, 196.0);
static const int3 DDGICascade1Counts = int3(12, 6, 12);

struct SceneVertex
{
    float3 Position;
    float Padding;

    float3 Normal;
    uint MaterialId;
};

struct SceneMaterial
{
    float3 Albedo;
    float Roughness;

    float Metallic;
    float Emission;
    float Transmission;
    float Ior;

    float Clearcoat;
    float ClearcoatRoughness;
    uint PatternType;
    float PatternScale;
};

struct SceneInstance
{
    float4x4 ObjectToWorld;

    float4x4 PreviousObjectToWorld;

    uint FirstIndex;
    uint IsDynamic;
    uint MaterialOverride;
    uint Padding;
};

struct GeometryHit
{
    float3 Position;
    uint MaterialId;

    float3 ObjectPosition;
    uint IsDynamic;

    float3 Normal;
    float3 LocalNormal;
    float3 GeometricNormal;
};

uint PCGHash(uint input)
{
    uint state = input * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

float InterleavedGradientNoise(uint2 pixel, uint frameIndex)
{
    float2 f = float2(pixel) + 5.588238 * float(frameIndex & 63u);

    return frac(52.9829189 * frac(0.06711056 * f.x + 0.00583715 * f.y));
}

float2 OctEncode(float3 n)
{
    n /= max(abs(n.x) + abs(n.y) + abs(n.z), 1e-6);
    if (n.z < 0.0)
    {
        n.xy = (1.0 - abs(n.yx)) * select(n.xy >= 0.0, float2(1.0, 1.0), float2(-1.0, -1.0));
    }

    return n.xy;
}

float3 OctDecode(float2 e)
{
    float3 n = float3(e.xy, 1.0 - abs(e.x) - abs(e.y));
    if (n.z < 0.0)
    {
        n.xy = (1.0 - abs(n.yx)) * select(n.xy >= 0.0, float2(1.0, 1.0), float2(-1.0, -1.0));
    }

    return normalize(n);
}

int2 OctBorderToInterior(int2 localTexel, int interiorSize)
{
    int2 inner = localTexel - 1;
    bool flipX = inner.x < 0 || inner.x >= interiorSize;
    bool flipY = inner.y < 0 || inner.y >= interiorSize;

    if (flipX && flipY)
    {
        return int2(inner.x < 0 ? interiorSize - 1 : 0, inner.y < 0 ? interiorSize - 1 : 0);
    }
    if (flipX)
    {
        return int2(inner.x < 0 ? 0 : interiorSize - 1, interiorSize - 1 - inner.y);
    }
    if (flipY)
    {
        return int2(interiorSize - 1 - inner.x, inner.y < 0 ? 0 : interiorSize - 1);
    }

    return inner;
}

float2 ComputeMotionVector(float4 currentClip, float4 previousClip)
{
    float currentInvW = 1.0 / max(abs(currentClip.w), 1e-5);
    float previousInvW = 1.0 / max(abs(previousClip.w), 1e-5);
    float2 currentNdc = currentClip.xy * currentInvW;
    float2 previousNdc = previousClip.xy * previousInvW;

    return (previousNdc - currentNdc) * float2(0.5, -0.5);
}

float3 GetCameraRayDirection(float2 uv, float4x4 inverseProjection, float4x4 inverseView)
{
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 viewTarget = mul(float4(ndc, 1.0, 1.0), inverseProjection);
    float3 viewDir = viewTarget.xyz / max(abs(viewTarget.w), 1e-5);

    return normalize(mul(float4(viewDir, 0.0), inverseView).xyz);
}

float3 ReconstructWorldPosition(float2 uv, float deviceDepth, float4x4 inverseViewProjection)
{
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 worldPos = mul(float4(ndc, deviceDepth, 1.0), inverseViewProjection);

    return worldPos.xyz / worldPos.w;
}

float3 OffsetRayOrigin(float3 position, float3 geometricNormal, float3 direction)
{
    float signValue = dot(geometricNormal, direction) >= 0.0 ? 1.0 : -1.0;

    return position + geometricNormal * (RayOffset * signValue);
}

void BuildOrthonormalBasis(float3 n, out float3 tangent, out float3 bitangent)
{
    float3 helper = abs(n.x) > 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    tangent = normalize(cross(helper, n));
    bitangent = cross(n, tangent);
}

float ValueNoise2D(float2 p)
{
    int2 cell = int2(floor(p));
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    uint h00 = PCGHash(uint(cell.x) * 374761393u + uint(cell.y) * 668265263u);
    uint h10 = PCGHash(uint(cell.x + 1) * 374761393u + uint(cell.y) * 668265263u);
    uint h01 = PCGHash(uint(cell.x) * 374761393u + uint(cell.y + 1) * 668265263u);
    uint h11 = PCGHash(uint(cell.x + 1) * 374761393u + uint(cell.y + 1) * 668265263u);

    float v00 = float(h00 & 0xFFFFu) * (1.0 / 65535.0);
    float v10 = float(h10 & 0xFFFFu) * (1.0 / 65535.0);
    float v01 = float(h01 & 0xFFFFu) * (1.0 / 65535.0);
    float v11 = float(h11 & 0xFFFFu) * (1.0 / 65535.0);

    return lerp(lerp(v00, v10, u.x), lerp(v01, v11, u.x), u.y);
}

float2 TriplanarCoords(float3 position, float3 normal, float scale)
{
    float3 axis = abs(normal);
    float2 uv;
    if (axis.y >= max(axis.x, axis.z))
    {
        uv = position.xz;
    }
    else if (axis.x >= axis.z)
    {
        uv = position.zy;
    }
    else
    {
        uv = position.xy;
    }

    return uv / max(scale, 0.001);
}

float FbmNoise2D(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;
    float2 q = p;
    for (int i = 0; i < 3; i++)
    {
        sum += ValueNoise2D(q) * amp;
        q = float2(q.x * 1.6 - q.y * 1.2, q.x * 1.2 + q.y * 1.6);
        amp *= 0.5;
    }

    return sum;
}

float3 EvaluateStarLayer(float3 dir, float gridScale, float densityThreshold, float intensityScale, float elapsedSeconds)
{
    float3 absD = abs(dir);
    uint faceId = 0u;
    float2 faceUV = 0.0;
    if (absD.x >= absD.y && absD.x >= absD.z)
    {
        faceId = dir.x >= 0.0 ? 0u : 1u;
        faceUV = dir.yz / absD.x;
    }
    else if (absD.y >= absD.z)
    {
        faceId = dir.y >= 0.0 ? 2u : 3u;
        faceUV = dir.xz / absD.y;
    }
    else
    {
        faceId = dir.z >= 0.0 ? 4u : 5u;
        faceUV = dir.xy / absD.z;
    }

    float2 scaled = (faceUV * 0.5 + 0.5) * gridScale;
    int2 cell = int2(floor(scaled));
    float2 local = frac(scaled);

    uint seed = PCGHash((faceId * 1597334677u) ^ (uint(cell.x) * 374761393u) ^ (uint(cell.y) * 668265263u));
    float presence = float(seed & 0xFFFFu) * (1.0 / 65535.0);
    if (presence < densityThreshold)
    {
        return 0.0;
    }

    uint seed2 = PCGHash(seed);
    float2 starPos = float2(float(seed2 & 0xFFu), float((seed2 >> 8u) & 0xFFu)) * (1.0 / 255.0);
    starPos = lerp(0.20, 0.80, starPos);

    float dist = length(local - starPos);
    float sizeFactor = lerp(0.11, 0.21, float((seed2 >> 16u) & 0xFFu) * (1.0 / 255.0));
    float falloff = smoothstep(sizeFactor, 0.0, dist);
    falloff = falloff * falloff;

    float mag = pow(float((seed2 >> 24u) & 0xFFu) * (1.0 / 255.0), 4.0);
    float twinkle = 0.86 + 0.14 * sin(elapsedSeconds * lerp(1.2, 2.8, presence) + float(seed & 255u));
    float temp = float((seed >> 16u) & 0xFFu) * (1.0 / 255.0);
    float3 starColor = lerp(float3(1.00, 0.78, 0.58), float3(0.74, 0.86, 1.05), temp);

    return starColor * (falloff * (0.20 + 2.40 * mag) * twinkle * intensityScale);
}

float3 EvaluateSkyAmbient(float3 direction, SceneEnvironment env)
{
    float3 keyDir = env.KeyLightDirection;
    float up = saturate(direction.y);
    float3 gradient = lerp(env.SkyHorizonColor, env.SkyZenithColor, pow(up, 0.60));

    float keyAlign = saturate(dot(direction, keyDir));
    float2 dirXZ = normalize(direction.xz + float2(1e-5, 0.0));
    float2 keyXZ = normalize(keyDir.xz + float2(1e-5, 0.0));
    float antiSun = saturate(-dot(dirXZ, keyXZ));

    if (env.SunAtmosphereFactor > 0.02 && env.SunAtmosphereFactor < 0.85)
    {
        float duskWeight = sin(saturate((env.SunAtmosphereFactor - 0.02) / 0.83) * PI);
        float venusBand = exp(-pow((direction.y - 0.11) / 0.14, 2.0)) * (0.45 + 0.55 * antiSun);
        gradient += float3(0.24, 0.10, 0.28) * (venusBand * duskWeight * 0.45);
        gradient += env.KeyLightIrradiance * (pow(saturate(dot(dirXZ, keyXZ)), 2.5) * exp(-abs(direction.y) * 5.5) * 0.11);
    }

    if (env.IsLunarDisk)
    {
        float lunarCorona = pow(keyAlign, 38.0) * 0.42 + pow(keyAlign, 6.0) * 0.06;
        gradient += float3(0.45, 0.62, 0.96) * (lunarCorona * 0.26);
    }
    else
    {
        float mieGlow = pow(keyAlign, 14.0) * 0.48 + pow(keyAlign, 3.0) * 0.12;
        gradient += env.KeyLightIrradiance * (mieGlow * 0.11);
    }

    if (direction.y < 0.0)
    {
        gradient *= lerp(1.0, 0.25, saturate(-direction.y * 3.2));
    }

    return gradient;
}

float3 EvaluateSkyFull(float3 direction, SceneEnvironment env)
{
    float3 keyDir = env.KeyLightDirection;
    float keyAlign = saturate(dot(direction, keyDir));
    float3 gradient = EvaluateSkyAmbient(direction, env);

    if (env.StarIntensity > 0.01 && direction.y > 0.004)
    {
        float horizonVis = smoothstep(0.008, 0.14, direction.y);
        float galacticY = dot(direction, normalize(float3(0.32, 0.82, -0.47)));
        if (abs(galacticY) < 0.55)
        {
            float galAz = atan2(direction.z, direction.x);
            float band = exp(-galacticY * galacticY * 14.0);
            float neb = FbmNoise2D(float2(galAz * 3.5, galacticY * 8.0)) / 0.875;
            float rift = smoothstep(0.42, 0.72, FbmNoise2D(float2(galAz * 7.2 + 3.1, galacticY * 15.0)) / 0.875);
            gradient += band * neb * (1.0 - 0.55 * rift) * float3(0.034, 0.048, 0.092) * (env.StarIntensity * horizonVis);
        }

        float3 stars = EvaluateStarLayer(direction, 138.0, 0.945, 1.35, env.ElapsedSeconds)
                     + EvaluateStarLayer(direction, 275.0, 0.920, 0.65, env.ElapsedSeconds + 13.7);
        gradient += stars * (env.StarIntensity * horizonVis);
    }

    float diskCos = env.KeyLightCosRadius;
    float rimMargin = (1.0 - diskCos) * 0.18;
    if (keyAlign >= diskCos - rimMargin)
    {
        float diskAlpha = smoothstep(diskCos - rimMargin, diskCos + rimMargin * 0.35, keyAlign);
        if (env.IsLunarDisk)
        {
            float3 moonRight;
            float3 moonUp;
            BuildOrthonormalBasis(keyDir, moonRight, moonUp);
            float sinRadius = sqrt(max(1.0 - diskCos * diskCos, 1e-7));
            float3 delta = direction - keyDir * dot(direction, keyDir);
            float2 moonUV = float2(dot(delta, moonRight), dot(delta, moonUp)) / sinRadius;
            float r2 = saturate(dot(moonUV, moonUV));
            float z = sqrt(max(1.0 - r2, 0.0));
            float maria = FbmNoise2D(moonUV * 2.1 + float2(1.4, -0.8)) / 0.875;
            float crater = FbmNoise2D(moonUV * 6.4 + float2(-2.1, 3.2)) / 0.875;
            float albedoTex = lerp(0.24, 1.06, smoothstep(0.34, 0.60, maria)) * lerp(0.80, 1.15, crater);
            float limb = pow(max(z, 0.08), 0.38);
            float3 moonColor = float3(0.90, 0.95, 1.04) * (1.52 * albedoTex * limb);
            gradient = lerp(gradient, moonColor, diskAlpha);
        }
        else
        {
            float limb = pow(saturate((keyAlign - (diskCos - rimMargin)) / max(1.0 - diskCos + rimMargin, 1e-6)), 0.35);
            float3 sunColor = env.SunAtmosphereFactor < 0.7 ? float3(11.0, 4.2, 1.1) : env.KeyLightIrradiance * 18.0;
            gradient += sunColor * (limb * diskAlpha);
        }
    }

    if (env.CloudCoverage > 0.01 && direction.y > 0.014)
    {
        float2 wind = env.ElapsedSeconds * float2(0.0085, 0.0042);
        float2 cloudUV = direction.xz / (direction.y + 0.13);
        float n1 = FbmNoise2D(cloudUV * 0.45 + wind) / 0.875;
        float n2 = FbmNoise2D(cloudUV * 1.25 - wind * 0.55) / 0.875;
        float cloudField = n1 * 0.68 + n2 * 0.32;
        float threshold = lerp(0.58, 0.30, env.CloudCoverage);
        float cumulus = smoothstep(threshold, threshold + 0.24, cloudField);
        float cirrus = smoothstep(0.56, 0.84, FbmNoise2D(float2(cloudUV.x * 0.26 + env.ElapsedSeconds * 0.004, cloudUV.y * 0.95)) / 0.875);
        float horizFade = smoothstep(0.018, 0.085, direction.y);
        float cloudAlpha = saturate((cumulus * 0.86 + cirrus * 0.28) * horizFade * (env.IsLunarDisk ? 0.42 : 0.92));

        if (cloudAlpha > 0.005)
        {
            float cloudTop = saturate((n1 - 0.35) * 1.6 - (n2 - 0.5) * 0.3 + 0.48);
            float silver = pow(keyAlign, 8.0) * 0.65 + pow(keyAlign, 2.0) * 0.18;
            float3 cloudLit;
            if (env.IsLunarDisk)
            {
                cloudLit = lerp(float3(0.016, 0.025, 0.048), float3(0.075, 0.108, 0.175), cloudTop)
                         + float3(0.18, 0.26, 0.42) * silver;
            }
            else if (env.SunAtmosphereFactor > 0.7)
            {
                float3 cloudBase = float3(1.32, 1.30, 1.26);
                float3 cloudShadow = float3(0.18, 0.30, 0.54);
                cloudLit = lerp(cloudShadow, cloudBase + env.KeyLightIrradiance * (0.08 + 0.16 * silver), cloudTop);
            }
            else
            {
                float3 cloudBase = lerp(env.SkyHorizonColor * 0.75, float3(1.18, 1.16, 1.14), env.SunAtmosphereFactor * 0.72 + 0.24);
                float3 cloudShadow = lerp(env.SkyZenithColor * 0.72 + float3(0.10, 0.15, 0.24), env.SkyHorizonColor * 0.52, 0.40);
                cloudLit = lerp(cloudShadow, cloudBase + env.KeyLightIrradiance * (0.14 + 0.22 * silver), cloudTop);
            }
            gradient = lerp(gradient, cloudLit, cloudAlpha);
        }
    }

    if (direction.y < 0.085)
    {
        float az = atan2(direction.x, direction.z);
        float distantRidge = 0.022 + 0.048 * (FbmNoise2D(float2(az * 2.1 - 1.9, 0.9)) / 0.875);
        float farRidge = 0.010 + 0.042 * (FbmNoise2D(float2(az * 3.8 + 1.1, 2.7)) / 0.875);
        float nearRidge = -0.006 + 0.026 * (FbmNoise2D(float2(az * 7.6 + 6.4, 8.3)) / 0.875);
        float ridgeHeight = max(distantRidge, max(farRidge, nearRidge));
        if (direction.y < ridgeHeight)
        {
            float ridgeMask = smoothstep(ridgeHeight + 0.002, ridgeHeight - 0.003, direction.y);
            float nearMask = smoothstep(nearRidge + 0.0025, nearRidge - 0.0025, direction.y);
            float midMask = smoothstep(farRidge + 0.0025, farRidge - 0.0025, direction.y);

            float3 dayRidge = lerp(lerp(float3(0.32, 0.44, 0.60), float3(0.22, 0.33, 0.49), midMask), float3(0.13, 0.22, 0.37), nearMask);
            float3 duskRidge = lerp(lerp(float3(0.36, 0.21, 0.27), float3(0.25, 0.15, 0.23), midMask), float3(0.15, 0.085, 0.16), nearMask);
            float3 nightRidge = lerp(lerp(float3(0.019, 0.030, 0.058), float3(0.014, 0.022, 0.044), midMask), float3(0.008, 0.013, 0.028), nearMask);
            float3 ridgeAlbedo = env.IsLunarDisk ? nightRidge : lerp(duskRidge, dayRidge, saturate(env.SunAtmosphereFactor * 1.15));

            float mistBlend = lerp(lerp(0.72, 0.50, midMask), 0.28, nearMask);
            mistBlend = saturate(mistBlend + smoothstep(0.020, -0.015, direction.y) * 0.22);
            float downDarken = lerp(1.0, 0.35, saturate(-direction.y * 2.5));
            float3 ridgeColor = lerp(ridgeAlbedo * downDarken, env.SkyHorizonColor * downDarken, mistBlend);
            gradient = lerp(gradient, ridgeColor, ridgeMask);
        }
    }

    return gradient;
}

float3 EvaluateMarbleColor(float2 uv, float contrast, float3 baseAlbedo)
{
    float warp = FbmNoise2D(uv * 1.6);
    float fine = FbmNoise2D(uv * 4.5 + warp * 1.8);
    float wave = abs(sin((uv.x * 1.1 + uv.y * 0.7 + warp * 3.0 + fine * 0.8) * PI));
    float whiteVein = pow(saturate(1.0 - wave), 14.0) * contrast;
    float goldThread = pow(saturate(1.0 - abs(wave - 0.14) * 8.0), 10.0) * contrast;

    float3 color = lerp(baseAlbedo, float3(0.72, 0.71, 0.68), whiteVein * 0.42);

    return lerp(color, float3(0.75, 0.58, 0.28), goldThread * 0.30);
}

void EvaluateProceduralPattern(SceneMaterial material, float3 objectPos, float3 objectNormal, float2 filterWidth,
                               inout float3 albedo, inout float roughness)
{
    if (material.PatternType == PatternSolid)
    {
        return;
    }

    float2 uv = TriplanarCoords(objectPos, objectNormal, material.PatternScale);
    float fwMax = max(filterWidth.x, filterWidth.y);

    if (material.PatternType == PatternChecker)
    {
        float3 darkTile = float3(0.09, 0.095, 0.105);

        if (fwMax > 0.0)
        {
            float2 w = max(filterWidth, 1e-4);
            float2 checkerSlope = 2.0 * (abs(frac((uv - 0.5 * w) * 0.5) - 0.5) - abs(frac((uv + 0.5 * w) * 0.5) - 0.5)) / w;
            float alt = saturate(0.5 - 0.5 * checkerSlope.x * checkerSlope.y);
            float2 dTile = abs(frac(uv) - 0.5);
            float seam = max(smoothstep(0.482 - w.x * 0.5, 0.482 + w.x * 0.5, dTile.x),
                             smoothstep(0.482 - w.y * 0.5, 0.482 + w.y * 0.5, dTile.y))
                         * saturate(1.0 - fwMax * 2.5);

            float3 tileColor = lerp(material.Albedo, darkTile, alt);
            float tileRough = lerp(roughness, clamp(roughness * 0.75, MinimumRoughness, 1.0), alt);
            albedo = lerp(tileColor, material.Albedo * 0.22, seam);
            roughness = lerp(tileRough, 0.55, seam);
        }
        else
        {
            int2 cell = int2(floor(uv));
            float2 localTile = abs(frac(uv) - 0.5);
            bool isSeam = max(localTile.x, localTile.y) > 0.482;
            bool alt = ((cell.x + cell.y) & 1) != 0;
            if (isSeam)
            {
                albedo = material.Albedo * 0.22;
                roughness = 0.55;
            }
            else if (alt)
            {
                albedo = darkTile;
                roughness = clamp(roughness * 0.75, MinimumRoughness, 1.0);
            }
            else
            {
                albedo = material.Albedo;
            }
        }
    }
    else if (material.PatternType == PatternGrid)
    {
        int2 cell = int2(floor(uv));
        uint tileHash = PCGHash(uint(cell.x) * 73856093u ^ uint(cell.y) * 19349663u);
        float tileTone = lerp(0.91, 1.07, float(tileHash & 0xFFu) * (1.0 / 255.0));
        float subtleGrain = lerp(0.95, 1.05, ValueNoise2D(uv * 4.5));
        float2 tile = abs(frac(uv) - 0.5);

        if (fwMax > 0.0)
        {
            float2 w = max(filterWidth, 1e-4);
            float detailFade = saturate(1.0 - fwMax * 2.0);
            float tone = lerp(0.99, tileTone * subtleGrain, detailFade);
            float grout = max(smoothstep(0.472 - w.x * 0.5, 0.472 + w.x * 0.5, tile.x),
                              smoothstep(0.472 - w.y * 0.5, 0.472 + w.y * 0.5, tile.y));
            grout = lerp(0.055, grout, saturate(1.0 - fwMax * 1.5));
            albedo = lerp(material.Albedo * tone, material.Albedo * 0.36, grout);
            roughness = lerp(roughness, clamp(roughness + 0.18, MinimumRoughness, 1.0), grout);
        }
        else
        {
            bool isGrout = max(tile.x, tile.y) > 0.472;
            if (isGrout)
            {
                albedo = material.Albedo * 0.36;
                roughness = clamp(roughness + 0.18, MinimumRoughness, 1.0);
            }
            else
            {
                albedo = material.Albedo * (tileTone * subtleGrain);
            }
        }
    }
    else if (material.PatternType == PatternStripes)
    {
        float fiber = FbmNoise2D(float2(uv.x * 6.0, uv.y * 0.55));
        float rings = 0.5 + 0.5 * sin((uv.x * 3.5 + fiber * 2.8) * PI);
        if (fwMax > 0.0)
        {
            rings = lerp(0.5, rings, saturate(1.0 - fwMax * 1.8));
        }

        albedo = material.Albedo * lerp(0.76, 1.12, rings);
        roughness = clamp(roughness + (1.0 - rings) * 0.06, MinimumRoughness, 1.0);
    }
    else if (material.PatternType == PatternMarble)
    {
        float contrast = fwMax > 0.0 ? saturate(1.0 - fwMax * 1.6) : 1.0;
        float3 absN = abs(objectNormal);
        if (absN.y < 0.85 && min(absN.x, absN.z) > 0.18)
        {
            float invScale = 1.0 / max(material.PatternScale, 0.001);
            float wx = pow(absN.x, 4.0);
            float wz = pow(absN.z, 4.0);
            wx /= max(wx + wz, 1e-4);

            float3 cx = EvaluateMarbleColor(objectPos.zy * invScale, contrast, material.Albedo);
            float3 cz = EvaluateMarbleColor(objectPos.xy * invScale, contrast, material.Albedo);
            albedo = lerp(cz, cx, wx);
        }
        else
        {
            albedo = EvaluateMarbleColor(uv, contrast, material.Albedo);
        }
    }
    else if (material.PatternType == PatternFluted)
    {
        float rXZ = length(objectPos.xz);
        float yFade = smoothstep(68.0, 84.0, objectPos.y) * (1.0 - smoothstep(512.0, 526.0, objectPos.y));
        if (yFade > 0.001 && rXZ > 5.0)
        {
            float detailFade = fwMax > 0.0 ? saturate(1.0 - fwMax * 0.35) : 1.0;
            float phi = atan2(objectPos.z, objectPos.x);
            float fluteCount = max(material.PatternScale, 12.0);
            float ridge = cos(phi * fluteCount);
            float groove = saturate(0.5 - 0.5 * ridge);
            float grain = lerp(0.96, 1.04, ValueNoise2D(float2(phi * 16.0, objectPos.y * 0.055)));
            albedo = material.Albedo * lerp(1.02, 0.91, groove * yFade * detailFade) * lerp(1.0, grain, detailFade);
            roughness = clamp(roughness + groove * 0.05 * yFade * detailFade, MinimumRoughness, 1.0);
        }
    }
}

void EvaluateSolidMaterialDetail(uint materialId, float3 objectPos, float3 objectNormal, float detailFade,
                                 inout float3 albedo, inout float roughness)
{
    if (detailFade <= 0.005)
    {
        return;
    }

    if (materialId == TerracottaMaterialId)
    {
        float wheel = 0.5 + 0.5 * sin(objectPos.y * 0.55);
        float grain = ValueNoise2D(TriplanarCoords(objectPos, objectNormal, 14.0) * 3.5);
        float dampBase = lerp(0.88, 1.0, smoothstep(0.0, 14.0, objectPos.y));
        float rimPatina = lerp(1.0, 0.94, smoothstep(74.0, 86.0, objectPos.y));
        float tone = lerp(0.94, 1.06, wheel * 0.45 + grain * 0.55) * dampBase * rimPatina;
        albedo *= lerp(1.0, tone, detailFade);
        roughness = clamp(roughness + lerp(-0.02, 0.04, 1.0 - wheel) * detailFade, MinimumRoughness, 1.0);
    }
    else if (materialId == FoliageMaterialId)
    {
        float cluster = FbmNoise2D(TriplanarCoords(objectPos, objectNormal, 8.5)) / 0.875;
        float fineLeaf = ValueNoise2D(TriplanarCoords(objectPos, objectNormal, 3.2));
        float crown = smoothstep(95.0, 195.0, objectPos.y);
        float leafMix = saturate(cluster * 0.55 + fineLeaf * 0.30 + crown * 0.25);
        float3 deepLeaf = albedo * float3(0.74, 0.78, 0.72);
        float3 tipLeaf = albedo * float3(1.16, 1.24, 0.92);
        albedo = lerp(albedo, lerp(deepLeaf, tipLeaf, leafMix), detailFade);
        roughness = clamp(roughness - (leafMix - 0.5) * 0.08 * detailFade, MinimumRoughness, 1.0);
    }
}

float3 EvaluateProceduralNormal(uint materialId, SceneMaterial material, float3 worldPos, float3 objectPos,
                                float3 worldNormal, float3 objectNormal, float elapsedSeconds, float detailFade)
{
    if (detailFade <= 0.005)
    {
        return worldNormal;
    }

    if (materialId == WaterMaterialId)
    {
        float2 p = worldPos.xz * 0.032;
        float2 driftA = float2(elapsedSeconds * 0.038, elapsedSeconds * 0.021);
        float2 driftB = float2(-elapsedSeconds * 0.027, elapsedSeconds * 0.033);
        float eps = 0.08;

        float hX1 = FbmNoise2D(p + driftA + float2(eps, 0.0)) + 0.5 * FbmNoise2D(p * 2.1 + driftB + float2(eps, 0.0));
        float hX0 = FbmNoise2D(p + driftA - float2(eps, 0.0)) + 0.5 * FbmNoise2D(p * 2.1 + driftB - float2(eps, 0.0));
        float hZ1 = FbmNoise2D(p + driftA + float2(0.0, eps)) + 0.5 * FbmNoise2D(p * 2.1 + driftB + float2(0.0, eps));
        float hZ0 = FbmNoise2D(p + driftA - float2(0.0, eps)) + 0.5 * FbmNoise2D(p * 2.1 + driftB - float2(0.0, eps));

        float2 grad = float2(hX1 - hX0, hZ1 - hZ0) * (0.018 * detailFade);

        return normalize(worldNormal - float3(grad.x, 0.0, grad.y));
    }

    if (material.PatternType == PatternFluted)
    {
        float rXZ = length(objectPos.xz);
        float yFade = smoothstep(68.0, 84.0, objectPos.y) * (1.0 - smoothstep(512.0, 526.0, objectPos.y));
        if (yFade > 0.001 && rXZ > 5.0 && abs(worldNormal.y) < 0.7)
        {
            float phi = atan2(objectPos.z, objectPos.x);
            float fluteCount = max(material.PatternScale, 12.0);
            float slope = sin(phi * fluteCount) * (0.28 * yFade * detailFade);
            float3 tangent = normalize(cross(float3(0.0, 1.0, 0.0), worldNormal));

            return normalize(worldNormal + tangent * slope);
        }

        return worldNormal;
    }

    if (material.PatternType == PatternGrid)
    {
        float2 uv = TriplanarCoords(objectPos, objectNormal, material.PatternScale);
        float2 f = frac(uv) - 0.5;
        float2 d = abs(f);
        float bevelBandX = smoothstep(0.456, 0.474, d.x) * (1.0 - smoothstep(0.474, 0.492, d.x));
        float bevelBandY = smoothstep(0.456, 0.474, d.y) * (1.0 - smoothstep(0.474, 0.492, d.y));
        float eps = 0.035;
        float gX = ValueNoise2D(uv * 5.5 + float2(eps, 0.0)) - ValueNoise2D(uv * 5.5 - float2(eps, 0.0));
        float gY = ValueNoise2D(uv * 5.5 + float2(0.0, eps)) - ValueNoise2D(uv * 5.5 - float2(0.0, eps));
        float2 bump = float2(sign(f.x) * bevelBandX * 0.14 + gX * 0.09, sign(f.y) * bevelBandY * 0.14 + gY * 0.09) * detailFade;

        if (abs(worldNormal.y) >= 0.75)
        {
            return normalize(worldNormal - float3(bump.x, 0.0, bump.y));
        }

        float3 t;
        float3 b;
        BuildOrthonormalBasis(worldNormal, t, b);

        return normalize(worldNormal - (t * bump.x + b * bump.y) * 0.5);
    }

    if (materialId == FoliageMaterialId || materialId == TerracottaMaterialId || materialId == LimestoneMaterialId)
    {
        float scale = 42.0;
        float amp = 0.045;
        if (materialId == FoliageMaterialId)
        {
            scale = 4.2;
            amp = 0.11;
        }
        else if (materialId == TerracottaMaterialId)
        {
            scale = 14.0;
            amp = 0.032;
        }

        float2 uv = TriplanarCoords(objectPos, objectNormal, scale);
        float eps = 0.045;
        float gX = ValueNoise2D(uv + float2(eps, 0.0)) - ValueNoise2D(uv - float2(eps, 0.0));
        float gY = ValueNoise2D(uv + float2(0.0, eps)) - ValueNoise2D(uv - float2(0.0, eps));
        float3 t;
        float3 b;
        BuildOrthonormalBasis(worldNormal, t, b);

        return normalize(worldNormal - (t * gX + b * gY) * (amp * detailFade));
    }

    return worldNormal;
}

float3 EvaluateMaterialEmission(uint materialId, SceneMaterial material, SceneEnvironment env)
{
    if (materialId == CeilingLightMaterialId)
    {
        float baseLen = max(length(DefaultCeilingLightRadiance), 1e-4);
        float scale = length(env.CeilingLightRadiance) / baseLen;

        return material.Albedo * (material.Emission * scale);
    }

    if (materialId == LanternCoreMaterialId)
    {
        return float3(1.00, 0.66, 0.28) * (material.Emission * env.NightLightIntensity);
    }

    if (materialId == RecessedStripMaterialId)
    {
        return float3(1.00, 0.72, 0.36) * (material.Emission * env.NightLightIntensity);
    }

    return material.Albedo * material.Emission;
}

float DistributionGGX(float NdotH, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;

    return a2 / max(PI * denom * denom, 1e-6);
}

float VisibilitySmithGGX(float NdotV, float NdotL, float roughness)
{
    float a = max(roughness * roughness, 1e-4);
    float a2 = a * a;
    float visV = NdotL * sqrt(NdotV * NdotV * (1.0 - a2) + a2);
    float visL = NdotV * sqrt(NdotL * NdotL * (1.0 - a2) + a2);

    return 0.5 / max(visV + visL, 1e-5);
}

float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float DielectricFresnelSchlick(float cosTheta, float ior)
{
    float r0 = (1.0 - ior) / (1.0 + ior);
    r0 = r0 * r0;

    return r0 + (1.0 - r0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float3 EstimateEnvSpecularAlbedo(float3 specularColor, float roughness, float NdotV)
{
    float alpha = roughness * roughness;
    float NoV = saturate(abs(NdotV));
    float4 X = float4(1.0, NoV, NoV * NoV, NoV * NoV * NoV);
    float4 Y = float4(1.0, alpha, alpha * alpha, alpha * alpha * alpha);
    float2x2 M1 = float2x2(0.99044, -1.28514, 1.29678, -0.755907);
    float3x3 M2 = float3x3(1.0, 2.92338, 59.4188, 20.3225, -27.0302, 222.592, 121.563, 626.13, 316.627);
    float2x2 M3 = float2x2(0.0365463, 3.32707, 9.0632, -9.04756);
    float3x3 M4 = float3x3(1.0, 3.59685, -1.36772, 9.04401, -16.3174, 9.22949, 5.56589, 19.7886, -20.2123);
    float bias = dot(mul(M1, X.xy), Y.xy) / max(dot(mul(M2, X.xyw), Y.xyw), 1e-4);
    float scale = dot(mul(M3, X.xy), Y.xy) / max(dot(mul(M4, X.xzw), Y.xyw), 1e-4);
    bias *= saturate(specularColor.g * 50.0);

    return specularColor * max(0.0, scale) + max(0.0, bias);
}

float3 EvaluateDirectSunSpecular(SceneEnvironment env, float roughness, float metallic, float3 albedo, float3 normal, float3 viewDir, float ior = 1.5, bool useDielectricIor = false)
{
    float NdotL = saturate(dot(normal, env.KeyLightDirection));
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float NdotV = max(dot(normal, viewDir), 1e-3);
    float3 H = normalize(viewDir + env.KeyLightDirection);
    float NdotH = max(dot(normal, H), 0.0);
    float HdotV = max(dot(H, viewDir), 1e-4);

    float r0 = (1.0 - ior) / (1.0 + ior);
    float3 F0 = useDielectricIor ? float3(r0 * r0) : lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float D = DistributionGGX(NdotH, roughness);
    float Vis = VisibilitySmithGGX(NdotV, NdotL, roughness);
    float3 F = FresnelSchlick(HdotV, F0);

    return (D * Vis * F) * NdotL * env.KeyLightIrradiance;
}

float EvaluateOrenNayar(float NdotL, float NdotV, float LdotV, float roughness)
{
    float sigma2 = roughness * roughness;
    float s = LdotV - NdotL * NdotV;
    float t = s > 0.0 ? max(NdotL, NdotV) : 1.0;
    float A = 1.0 - 0.5 * (sigma2 / (sigma2 + 0.33)) + 0.17 * (sigma2 / (sigma2 + 0.13));
    float B = 0.45 * (sigma2 / (sigma2 + 0.09));

    return max(0.0, A + B * s / max(t, 1e-4));
}

float3 EvaluateDirectLightBrdf(float3 L, float NdotL, float NdotV, float3 normal, float3 viewDir, float3 F0,
                              float3 albedo, float diffuseRoughness, float specRoughness, float metallic,
                              float clearcoat, float clearcoatRoughness)
{
    float3 H = normalize(viewDir + L);
    float NdotH = max(dot(normal, H), 0.0);
    float HdotV = max(dot(H, viewDir), 1e-4);

    float D = DistributionGGX(NdotH, specRoughness);
    float Vis = VisibilitySmithGGX(NdotV, NdotL, specRoughness);
    float3 F = FresnelSchlick(HdotV, F0);
    float3 msScale = 1.0 + F0 * (specRoughness * specRoughness * 0.45) * metallic;
    float3 specular = (D * Vis * F) * msScale;
    float orenNayar = EvaluateOrenNayar(NdotL, NdotV, dot(L, viewDir), diffuseRoughness);
    float3 diffuse = (1.0 - F) * (1.0 - metallic) * albedo * (InvPI * orenNayar);

    float3 coatSpec = 0.0;
    if (clearcoat > 0.0)
    {
        float Dc = DistributionGGX(NdotH, clearcoatRoughness);
        float Visc = VisibilitySmithGGX(NdotV, NdotL, clearcoatRoughness);
        float Fc = (0.04 + 0.96 * pow(saturate(1.0 - HdotV), 5.0)) * clearcoat;
        coatSpec = float3(Dc * Visc * Fc);
        diffuse *= (1.0 - Fc * 0.5);
    }

    return diffuse + specular + coatSpec;
}

float3 EvaluateDirectSunPBR(SceneEnvironment env, float3 albedo, float roughness, float metallic, float clearcoat, float clearcoatRoughness,
                            float3 normal, float3 viewDir)
{
    float NdotL = saturate(dot(normal, env.KeyLightDirection));
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float NdotV = max(dot(normal, viewDir), 1e-3);
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float3 brdf = EvaluateDirectLightBrdf(env.KeyLightDirection, NdotL, NdotV, normal, viewDir, F0,
                                          albedo, roughness, roughness, metallic, clearcoat, clearcoatRoughness);

    return brdf * NdotL * env.KeyLightIrradiance;
}

float3 EvaluateClippedQuadIrradiance(SceneEnvironment env, float3 worldPos, float3 normal)
{
    if (worldPos.y >= CeilingLightCenter.y - 0.1)
    {
        return 0.0;
    }

    float3 polyIn[5];
    polyIn[0] = CeilingLightCenter + float3(-CeilingLightHalfSize.x, 0.0, -CeilingLightHalfSize.y) - worldPos;
    polyIn[1] = CeilingLightCenter + float3(-CeilingLightHalfSize.x, 0.0,  CeilingLightHalfSize.y) - worldPos;
    polyIn[2] = CeilingLightCenter + float3( CeilingLightHalfSize.x, 0.0,  CeilingLightHalfSize.y) - worldPos;
    polyIn[3] = CeilingLightCenter + float3( CeilingLightHalfSize.x, 0.0, -CeilingLightHalfSize.y) - worldPos;
    polyIn[4] = 0.0;

    float3 polyOut[5];
    int outCount = 0;

    for (int i = 0; i < 4; i++)
    {
        float3 a = polyIn[i];
        float3 b = polyIn[(i + 1) & 3];
        float da = dot(a, normal);
        float db = dot(b, normal);

        if (da >= 0.0)
        {
            polyOut[outCount++] = a;
            if (db < 0.0)
            {
                polyOut[outCount++] = lerp(a, b, da / (da - db));
            }
        }
        else if (db >= 0.0)
        {
            polyOut[outCount++] = lerp(a, b, da / (da - db));
        }
    }

    if (outCount < 3)
    {
        return 0.0;
    }

    for (int i = 0; i < outCount; i++)
    {
        polyOut[i] = normalize(polyOut[i]);
    }

    float3 sum = 0.0;
    for (int i = 0; i < outCount; i++)
    {
        float3 a = polyOut[i];
        float3 b = polyOut[(i + 1 < outCount) ? (i + 1) : 0];
        float3 c = cross(a, b);
        float sinTheta = length(c);
        if (sinTheta > 1e-5)
        {
            float cosTheta = clamp(dot(a, b), -1.0, 1.0);
            sum += c * (atan2(sinTheta, cosTheta) / sinTheta);
        }
    }

    float formFactor = max(0.0, dot(sum, normal)) * (0.5 * InvPI);

    return env.CeilingLightRadiance * formFactor;
}

float SoftClampSymmetric(float x, float halfExtent, float softness)
{
    float s2 = softness * softness;
    float dPos = x + halfExtent;
    float dNeg = x - halfExtent;

    return 0.5 * (sqrt(dPos * dPos + s2) - sqrt(dNeg * dNeg + s2));
}

float3 EvaluateAreaLightSpecular(SceneEnvironment env, float3 worldPos, float3 normal, float3 viewDir, float roughness, float3 F0)
{
    float deltaY = CeilingLightCenter.y - worldPos.y;
    if (deltaY <= 0.05)
    {
        return 0.0;
    }

    float3 R = reflect(-viewDir, normal);
    float3 toCenter = CeilingLightCenter - worldPos;
    float centerDist = length(toCenter);
    float maxRayDist = centerDist + 125.0;

    float dotCR = dot(toCenter, R);
    float tClosest = 0.5 * (dotCR + sqrt(dotCR * dotCR + 900.0));

    float rMin = saturate(deltaY / maxRayDist);
    float rMax = min(rMin + 0.35, 0.98);
    float planeWeight = smoothstep(rMin, max(rMax, rMin + 0.05), R.y);
    float tPlane = deltaY / max(R.y, max(rMin, 1e-3));
    float t = lerp(tClosest, tPlane, planeWeight);

    float3 rawHit = worldPos + R * t;
    float edgeSoftness = roughness * roughness * 24.0 + 0.25;
    float3 hitPoint;
    hitPoint.x = CeilingLightCenter.x + SoftClampSymmetric(rawHit.x - CeilingLightCenter.x, CeilingLightHalfSize.x, edgeSoftness);
    hitPoint.z = CeilingLightCenter.z + SoftClampSymmetric(rawHit.z - CeilingLightCenter.z, CeilingLightHalfSize.y, edgeSoftness);
    hitPoint.y = CeilingLightCenter.y;

    float3 toLight = hitPoint - worldPos;
    float distSq = max(dot(toLight, toLight), 1.0);
    float dist = sqrt(distSq);
    float3 L = toLight / dist;

    float NdotL = saturate(dot(normal, L));
    if (NdotL <= 0.0 || L.y <= 0.0)
    {
        return 0.0;
    }

    float NdotV = max(dot(normal, viewDir), 1e-3);
    float3 H = normalize(viewDir + L);
    float NdotH = max(dot(normal, H), 0.0);
    float HdotV = max(dot(H, viewDir), 1e-4);

    float lightSinTheta = saturate(99.1 / dist);
    float effectiveRoughness = clamp(sqrt(roughness * roughness + 0.5 * lightSinTheta), MinimumRoughness, 1.0);
    float D = DistributionGGX(NdotH, effectiveRoughness);
    float Vis = VisibilitySmithGGX(NdotV, NdotL, effectiveRoughness);
    float3 F = FresnelSchlick(HdotV, F0);
    float heightFade = saturate((deltaY - 0.05) * 0.25);
    float solidAngle = min(CeilingLightArea * L.y / distSq, TwoPI) * heightFade;
    float specWeight = min(D * Vis * NdotL * solidAngle, 1.0);

    return specWeight * F * env.CeilingLightRadiance;
}

float2 ComputeDDGIAtlasUV(uint probeIndex, float3 dir, uint interiorSize, float2 atlasSize)
{
    uint2 tile = uint2(probeIndex % DDGIAtlasColumns, probeIndex / DDGIAtlasColumns);
    float tileSize = float(interiorSize + 2u);
    float2 octUV = OctEncode(dir) * 0.5 + 0.5;
    float2 pixelPos = float2(tile) * tileSize + 1.0 + octUV * float(interiorSize);

    return pixelPos / atlasSize;
}

float3 SampleDDGICascade(float3 worldPos, float3 normal, float3 viewDir,
                         float3 origin, float3 spacing, int3 counts, uint probeOffset,
                         Texture2D<float4> irradianceAtlas, Texture2D<float2> distanceAtlas,
                         SamplerState linearSampler, out float confidence)
{
    float3 bias = (normal * 0.22 + viewDir * 0.78) * (min(spacing.x, min(spacing.y, spacing.z)) * 0.16);
    float3 biasedPos = worldPos + bias;

    float3 gridCoord = (biasedPos - origin) / spacing;
    float3 clampedCoord = clamp(gridCoord, float3(0.0, 0.0, 0.0), float3(counts - 1) - 1e-4);
    int3 baseIdx = int3(floor(clampedCoord));
    float3 alpha = clampedCoord - float3(baseIdx);

    float3 sumIrradiance = 0.0;
    float sumWeight = 0.0;

    for (int i = 0; i < 8; i++)
    {
        int3 offset = int3(i & 1, (i >> 1) & 1, (i >> 2) & 1);
        int3 probeCoord = clamp(baseIdx + offset, int3(0, 0, 0), counts - 1);
        uint probeIndex = probeOffset + uint((probeCoord.z * counts.y + probeCoord.y) * counts.x + probeCoord.x);

        float3 probePos = origin + float3(probeCoord) * spacing;
        float3 toProbe = probePos - worldPos;
        float distToBiased = length(probePos - biasedPos);
        float3 dirToProbe = normalize(toProbe);
        float3 dirFromProbe = normalize(biasedPos - probePos);

        float3 trilinearVec = select(offset == 0, 1.0 - alpha, alpha);
        float weight = trilinearVec.x * trilinearVec.y * trilinearVec.z;

        float wrapCos = saturate((dot(dirToProbe, normal) + 1.0) * 0.5);
        weight *= (wrapCos * wrapCos) + 0.05;

        float2 distUV = ComputeDDGIAtlasUV(probeIndex, dirFromProbe, 14u, DDGIDistanceAtlasSize);
        float2 meanStats = distanceAtlas.SampleLevel(linearSampler, distUV, 0).xy;
        float meanDist = meanStats.x;
        if (distToBiased > meanDist)
        {
            float variance = max(abs(meanStats.y - meanDist * meanDist), 0.01);
            float diff = distToBiased - meanDist;
            float chebyshev = variance / (variance + diff * diff);
            weight *= max(chebyshev * chebyshev * chebyshev, 0.0);
        }

        float2 irrUV = ComputeDDGIAtlasUV(probeIndex, normal, 6u, DDGIIrradianceAtlasSize);
        float4 probeIrr = irradianceAtlas.SampleLevel(linearSampler, irrUV, 0);
        weight *= probeIrr.w;

        if (weight > 1e-5)
        {
            sumIrradiance += sqrt(max(probeIrr.rgb, 0.0)) * weight;
            sumWeight += weight;
        }
    }

    confidence = saturate(sumWeight);
    if (sumWeight <= 1e-5)
    {
        return 0.0;
    }

    float3 result = sumIrradiance / sumWeight;

    return result * result;
}

float3 SampleDDGIIrradiance(float3 worldPos, float3 normal, float3 viewDir,
                            Texture2D<float4> irradianceAtlas, Texture2D<float2> distanceAtlas,
                            SamplerState linearSampler)
{
    bool insideSanctuary = abs(worldPos.x) <= 295.0 && worldPos.y >= -5.0 && worldPos.y <= 562.0 && worldPos.z >= -5.0 && worldPos.z <= 565.0;
    if (insideSanctuary)
    {
        float conf0;
        return SampleDDGICascade(worldPos, normal, viewDir,
                                 DDGICascade0Origin, DDGICascade0Spacing, DDGICascade0Counts, 0u,
                                 irradianceAtlas, distanceAtlas, linearSampler, conf0);
    }

    float3 cascade0Max = DDGICascade0Origin + float3(DDGICascade0Counts - 1) * DDGICascade0Spacing;
    float3 marginMin = worldPos - DDGICascade0Origin;
    float3 marginMax = cascade0Max - worldPos;
    float minMargin = min(min(marginMin.x, marginMax.x), min(min(marginMin.y, marginMax.y), min(marginMin.z, marginMax.z)));
    float blend0 = smoothstep(-20.0, 80.0, minMargin);

    if (blend0 >= 0.999)
    {
        float conf0;
        float3 irr0 = SampleDDGICascade(worldPos, normal, viewDir,
                                        DDGICascade0Origin, DDGICascade0Spacing, DDGICascade0Counts, 0u,
                                        irradianceAtlas, distanceAtlas, linearSampler, conf0);
        if (conf0 > 0.05)
        {
            return irr0;
        }
    }

    if (blend0 <= 0.001)
    {
        float conf1;
        return SampleDDGICascade(worldPos, normal, viewDir,
                                 DDGICascade1Origin, DDGICascade1Spacing, DDGICascade1Counts, DDGICascade0Probes,
                                 irradianceAtlas, distanceAtlas, linearSampler, conf1);
    }

    float conf0;
    float conf1;
    float3 irr0 = SampleDDGICascade(worldPos, normal, viewDir,
                                    DDGICascade0Origin, DDGICascade0Spacing, DDGICascade0Counts, 0u,
                                    irradianceAtlas, distanceAtlas, linearSampler, conf0);
    float3 irr1 = SampleDDGICascade(worldPos, normal, viewDir,
                                    DDGICascade1Origin, DDGICascade1Spacing, DDGICascade1Counts, DDGICascade0Probes,
                                    irradianceAtlas, distanceAtlas, linearSampler, conf1);

    float effectiveBlend = conf0 > 0.02 ? blend0 : 0.0;

    return lerp(irr1, irr0, effectiveBlend);
}

bool TraceVisibilityRay(RaytracingAccelerationStructure scene, float3 origin, float3 direction, float maxDistance)
{
    if (maxDistance <= RayMinDistance + RayOffset)
    {
        return true;
    }

    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = RayMinDistance;
    ray.TMax = maxDistance - RayOffset;

    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> query;
    query.TraceRayInline(scene, RAY_FLAG_NONE, RayMaskOpaque, ray);
    query.Proceed();

    return query.CommittedStatus() == COMMITTED_NOTHING;
}

bool TraceClosestHit(RaytracingAccelerationStructure scene, float3 origin, float3 direction, uint instanceMask,
                     out uint instanceIndex, out uint primitiveIndex, out float2 barycentrics, out float hitDistance)
{
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = RayMinDistance;
    ray.TMax = RayMaxDistance;

    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> query;
    query.TraceRayInline(scene, RAY_FLAG_NONE, instanceMask, ray);
    query.Proceed();

    if (query.CommittedStatus() == COMMITTED_NOTHING)
    {
        instanceIndex = InvalidIndex;
        primitiveIndex = 0u;
        barycentrics = 0.0;
        hitDistance = RayMaxDistance;

        return false;
    }

    instanceIndex = query.CommittedInstanceIndex();
    primitiveIndex = query.CommittedPrimitiveIndex();
    barycentrics = query.CommittedTriangleBarycentrics();
    hitDistance = query.CommittedRayT();

    return true;
}

GeometryHit ReconstructGeometryHit(uint instanceIndex, uint primitiveIndex, float2 barycentrics,
                                   StructuredBuffer<SceneVertex> vertices, StructuredBuffer<uint> indices,
                                   StructuredBuffer<SceneInstance> instances)
{
    SceneInstance instance = instances[instanceIndex];
    uint firstIndex = instance.FirstIndex + primitiveIndex * 3u;

    SceneVertex v0 = vertices[indices[firstIndex + 0u]];
    SceneVertex v1 = vertices[indices[firstIndex + 1u]];
    SceneVertex v2 = vertices[indices[firstIndex + 2u]];

    float3 weights = float3(1.0 - barycentrics.x - barycentrics.y, barycentrics.x, barycentrics.y);
    float3 localPos = v0.Position * weights.x + v1.Position * weights.y + v2.Position * weights.z;
    float3 localNormal = normalize(v0.Normal * weights.x + v1.Normal * weights.y + v2.Normal * weights.z);
    float3 localFaceNormal = normalize(cross(v1.Position - v0.Position, v2.Position - v0.Position));

    if (dot(localFaceNormal, localNormal) < 0.0)
    {
        localFaceNormal = -localFaceNormal;
    }

    GeometryHit hit;
    hit.Position = mul(float4(localPos, 1.0), instance.ObjectToWorld).xyz;
    hit.ObjectPosition = localPos;
    hit.MaterialId = instance.MaterialOverride != InvalidIndex ? instance.MaterialOverride : v0.MaterialId;
    hit.IsDynamic = instance.IsDynamic;
    hit.Normal = normalize(mul(float4(localNormal, 0.0), instance.ObjectToWorld).xyz);
    hit.LocalNormal = localNormal;
    hit.GeometricNormal = normalize(mul(float4(localFaceNormal, 0.0), instance.ObjectToWorld).xyz);

    return hit;
}

float3 EvaluateNightAccentIrradiance(SceneEnvironment env, float3 worldPos, float3 normal)
{
    if (env.NightLightIntensity <= 0.001)
    {
        return 0.0;
    }

    float3 accent = 0.0;
    float ax = abs(worldPos.x);

    if (worldPos.y > -28.0 && worldPos.y < -12.0)
    {
        if (ax > 362.0 && ax < 454.0 && worldPos.z > -188.0 && worldPos.z < 588.0)
        {
            float rimDist = min(abs(ax - 370.5), abs(ax - 445.5));
            float rimGlow = exp(-rimDist * 0.14);
            float centerDist = abs(ax - 408.0);
            float poolDepthWash = saturate(1.0 - centerDist / 38.0) * saturate((worldPos.y + 24.5) * 0.5);
            accent += float3(1.00, 0.70, 0.34) * (rimGlow * 0.42) + float3(0.20, 0.68, 0.72) * (poolDepthWash * 0.16);
        }

        if (ax > 484.0 && ax < 612.0 && worldPos.z > -848.0 && worldPos.z < -532.0)
        {
            float rimDist = min(abs(ax - 492.5), abs(ax - 603.5));
            float rimGlow = exp(-rimDist * 0.12);
            float centerDist = abs(ax - 548.0);
            float poolDepthWash = saturate(1.0 - centerDist / 56.0) * saturate((worldPos.y + 24.5) * 0.5);
            accent += float3(1.00, 0.70, 0.34) * (rimGlow * 0.40) + float3(0.20, 0.68, 0.72) * (poolDepthWash * 0.14);
        }
    }

    if (ax < 268.0 && worldPos.z > -206.0 && worldPos.z < -16.0 && worldPos.y > -28.0 && worldPos.y < 6.0)
    {
        for (int i = 0; i < 3; i++)
        {
            float yTop = -20.25 + float(i) * 6.75;
            float zFront = -178.0 + float(i) * 52.0;
            float halfW = 264.0 - float(i) * 12.0;
            if (ax <= halfW + 4.0)
            {
                float dy = worldPos.y - (yTop - 0.5);
                float dz = worldPos.z - (zFront - 0.5);
                float d = sqrt(dy * dy + dz * dz);
                if (d < 22.0)
                {
                    float3 dirToStrip = float3(0.0, yTop - 0.5 - worldPos.y, zFront - 0.5 - worldPos.z) / max(d, 0.5);
                    float nDot = saturate(dot(normal, dirToStrip) * 0.6 + 0.4);
                    accent += float3(1.00, 0.72, 0.36) * (exp(-d * 0.20) * 0.75 * nDot);
                }
            }
        }
    }

    return accent * env.NightLightIntensity;
}

float ComputeLanternAttenuation(float distSq, out float invDist)
{
    if (distSq >= NightLanternRange * NightLanternRange || distSq < 144.0)
    {
        invDist = 0.0;

        return 0.0;
    }

    invDist = rsqrt(distSq);
    float dist = distSq * invDist;
    float rangeFade = saturate(1.0 - dist / NightLanternRange);
    float coreFade = saturate((dist - 12.0) / 8.0);

    return (rangeFade * rangeFade) * coreFade / (distSq + 1200.0);
}

float3 EvaluateSingleLanternBrdf(float3 L, float NdotL, float atten, float dist,
                                   float3 shadingNormal, float3 viewDir, float NdotV, float3 F0,
                                   float3 albedo, float roughness, float metallic, float clearcoat, float clearcoatRoughness)
{
    float lightSinTheta = saturate(11.5 / dist);
    float effRough = clamp(sqrt(roughness * roughness + 0.5 * lightSinTheta), MinimumRoughness, 1.0);
    float effCoatRough = clamp(sqrt(clearcoatRoughness * clearcoatRoughness + 0.5 * lightSinTheta), MinimumRoughness, 1.0);
    float3 brdf = EvaluateDirectLightBrdf(L, NdotL, NdotV, shadingNormal, viewDir, F0,
                                          albedo, roughness, effRough, metallic, clearcoat, effCoatRough);

    return brdf * (NdotL * atten);
}

float3 EvaluateNightLightsPBR(SceneEnvironment env,
                              float3 worldPos, float3 shadingNormal, float3 viewDir,
                              float3 albedo, float roughness, float metallic, float clearcoat, float clearcoatRoughness,
                              float nightVis)
{
    if (env.NightLightIntensity <= 0.001)
    {
        return 0.0;
    }

    float3 accumLight = 0.0;
    if (nightVis > 0.001)
    {
        int bestIdx0 = -1;
        float bestScore0 = 0.0;
        float3 bestL0 = 0.0;
        float bestAtten0 = 0.0;
        float bestNdotL0 = 0.0;
        float bestDist0 = 0.0;
        float tailDiffuseSum = 0.0;

        for (uint i = 0u; i < NightLanternCount; i++)
        {
            float3 toLight = NightLanternPositions[i] - worldPos;
            float distSq = dot(toLight, toLight);
            float invDist;
            float atten = ComputeLanternAttenuation(distSq, invDist);
            if (atten <= 0.0)
            {
                continue;
            }

            float dist = distSq * invDist;
            float3 L = toLight * invDist;
            float NdotL = saturate(dot(shadingNormal, L));
            if (NdotL <= 0.0)
            {
                continue;
            }

            float contribution = atten * NdotL;
            tailDiffuseSum += contribution;
            if (contribution > bestScore0)
            {
                bestScore0 = contribution;
                bestIdx0 = int(i);
                bestL0 = L;
                bestAtten0 = atten;
                bestNdotL0 = NdotL;
                bestDist0 = dist;
            }
        }

        if (bestIdx0 >= 0)
        {
            float NdotV = max(dot(shadingNormal, viewDir), 1e-3);
            float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
            float3 lanternColor = float3(1.00, 0.64, 0.28) * (38000.0 * env.NightLightIntensity);

            float3 brdf = EvaluateSingleLanternBrdf(bestL0, bestNdotL0, bestAtten0, bestDist0,
                                                    shadingNormal, viewDir, NdotV, F0,
                                                    albedo, roughness, metallic, clearcoat, clearcoatRoughness);
            float3 tailDiffuse = max(tailDiffuseSum - bestAtten0 * bestNdotL0, 0.0) * (1.0 - metallic) * albedo * InvPI;
            accumLight = (brdf + tailDiffuse) * (nightVis * lanternColor);
        }
    }

    float3 accent = EvaluateNightAccentIrradiance(env, worldPos, shadingNormal);
    if (dot(accent, accent) > 1e-6)
    {
        accumLight += accent * (albedo * (1.0 - metallic) * InvPI);
    }

    return accumLight;
}

float3 EvaluateNightLightsSecondary(SceneEnvironment env, float3 worldPos, float3 shadingNormal, float3 diffuseAlbedo)
{
    if (env.NightLightIntensity <= 0.001)
    {
        return 0.0;
    }

    float interiorDepth = min(min(289.0 - abs(worldPos.x), worldPos.z - 2.0), min(558.0 - worldPos.z, 556.0 - worldPos.y));
    float exteriorFactor = worldPos.y >= -1.0 ? (1.0 - smoothstep(0.0, 96.0, interiorDepth)) : 1.0;
    if (exteriorFactor <= 0.001)
    {
        return 0.0;
    }

    float diffuseSum = 0.0;
    for (uint i = 0u; i < NightLanternCount; i++)
    {
        float3 toLight = NightLanternPositions[i] - worldPos;
        float distSq = dot(toLight, toLight);
        float invDist;
        float atten = ComputeLanternAttenuation(distSq, invDist);
        if (atten > 0.0)
        {
            float NdotL = saturate(dot(shadingNormal, toLight * invDist));
            diffuseSum += atten * NdotL;
        }
    }

    diffuseSum *= exteriorFactor;
    float3 lanternIrr = float3(1.00, 0.64, 0.28) * (38000.0 * env.NightLightIntensity * diffuseSum * InvPI);
    float3 accentIrr = EvaluateNightAccentIrradiance(env, worldPos, shadingNormal) * InvPI;

    return diffuseAlbedo * (lanternIrr + accentIrr);
}

float3 EvaluateSphereLensCaustic(float3 lightDir, float3 worldPos, float3 normal,
                                 float3 lensCenter, float lensRadius,
                                 float3 causticTint, float3 lightIrradiance,
                                 float focusGain, float rimShadowDepth,
                                 float minProj, float fadeInEnd, float maxProjStart, float maxProjEnd)
{
    float NdotL = saturate(dot(normal, lightDir));
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float3 toLens = lensCenter - worldPos;
    float proj = dot(toLens, lightDir);
    if (proj <= minProj || proj >= maxProjEnd)
    {
        return 0.0;
    }

    float axialWeight = smoothstep(minProj, fadeInEnd, proj) * (1.0 - smoothstep(maxProjStart, maxProjEnd, proj));
    float perpSq = max(dot(toLens, toLens) - proj * proj, 0.0);
    float r2 = perpSq / (lensRadius * lensRadius);
    if (r2 >= 1.85)
    {
        return 0.0;
    }

    float radialFade = 1.0 - smoothstep(0.95, 1.85, r2);
    float focus = focusGain * exp(-r2 / 0.105);
    float rimShadow = rimShadowDepth * exp(-pow((sqrt(max(r2, 1e-6)) - 0.78) / 0.28, 2.0));

    return (causticTint * focus - rimShadow) * (axialWeight * radialFade * NdotL) * lightIrradiance;
}

float3 EvaluateGlassCaustics(SceneEnvironment env, float3 worldPos, float3 normal,
                             float sunVis, float areaVis, float3 diffuseReflectance)
{
    float3 causticIrradiance = 0.0;

    if (areaVis > 0.001 && worldPos.y < GlassSphereCenter.y)
    {
        float3 toLight = CeilingLightCenter - worldPos;
        float distSq = max(dot(toLight, toLight), 1.0);
        float dist = sqrt(distSq);
        float3 L = toLight / dist;
        float3 ceilIrr = env.CeilingLightRadiance * (CeilingLightArea * saturate(L.y) / max(PI * distSq, 1.0));
        causticIrradiance += EvaluateSphereLensCaustic(L, worldPos, normal, GlassSphereCenter, GlassSphereRadius,
                                                       float3(1.00, 0.97, 0.92), ceilIrr * areaVis,
                                                       3.20, 0.62, 8.0, 26.0, 72.0, 155.0);
    }

    if (sunVis > 0.001)
    {
        float3 keyDir = env.KeyLightDirection;
        float3 sunIrr = env.KeyLightIrradiance * (sunVis * InvPI);
        causticIrradiance += EvaluateSphereLensCaustic(keyDir, worldPos, normal, GlassSphereCenter, GlassSphereRadius,
                                                       float3(0.98, 0.99, 1.02), sunIrr,
                                                       2.40, 0.48, 8.0, 24.0, 140.0, 360.0);
        causticIrradiance += EvaluateSphereLensCaustic(keyDir, worldPos, normal, EmeraldUrnCenter, EmeraldUrnRadius,
                                                       float3(0.36, 0.94, 0.64), sunIrr,
                                                       2.20, 0.42, 8.0, 24.0, 180.0, 380.0);
    }

    return diffuseReflectance * causticIrradiance;
}

float3 EvaluateWaterSubsurface(SceneEnvironment env, float3 worldPos, float3 normal, float3 viewDir, float sunVis)
{
    float3 refrDir = refract(-viewDir, normal, 0.75);
    float tFloor = 4.5 / max(-refrDir.y, 0.25);
    float2 floorXZ = worldPos.xz + refrDir.xz * tFloor;

    float stoneTone = lerp(0.95, 1.05, FbmNoise2D(floorXZ * 0.022) / 0.875);
    float3 basinAlbedo = float3(0.045, 0.062, 0.072) * stoneTone;

    float ax = abs(floorXZ.x);
    float stripDist = ax > 470.0 ? min(abs(ax - 492.5), abs(ax - 603.5)) : min(abs(ax - 370.5), abs(ax - 445.5));
    float stripGlow = exp(-stripDist * 0.11) * env.NightLightIntensity;

    float3 lightIrr = EvaluateSkyAmbient(float3(0.0, 1.0, 0.0), env) * 0.45
                    + env.KeyLightIrradiance * (sunVis * saturate(env.KeyLightDirection.y) * 0.16)
                    + float3(1.00, 0.68, 0.32) * (stripGlow * 0.42);
    float3 transmittance = float3(0.26, 0.56, 0.62) * exp(-tFloor * 0.055);

    return basinAlbedo * lightIrr * transmittance;
}

float3 ApplyAerialPerspective(float3 color, float distanceValue, SceneEnvironment env)
{
    float haze = saturate((distanceValue - 480.0) / 3600.0);
    if (haze <= 0.0)
    {
        return color;
    }

    float fogAmount = (1.0 - exp(-haze * 0.42)) * (env.IsLunarDisk ? 0.35 : 0.26);
    float3 hazeColor = lerp(env.SkyHorizonColor, env.SkyZenithColor, 0.15) * (env.IsLunarDisk ? 0.85 : 0.92);

    return lerp(color, hazeColor, fogAmount);
}
