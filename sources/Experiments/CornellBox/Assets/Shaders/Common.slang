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

static const uint EnvFlagIsPaused = 1u << 0;
static const uint EnvFlagIsLunarDisk = 1u << 1;

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

static const float3 DDGICascade0Origin = float3(-645.0, -12.0, -196.0);
static const float3 DDGICascade0Spacing = float3(86.0, 62.0, 72.0);
static const int3 DDGICascade0Counts = int3(16, 10, 12);

static const float3 DDGICascade1Origin = float3(-1155.0, -12.0, -1150.0);
static const float3 DDGICascade1Spacing = float3(210.0, 128.0, 196.0);
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
            float2 radialDir = objectPos.xz / max(rXZ, 1e-4);
            float grain = lerp(0.96, 1.04, ValueNoise2D(float2(radialDir.x * 11.0 - radialDir.y * 7.0, objectPos.y * 0.055 + radialDir.y * 5.0)));
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
        float2 p = worldPos.xz * 0.036;
        float2 driftA = float2(elapsedSeconds * 0.042, elapsedSeconds * 0.024);
        float2 driftB = float2(-elapsedSeconds * 0.031, elapsedSeconds * 0.036);
        float eps = 0.08;

        float hX1 = FbmNoise2D(p + driftA + float2(eps, 0.0)) + 0.5 * FbmNoise2D(p * 2.15 + driftB + float2(eps, 0.0));
        float hX0 = FbmNoise2D(p + driftA - float2(eps, 0.0)) + 0.5 * FbmNoise2D(p * 2.15 + driftB - float2(eps, 0.0));
        float hZ1 = FbmNoise2D(p + driftA + float2(0.0, eps)) + 0.5 * FbmNoise2D(p * 2.15 + driftB + float2(0.0, eps));
        float hZ0 = FbmNoise2D(p + driftA - float2(0.0, eps)) + 0.5 * FbmNoise2D(p * 2.15 + driftB - float2(0.0, eps));

        float2 grad = float2(hX1 - hX0, hZ1 - hZ0) * ((0.020 / (2.0 * eps)) * detailFade);

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

bool IsCeilingLightPotentiallyVisible(float3 worldPos)
{
    if (worldPos.y >= CeilingLightCenter.y - 0.5)
    {
        return false;
    }

    if (abs(worldPos.x) > 342.0 && worldPos.z > 6.0 && worldPos.z < 558.0 && worldPos.y >= -28.0)
    {
        return false;
    }

    return true;
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
    float3 bias = (normal * 0.75 + viewDir * 0.25) * (min(spacing.x, min(spacing.y, spacing.z)) * 0.14);
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

float3 SampleDDGIIrradiance(float3 worldPos, float3 normal, float3 viewDir, SceneEnvironment env,
                            Texture2D<float4> irradianceAtlas, Texture2D<float2> distanceAtlas,
                            SamplerState linearSampler)
{
    bool insideSanctuary = abs(worldPos.x) <= 295.0 && worldPos.y >= -5.0 && worldPos.y <= 562.0 && worldPos.z >= -5.0 && worldPos.z <= 565.0;
    float3 cascade0Max = DDGICascade0Origin + float3(DDGICascade0Counts - 1) * DDGICascade0Spacing;
    float3 marginMin = worldPos - DDGICascade0Origin;
    float3 marginMax = cascade0Max - worldPos;
    float minMargin = min(min(marginMin.x, marginMax.x), min(marginMax.y, min(marginMin.z, marginMax.z)));
    float blend0 = insideSanctuary ? 1.0 : smoothstep(-20.0, 72.0, minMargin);

    float3 irr0 = 0.0;
    float conf0 = 0.0;
    if (blend0 > 0.001)
    {
        irr0 = SampleDDGICascade(worldPos, normal, viewDir,
                                 DDGICascade0Origin, DDGICascade0Spacing, DDGICascade0Counts, 0u,
                                 irradianceAtlas, distanceAtlas, linearSampler, conf0);
        if (insideSanctuary || (blend0 >= 0.999 && conf0 > 0.05))
        {
            return irr0;
        }
    }

    float conf1 = 0.0;
    float3 irr1 = SampleDDGICascade(worldPos, normal, viewDir,
                                    DDGICascade1Origin, DDGICascade1Spacing, DDGICascade1Counts, DDGICascade0Probes,
                                    irradianceAtlas, distanceAtlas, linearSampler, conf1);

    float w0 = blend0 * saturate(conf0 / 0.05);
    float3 irr = lerp(irr1, irr0, w0);
    float totalConf = lerp(conf1, conf0, w0);

    if (totalConf < 0.08)
    {
        irr = lerp(EvaluateSkyAmbient(normal, env) * 0.16, irr, saturate(totalConf / 0.08));
    }

    return irr;
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

float ComputeLanternAttenuation(float3 toLight, out float3 L, out float dist)
{
    float distSq = dot(toLight, toLight);
    if (distSq >= NightLanternRange * NightLanternRange || distSq < 144.0)
    {
        L = 0.0;
        dist = 0.0;

        return 0.0;
    }

    float invDist = rsqrt(distSq);
    dist = distSq * invDist;
    L = toLight * invDist;

    float rangeFade = saturate(1.0 - dist / NightLanternRange);
    float coreFade = saturate((dist - 12.0) / 8.0);
    float hoodFade = 1.0 - smoothstep(0.32, 0.68, -L.y);

    return (rangeFade * rangeFade) * coreFade * hoodFade / (distSq + 1200.0);
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
        float3 L;
        float dist;
        float atten = ComputeLanternAttenuation(NightLanternPositions[i] - worldPos, L, dist);
        if (atten > 0.0)
        {
            float NdotL = saturate(dot(shadingNormal, L));
            diffuseSum += atten * NdotL;
        }
    }

    diffuseSum *= exteriorFactor;
    float3 lanternIrr = float3(1.00, 0.64, 0.28) * (38000.0 * env.NightLightIntensity * diffuseSum * InvPI);
    float3 accentIrr = EvaluateNightAccentIrradiance(env, worldPos, shadingNormal) * InvPI;

    return diffuseAlbedo * (lanternIrr + accentIrr);
}
