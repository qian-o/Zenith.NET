#include "Common.slang"

static const uint FlagReset = 1u << 0;
static const uint FlagHasReactiveMask = 1u << 1;

struct LightingConstants
{
    float4x4 InverseJitteredViewProjection;

    float4x4 InverseJitteredProjection;

    float4x4 InverseView;

    float4x4 ViewProjection;

    float4x4 PreviousViewProjection;

    private float4 CameraPositionAndFlags;

    private float4 RenderConfig;

    SceneEnvironment Environment;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<SceneVertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<StructuredBuffer<SceneInstance>> Instances;

    DescriptorHandle<Texture2D<float4>> AlbedoAndMaterial;

    DescriptorHandle<Texture2D<float4>> NormalAndRoughness;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float2>> MotionVectors;

    DescriptorHandle<Texture2D<float4>> ShadowAO;

    DescriptorHandle<Texture2D<float4>> IrradianceAtlas;

    DescriptorHandle<Texture2D<float2>> DistanceAtlas;

    DescriptorHandle<SamplerState> LinearSampler;

    DescriptorHandle<Texture2D<float>> PreviousLinearDepth;

    DescriptorHandle<WTexture2D<float4>> Color;

    DescriptorHandle<WTexture2D<float>> ReactiveMask;

    property float3 CameraPosition
    {
        get
        {
            return CameraPositionAndFlags.xyz;
        }
    }

    property uint Flags
    {
        get
        {
            return asuint(CameraPositionAndFlags.w);
        }
    }

    property float2 RenderSize
    {
        get
        {
            return RenderConfig.xy;
        }
    }
};

ConstantBuffer<LightingConstants> lighting;

float ValueNoise3D(float3 p)
{
    float3 fl = floor(p);
    int3 cell = int3(fl);
    float3 f = p - fl;
    float3 u = f * f * (3.0 - 2.0 * f);

    uint bx0 = uint(cell.x) * 374761393u;
    uint bx1 = uint(cell.x + 1) * 374761393u;
    uint by0 = uint(cell.y) * 668265263u;
    uint by1 = uint(cell.y + 1) * 668265263u;
    uint bz0 = uint(cell.z) * 1442695041u;
    uint bz1 = uint(cell.z + 1) * 1442695041u;

    float v000 = float(PCGHash(bx0 + by0 + bz0) & 0xFFFFu) * (1.0 / 65535.0);
    float v100 = float(PCGHash(bx1 + by0 + bz0) & 0xFFFFu) * (1.0 / 65535.0);
    float v010 = float(PCGHash(bx0 + by1 + bz0) & 0xFFFFu) * (1.0 / 65535.0);
    float v110 = float(PCGHash(bx1 + by1 + bz0) & 0xFFFFu) * (1.0 / 65535.0);
    float v001 = float(PCGHash(bx0 + by0 + bz1) & 0xFFFFu) * (1.0 / 65535.0);
    float v101 = float(PCGHash(bx1 + by0 + bz1) & 0xFFFFu) * (1.0 / 65535.0);
    float v011 = float(PCGHash(bx0 + by1 + bz1) & 0xFFFFu) * (1.0 / 65535.0);
    float v111 = float(PCGHash(bx1 + by1 + bz1) & 0xFFFFu) * (1.0 / 65535.0);

    float z0 = lerp(lerp(v000, v100, u.x), lerp(v010, v110, u.x), u.y);
    float z1 = lerp(lerp(v001, v101, u.x), lerp(v011, v111, u.x), u.y);

    return lerp(z0, z1, u.z);
}

float FbmNoise3D(float3 p)
{
    static const float3x3 OctaveTransform = float3x3(
        1.3533, -1.3533,  0.6767,
        0.6767,  1.3533,  1.3533,
        1.3533,  0.6767, -1.3533);

    float sum = 0.0;
    float amp = 0.5;
    float3 q = p;
    for (int i = 0; i < 3; i++)
    {
        sum += ValueNoise3D(q) * amp;
        q = mul(OctaveTransform, q);
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

float3 EvaluateSkyFull(float3 direction, SceneEnvironment env)
{
    float3 keyDir = env.KeyLightDirection;
    float keyAlign = saturate(dot(direction, keyDir));
    float3 gradient = EvaluateSkyAmbient(direction, env);

    if (env.StarIntensity > 0.01 && direction.y > 0.004)
    {
        float horizonVis = smoothstep(0.008, 0.14, direction.y);
        float3 galNormal = normalize(float3(0.32, 0.82, -0.47));
        float galacticY = dot(direction, galNormal);
        if (abs(galacticY) < 0.55)
        {
            float3 galU;
            float3 galV;
            BuildOrthonormalBasis(galNormal, galU, galV);
            float3 galDir = float3(dot(direction, galU), dot(direction, galV), galacticY);
            float band = exp(-galacticY * galacticY * 14.0);
            float neb = FbmNoise3D(galDir * float3(3.6, 3.6, 7.5) + float3(1.7, -2.3, 0.9)) / 0.875;
            float rift = smoothstep(0.42, 0.72, FbmNoise3D(galDir * float3(7.4, 7.4, 14.0) + float3(-4.1, 3.3, 2.6)) / 0.875);
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
        float2 horizDir = normalize(direction.xz + float2(1e-6, 0.0));
        float distantRidge = 0.022 + 0.048 * (FbmNoise2D(horizDir * 2.4 + float2(-1.9, 0.9)) / 0.875);
        float farRidge = 0.010 + 0.042 * (FbmNoise2D(horizDir * 4.4 + float2(1.1, 2.7)) / 0.875);
        float nearRidge = -0.006 + 0.026 * (FbmNoise2D(horizDir * 8.6 + float2(6.4, 8.3)) / 0.875);
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

float3 ComputeDielectricF0(float ior)
{
    float safeIor = max(ior, 1.0);
    float r0 = (1.0 - safeIor) / (1.0 + safeIor);

    return float3(r0 * r0);
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

float3 EvaluateDielectricSpecular(float3 L, float3 normal, float3 viewDir, float roughness, float3 F0)
{
    float NdotL = saturate(dot(normal, L));
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float NdotV = max(dot(normal, viewDir), 1e-3);
    float3 H = normalize(viewDir + L);
    float NdotH = max(dot(normal, H), 0.0);
    float HdotV = max(dot(H, viewDir), 1e-4);

    float D = DistributionGGX(NdotH, roughness);
    float Vis = VisibilitySmithGGX(NdotV, NdotL, roughness);
    float3 F = FresnelSchlick(HdotV, F0);

    return (D * Vis * F) * NdotL;
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

float3 EvaluateDirectSunPBR(SceneEnvironment env, float3 albedo, float3 F0, float roughness, float metallic, float clearcoat, float clearcoatRoughness,
                            float3 normal, float3 viewDir)
{
    float NdotL = saturate(dot(normal, env.KeyLightDirection));
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float NdotV = max(dot(normal, viewDir), 1e-3);
    float3 brdf = EvaluateDirectLightBrdf(env.KeyLightDirection, NdotL, NdotV, normal, viewDir, F0,
                                          albedo, roughness, roughness, metallic, clearcoat, clearcoatRoughness);

    return brdf * NdotL * env.KeyLightIrradiance;
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
                              float3 albedo, float3 F0, float roughness, float metallic, float clearcoat, float clearcoatRoughness,
                              float nightVis)
{
    if (env.NightLightIntensity <= 0.001)
    {
        return 0.0;
    }

    float3 accumLight = 0.0;
    if (nightVis > 0.001)
    {
        float NdotV = max(dot(shadingNormal, viewDir), 1e-3);
        float3 lanternColor = float3(1.00, 0.64, 0.28) * (38000.0 * env.NightLightIntensity);
        float3 lanternSum = 0.0;

        for (uint i = 0u; i < NightLanternCount; i++)
        {
            float3 L;
            float dist;
            float atten = ComputeLanternAttenuation(worldPos, NightLanternPositions[i], L, dist);
            if (atten <= 0.0)
            {
                continue;
            }

            float NdotL = saturate(dot(shadingNormal, L));
            if (NdotL <= 0.0)
            {
                continue;
            }

            lanternSum += EvaluateSingleLanternBrdf(L, NdotL, atten, dist,
                                                    shadingNormal, viewDir, NdotV, F0,
                                                    albedo, roughness, metallic, clearcoat, clearcoatRoughness);
        }

        accumLight = lanternSum * (nightVis * lanternColor);
    }

    float3 accent = EvaluateNightAccentIrradiance(env, worldPos, shadingNormal);
    if (dot(accent, accent) > 1e-6)
    {
        accumLight += accent * (albedo * (1.0 - metallic) * InvPI);
    }

    return accumLight;
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

float3 EvaluateWaterSubsurface(SceneEnvironment env, float3 worldPos, float3 normal, float3 viewDir,
                               float sunVis, float ao, float nightVis, float3 ddgiIrradiance)
{
    float3 refrDir = refract(-viewDir, normal, 0.75);
    float tFloor = 4.5 / max(-refrDir.y, 0.25);
    float2 floorXZ = worldPos.xz + refrDir.xz * tFloor;
    float3 floorPos = float3(floorXZ.x, -27.0, floorXZ.y);

    float stoneTone = lerp(0.94, 1.06, FbmNoise2D(floorXZ * 0.024) / 0.875);
    float3 basinAlbedo = float3(0.095, 0.135, 0.145) * stoneTone;

    float ax = abs(floorXZ.x);
    float stripDist = ax > 470.0 ? min(abs(ax - 492.5), abs(ax - 603.5)) : min(abs(ax - 370.5), abs(ax - 445.5));
    float stripGlow = exp(-stripDist * 0.11) * env.NightLightIntensity;

    float3 lanternFloor = EvaluateNightLightsSecondary(env, floorPos, float3(0.0, 1.0, 0.0), float3(1.0)) * lerp(0.35, 1.0, nightVis);
    float3 lightIrr = ddgiIrradiance * lerp(1.0, ao, 0.65)
                    + env.KeyLightIrradiance * (sunVis * saturate(env.KeyLightDirection.y) * InvPI)
                    + lanternFloor
                    + float3(1.00, 0.68, 0.32) * (stripGlow * 0.52);
    float3 transmittance = exp(-float3(0.115, 0.032, 0.026) * (4.5 + tFloor));

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

float3 ShadeOpaqueSurface(uint materialId, float3 position, float3 shadingNormal, float3 viewDir,
                          float3 albedo, float roughness, SceneMaterial material,
                          float sunVis, float areaVis, float ao, float nightVis, bool isPrimary, out float3 outDdgiIrradiance)
{
    float metallic = saturate(material.Metallic);
    float clearcoat = saturate(material.Clearcoat);
    float clearcoatRoughness = max(material.ClearcoatRoughness, MinimumRoughness);
    float3 F0 = lerp(ComputeDielectricF0(material.Ior), albedo, metallic);
    bool isPrimaryWater = isPrimary && materialId == WaterMaterialId;
    float3 surfaceAlbedo = isPrimaryWater ? 0.0 : albedo;

    float3 sunDirect = sunVis > 0.0
        ? EvaluateDirectSunPBR(lighting.Environment, surfaceAlbedo, F0, roughness, metallic, clearcoat, clearcoatRoughness, shadingNormal, viewDir) * sunVis
        : 0.0;

    float NdotV = saturate(dot(shadingNormal, viewDir));
    float3 F = FresnelSchlick(NdotV, F0);
    float Fc = clearcoat > 0.0 ? (0.04 + 0.96 * pow(1.0 - NdotV, 5.0)) * clearcoat : 0.0;
    float3 kd = (1.0 - F) * (1.0 - metallic) * (1.0 - Fc * 0.5);
    bool hasDiffuse = any(kd * surfaceAlbedo > 1e-4);

    float3 ceilingDiffuse = (hasDiffuse && areaVis > 0.0)
        ? kd * surfaceAlbedo * EvaluateClippedQuadIrradiance(lighting.Environment, position, shadingNormal) * areaVis
        : 0.0;

    float3 nightDirect = isPrimary
        ? EvaluateNightLightsPBR(lighting.Environment, position, shadingNormal, viewDir,
                                 surfaceAlbedo, F0, roughness, metallic, clearcoat, clearcoatRoughness, nightVis)
        : (metallic < 0.999 ? EvaluateNightLightsSecondary(lighting.Environment, position, shadingNormal, albedo * (1.0 - metallic)) : 0.0);

    outDdgiIrradiance = (isPrimary || hasDiffuse)
        ? SampleDDGIIrradiance(position, shadingNormal, viewDir, lighting.Environment,
                               lighting.IrradianceAtlas, lighting.DistanceAtlas,
                               lighting.LinearSampler)
        : 0.0;
    float3 indirectDiffuse = hasDiffuse ? kd * surfaceAlbedo * outDdgiIrradiance * lerp(1.0, ao, 0.75) : 0.0;
    float3 emission = EvaluateMaterialEmission(materialId, material, lighting.Environment);
    float3 caustics = isPrimary
        ? EvaluateGlassCaustics(lighting.Environment, position, shadingNormal, sunVis, areaVis, kd * surfaceAlbedo)
        : 0.0;
    float3 subsurface = isPrimaryWater
        ? (1.0 - F) * EvaluateWaterSubsurface(lighting.Environment, position, shadingNormal, viewDir, sunVis, ao, nightVis, outDdgiIrradiance)
        : 0.0;

    return max(sunDirect + ceilingDiffuse + indirectDiffuse + caustics, 0.0) + nightDirect + subsurface + emission;
}

float3 ShadeSecondaryHit(GeometryHit hit, float3 rayDir, float hitDistance, bool includeEmissiveSpecular)
{
    SceneMaterial material = lighting.Materials[hit.MaterialId];
    float3 albedo = material.Albedo;
    float roughness = max(material.Roughness, MinimumRoughness);
    EvaluateProceduralPattern(material, hit.ObjectPosition, hit.LocalNormal, 0.0, albedo, roughness);

    float3 normal = dot(hit.Normal, rayDir) > 0.0 ? -hit.Normal : hit.Normal;
    float3 geoNormal = dot(hit.GeometricNormal, rayDir) > 0.0 ? -hit.GeometricNormal : hit.GeometricNormal;
    if (hit.MaterialId == WaterMaterialId)
    {
        float detailFade = saturate(1.0 - hitDistance * 0.0008);
        normal = EvaluateProceduralNormal(hit.MaterialId, material, hit.Position, hit.ObjectPosition,
                                          normal, hit.LocalNormal, lighting.Environment.ElapsedSeconds, detailFade);
    }

    float3 viewDir = -rayDir;

    float3 keyDir = lighting.Environment.KeyLightDirection;
    float3 shadowOrigin = OffsetRayOrigin(hit.Position, geoNormal, keyDir);
    float sunVis = dot(normal, keyDir) > 0.0
        ? (TraceVisibilityRay(lighting.Scene, shadowOrigin, keyDir, RayMaxDistance) ? 1.0 : 0.0)
        : 0.0;

    float areaVis = 0.0;
    if (IsCeilingLightPotentiallyVisible(hit.Position))
    {
        float3 toCeiling = CeilingLightCenter - hit.Position;
        float dist = length(toCeiling);
        float3 dirToCeiling = toCeiling / max(dist, 1e-4);
        if (dot(normal, dirToCeiling) > -0.2)
        {
            float3 ceilOrigin = OffsetRayOrigin(hit.Position, geoNormal, dirToCeiling);
            areaVis = TraceVisibilityRay(lighting.Scene, ceilOrigin, dirToCeiling, dist - 1.5) ? 1.0 : 0.0;
        }
    }

    float3 dummyDdgi;
    float3 color = ShadeOpaqueSurface(hit.MaterialId, hit.Position, normal, viewDir, albedo, roughness, material, sunVis, areaVis, 1.0, 1.0, false, dummyDdgi);

    float metallic = saturate(material.Metallic);
    float clearcoat = saturate(material.Clearcoat);
    if (includeEmissiveSpecular && (metallic > 0.15 || clearcoat > 0.35))
    {
        float3 reflDir = reflect(rayDir, normal);
        float3 envDir = normalize(lerp(normal, reflDir, 0.75));
        float3 envIrr = SampleDDGIIrradiance(hit.Position, envDir, viewDir, lighting.Environment,
                                             lighting.IrradianceAtlas, lighting.DistanceAtlas,
                                             lighting.LinearSampler);
        float3 envSky = envDir.y > 0.05 && !IsInsideSanctuaryShell(hit.Position)
            ? EvaluateSkyAmbient(reflDir, lighting.Environment)
            : 0.0;
        float3 envRadiance = max(envIrr, envSky * 0.65);

        float3 F0 = lerp(ComputeDielectricF0(material.Ior), albedo, metallic);
        float NdotV = saturate(dot(normal, viewDir));
        float3 specScale = EstimateEnvSpecularAlbedo(F0, roughness, NdotV) * (metallic + clearcoat * 0.25);
        color += envRadiance * specScale;
    }

    return ApplyAerialPerspective(color, hitDistance, lighting.Environment);
}

float EvaluateReactivity(uint2 pixel, float2 motionVector, float linearDepth, bool isDynamicNow)
{
    if ((lighting.Flags & FlagReset) != 0u)
    {
        return 0.0;
    }

    float2 prevUv = (float2(pixel) + 0.5) / lighting.RenderSize + motionVector;
    if (any(prevUv < 0.0) || any(prevUv > 1.0))
    {
        return 0.85;
    }

    int2 prevCenter = int2(prevUv * lighting.RenderSize);
    int2 maxPixel = int2(lighting.RenderSize) - 1;
    float maxPrevDepth = 0.0;
    bool wasDynamicNearby = false;
    static const int2 Offsets[5] = { int2(0, 0), int2(-1, 0), int2(1, 0), int2(0, -1), int2(0, 1) };

    for (int i = 0; i < 5; i++)
    {
        int2 p = clamp(prevCenter + Offsets[i], int2(0, 0), maxPixel);
        float rawPrev = lighting.PreviousLinearDepth.Load(int3(p, 0));
        maxPrevDepth = max(maxPrevDepth, abs(rawPrev));
        if (rawPrev < 0.0)
        {
            wasDynamicNearby = true;
        }
    }

    float pixelSpeed = length(motionVector * lighting.RenderSize);
    float reactivity = 0.0;

    if (isDynamicNow)
    {
        reactivity = 0.22 * saturate(pixelSpeed * 0.8);
    }
    else if (wasDynamicNearby)
    {
        reactivity = 0.65;
    }
    else if (pixelSpeed > 0.15 && maxPrevDepth > 0.0 && (linearDepth - maxPrevDepth) > max(16.0, linearDepth * 0.06))
    {
        reactivity = 0.65 * saturate((pixelSpeed - 0.15) * 0.5);
    }

    return reactivity;
}

float3 ShadeGlassPixel(float3 hitPos, float3 frontNormal, float3 viewDir, SceneMaterial material, float baseF0, float pixelSpeed, out float reflectionReactivity)
{
    float3 rayDir = -viewDir;
    float ior = max(material.Ior, 1.0);
    float cosTheta = abs(dot(frontNormal, viewDir));
    float fresnel = DielectricFresnelSchlick(cosTheta, ior);
    bool hitDynamic = false;
    bool hitCeilingLight = false;
    bool hitLanternCore = false;

    float3 reflectedRadiance = 0.0;
    float3 reflDir = reflect(rayDir, frontNormal);
    float3 reflOrigin = hitPos + frontNormal * max(RayOffset, length(hitPos - lighting.CameraPosition) * 0.0005);

    uint rInst;
    uint rPrim;
    float2 rBary;
    float rDist;
    if (TraceClosestHit(lighting.Scene, reflOrigin, reflDir, RayMaskOpaque, rInst, rPrim, rBary, rDist))
    {
        GeometryHit rHit = ReconstructGeometryHit(rInst, rPrim, rBary, lighting.Vertices, lighting.Indices, lighting.Instances);
        hitCeilingLight = rHit.MaterialId == CeilingLightMaterialId;
        hitLanternCore = rHit.MaterialId == LanternCoreMaterialId;
        reflectedRadiance = ShadeSecondaryHit(rHit, reflDir, rDist, true);
        if (dot(rHit.Velocity, rHit.Velocity) > 1e-7)
        {
            hitDynamic = true;
        }
    }
    else
    {
        reflectedRadiance = EvaluateSkyFull(reflDir, lighting.Environment);
    }

    float glassRough = max(material.Roughness, MinimumRoughness);
    float glassMirrorWeight = saturate(1.0 - smoothstep(0.06, 0.42, glassRough));
    float glassAreaScale = 1.0 - glassMirrorWeight;
    float3 glassF0 = float3(baseF0);
    float3 keyHighlight = 0.0;
    float3 keyDir = lighting.Environment.KeyLightDirection;
    if (dot(frontNormal, keyDir) > 0.0 && TraceVisibilityRay(lighting.Scene, reflOrigin, keyDir, RayMaxDistance))
    {
        keyHighlight += EvaluateDielectricSpecular(keyDir, frontNormal, viewDir, glassRough, glassF0) * lighting.Environment.KeyLightIrradiance;
    }

    if (!hitCeilingLight && glassAreaScale > 1e-4 && IsCeilingLightPotentiallyVisible(hitPos))
    {
        float3 toCeiling = CeilingLightCenter - hitPos;
        float ceilingDist = length(toCeiling);
        float3 dirToCeiling = toCeiling / max(ceilingDist, 1e-4);
        if (dot(frontNormal, dirToCeiling) > 0.0 && TraceVisibilityRay(lighting.Scene, reflOrigin, dirToCeiling, ceilingDist - 1.5))
        {
            keyHighlight += EvaluateAreaLightSpecular(lighting.Environment, hitPos, frontNormal, viewDir, glassRough, glassF0) * glassAreaScale;
        }
    }

    if (!hitLanternCore && lighting.Environment.NightLightIntensity > 0.001)
    {
        float3 lanternColor = float3(1.00, 0.64, 0.28) * (38000.0 * lighting.Environment.NightLightIntensity);

        for (uint i = 0u; i < NightLanternCount; i++)
        {
            float3 L;
            float dist;
            float atten = ComputeLanternAttenuation(hitPos, NightLanternPositions[i], L, dist);
            if (atten <= 0.0)
            {
                continue;
            }

            float ndl = saturate(dot(frontNormal, L));
            if (atten * ndl > 5e-8 && TraceVisibilityRay(lighting.Scene, reflOrigin, L, dist - GetLanternShadowTrim(L)))
            {
                float lightSinTheta = saturate(11.5 / dist);
                float effRough = clamp(sqrt(glassRough * glassRough + 0.5 * lightSinTheta), MinimumRoughness, 1.0);
                keyHighlight += EvaluateDielectricSpecular(L, frontNormal, viewDir, effRough, glassF0) * (atten * lanternColor);
            }
        }
    }

    float3 refractedRadiance = 0.0;
    float3 transmittance = 1.0;
    float3 absorbCoeff = max(1.0 - material.Albedo, 0.01) * 0.014;

    float3 currNormal = frontNormal;
    float3 currDir = rayDir;
    bool insideGlass = true;
    float eta = 1.0 / ior;
    float3 refrDir = refract(currDir, currNormal, eta);
    if (dot(refrDir, refrDir) < 1e-6)
    {
        refrDir = reflect(currDir, currNormal);
        insideGlass = false;
    }
    else
    {
        refrDir = normalize(refrDir);
    }

    currDir = refrDir;
    float3 stepOrigin = hitPos - frontNormal * RayOffset;
    bool hitOpaque = false;
    float opaqueDist = RayMaxDistance;
    float3 opaqueDir = currDir;
    GeometryHit opaqueHit = {};

    [loop]
    for (int step = 0; step < 4; step++)
    {
        uint hInst;
        uint hPrim;
        float2 hBary;
        float hDist;
        if (!TraceClosestHit(lighting.Scene, stepOrigin, currDir, RayMaskAll, hInst, hPrim, hBary, hDist))
        {
            break;
        }

        if (insideGlass)
        {
            transmittance *= exp(-absorbCoeff * hDist);
        }

        GeometryHit hit = ReconstructGeometryHit(hInst, hPrim, hBary, lighting.Vertices, lighting.Indices, lighting.Instances);
        SceneMaterial hitMat = lighting.Materials[hit.MaterialId];

        if (hitMat.Transmission <= 0.5)
        {
            hitOpaque = true;
            opaqueDist = hDist;
            opaqueDir = currDir;
            opaqueHit = hit;
            break;
        }

        if (dot(hit.Velocity, hit.Velocity) > 1e-7)
        {
            hitDynamic = true;
        }

        float3 exitNormal = dot(hit.Normal, currDir) > 0.0 ? -hit.Normal : hit.Normal;
        float3 exitGeo = dot(hit.GeometricNormal, currDir) > 0.0 ? -hit.GeometricNormal : hit.GeometricNormal;
        float nextEta = insideGlass ? max(hitMat.Ior, 1.0) : (1.0 / max(hitMat.Ior, 1.0));
        float3 nextDir = refract(currDir, exitNormal, nextEta);

        if (dot(nextDir, nextDir) < 1e-6)
        {
            currDir = normalize(reflect(currDir, exitNormal));
            stepOrigin = OffsetRayOrigin(hit.Position, exitGeo, currDir);
        }
        else
        {
            currDir = normalize(nextDir);
            insideGlass = !insideGlass;
            stepOrigin = hit.Position - exitGeo * RayOffset;
        }
    }

    if (hitOpaque)
    {
        if (dot(opaqueHit.Velocity, opaqueHit.Velocity) > 1e-7)
        {
            hitDynamic = true;
        }

        refractedRadiance = transmittance * ShadeSecondaryHit(opaqueHit, opaqueDir, opaqueDist, true);
    }
    else
    {
        refractedRadiance = transmittance * EvaluateSkyFull(currDir, lighting.Environment);
    }

    if (hitDynamic)
    {
        reflectionReactivity = 0.85;
    }
    else if (pixelSpeed > 0.15)
    {
        reflectionReactivity = saturate(pixelSpeed * 0.6) * saturate(0.55 + fresnel * 0.35);
    }
    else
    {
        reflectionReactivity = 0.0;
    }

    return reflectedRadiance * fresnel + refractedRadiance * (1.0 - fresnel) + keyHighlight;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void LightingCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    uint width = uint(lighting.RenderSize.x);
    uint height = uint(lighting.RenderSize.y);
    if (pixel.x >= width || pixel.y >= height)
    {
        return;
    }

    float2 uv = (float2(pixel) + 0.5) / lighting.RenderSize;
    float deviceDepth = lighting.Depth.Load(int3(pixel, 0));

    if (deviceDepth >= 0.999999)
    {
        float3 rayDir = GetCameraRayDirection(uv, lighting.InverseJitteredProjection, lighting.InverseView);
        float3 skyColor = min(EvaluateSkyFull(rayDir, lighting.Environment), float3(32.0));
        lighting.Color.Store(pixel, float4(skyColor, 1.0));

        if ((lighting.Flags & FlagHasReactiveMask) != 0u)
        {
            lighting.ReactiveMask.Store(pixel, 0.0);
        }

        return;
    }

    float4 albedoMat = lighting.AlbedoAndMaterial.Load(int3(pixel, 0));
    float3 albedo = albedoMat.rgb;
    uint materialId = min(uint(round(albedoMat.a * 255.0)), MaxMaterialId);
    SceneMaterial material = lighting.Materials[materialId];

    float4 normalRough = lighting.NormalAndRoughness.Load(int3(pixel, 0));
    float3 normal = normalize(normalRough.xyz);
    bool isDynamic = normalRough.w < 0.0;
    float effectiveRoughness = max(abs(normalRough.w), MinimumRoughness);

    float3 worldPos = ReconstructWorldPosition(uv, deviceDepth, lighting.InverseJitteredViewProjection);
    float3 toCamera = lighting.CameraPosition - worldPos;
    float linearDepth = length(toCamera);
    float3 viewDir = toCamera / max(linearDepth, 1e-4);

    bool computeReactive = (lighting.Flags & FlagHasReactiveMask) != 0u;
    float2 motionVector = (computeReactive || material.Transmission > 0.5) ? lighting.MotionVectors.Load(int3(pixel, 0)) : 0.0;
    float pixelSpeed = length(motionVector * lighting.RenderSize);
    float reactivity = computeReactive ? EvaluateReactivity(pixel, motionVector, linearDepth, isDynamic) : 0.0;

    if (material.Transmission > 0.5)
    {
        float ior = max(material.Ior, 1.0);
        float r0 = (1.0 - ior) / (1.0 + ior);
        float glassReactivity;
        float3 glassColor = ShadeGlassPixel(worldPos, normal, viewDir, material, r0 * r0, pixelSpeed, glassReactivity);
        glassColor = ApplyAerialPerspective(glassColor, linearDepth, lighting.Environment);
        lighting.Color.Store(pixel, float4(glassColor, 1.0));

        if (computeReactive)
        {
            lighting.ReactiveMask.Store(pixel, saturate(max(reactivity, glassReactivity)));
        }

        return;
    }

    float4 shadowAO = lighting.ShadowAO.Load(int3(pixel, 0));
    float sunVis = shadowAO.r;
    float areaVis = shadowAO.g;
    float ao = shadowAO.b;
    float nightVis = shadowAO.a;

    float metallic = saturate(material.Metallic);
    float clearcoat = saturate(material.Clearcoat);
    float clearcoatRoughness = max(material.ClearcoatRoughness, MinimumRoughness);
    float baseRoughness = effectiveRoughness;
    if (clearcoat > 0.0)
    {
        float baseMatRough = max(material.Roughness, MinimumRoughness) + (material.PatternType == PatternStripes ? 0.06 : 0.0);
        float geoVar = (material.PatternType == PatternChecker || material.PatternType == PatternGrid)
            ? 0.0
            : max(0.0, effectiveRoughness * effectiveRoughness - baseMatRough * baseMatRough);
        clearcoatRoughness = clamp(sqrt(clearcoatRoughness * clearcoatRoughness + geoVar), MinimumRoughness, 1.0);
    }

    float3 ddgiIrradiance;
    float3 directAndDiffuse = ShadeOpaqueSurface(materialId, worldPos, normal, viewDir, albedo, baseRoughness, material, sunVis, areaVis, ao, nightVis, true, ddgiIrradiance);

    float NdotV = saturate(dot(normal, viewDir));
    float3 F0 = lerp(ComputeDielectricF0(material.Ior), albedo, metallic);
    float3 envBaseSpec = EstimateEnvSpecularAlbedo(F0, baseRoughness, NdotV);
    envBaseSpec *= 1.0 + F0 * saturate(1.0 / max(envBaseSpec, 0.04) - 1.0) * (metallic * 0.35);
    float3 envCoatSpec = clearcoat > 0.0
        ? EstimateEnvSpecularAlbedo(float3(0.04, 0.04, 0.04), clearcoatRoughness, NdotV) * clearcoat
        : 0.0;
    float3 totalSpecAlbedo = envBaseSpec * (1.0 - envCoatSpec.r) + envCoatSpec;

    float lobeRoughness = clearcoat > 0.2 ? clearcoatRoughness : baseRoughness;
    float mirrorWeight = saturate(1.0 - smoothstep(0.06, 0.42, lobeRoughness));
    float3 envColor = ddgiIrradiance;

    if (mirrorWeight > 0.001)
    {
        float3 reflDir = reflect(-viewDir, normal);
        float3 reflOrigin = worldPos + normal * max(RayOffset, linearDepth * 0.0004);

        float3 tracedEnv = 0.0;
        bool hitCeilingLight = false;
        float reflDistance = RayMaxDistance;
        float reflParallax = 1.0;
        uint rInst;
        uint rPrim;
        float2 rBary;
        float rDist;

        if (TraceClosestHit(lighting.Scene, reflOrigin, reflDir, RayMaskAll, rInst, rPrim, rBary, rDist))
        {
            reflDistance = rDist;
            reflParallax = saturate(rDist / max(linearDepth + rDist, 1.0));
            GeometryHit rHit = ReconstructGeometryHit(rInst, rPrim, rBary, lighting.Vertices, lighting.Indices, lighting.Instances);
            SceneMaterial rMat = lighting.Materials[rHit.MaterialId];

            if (rMat.Transmission > 0.5)
            {
                float3 rNormal = dot(rHit.Normal, reflDir) > 0.0 ? -rHit.Normal : rHit.Normal;
                float rIor = max(rMat.Ior, 1.0);
                float rFresnel = DielectricFresnelSchlick(abs(dot(rNormal, -reflDir)), rIor);
                float3 tDir = refract(reflDir, rNormal, 1.0 / rIor);
                tDir = dot(tDir, tDir) > 1e-6 ? normalize(tDir) : reflect(reflDir, rNormal);

                uint tInst;
                uint tPrim;
                float2 tBary;
                float tDist;
                float3 refractedGlass = TraceClosestHit(lighting.Scene, rHit.Position + tDir * 0.25, tDir, RayMaskOpaque, tInst, tPrim, tBary, tDist)
                    ? ShadeSecondaryHit(ReconstructGeometryHit(tInst, tPrim, tBary, lighting.Vertices, lighting.Indices, lighting.Instances), tDir, tDist, false)
                    : EvaluateSkyFull(tDir, lighting.Environment);
                refractedGlass *= lerp(float3(1.0), rMat.Albedo, 0.85);

                float3 secondaryRefl = reflect(reflDir, rNormal);
                float3 reflectedGlass = SampleDDGIIrradiance(rHit.Position, rNormal, -reflDir, lighting.Environment, lighting.IrradianceAtlas, lighting.DistanceAtlas, lighting.LinearSampler);
                if (!IsInsideSanctuary(rHit.Position))
                {
                    reflectedGlass = max(reflectedGlass, EvaluateSkyAmbient(secondaryRefl, lighting.Environment));
                }

                tracedEnv = lerp(refractedGlass, reflectedGlass, rFresnel);
            }
            else
            {
                hitCeilingLight = rHit.MaterialId == CeilingLightMaterialId;
                tracedEnv = ShadeSecondaryHit(rHit, reflDir, rDist, true);
            }

            if (computeReactive && dot(rHit.Velocity, rHit.Velocity) > 1e-7)
            {
                float4 curClip = mul(float4(worldPos, 1.0), lighting.ViewProjection);
                float4 prevClip = mul(float4(worldPos - rHit.Velocity, 1.0), lighting.PreviousViewProjection);
                float reflSpeed = length(ComputeMotionVector(curClip, prevClip) * lighting.RenderSize);
                float reflWeight = mirrorWeight * max(totalSpecAlbedo.r, max(totalSpecAlbedo.g, totalSpecAlbedo.b));
                reactivity = max(reactivity, saturate(reflSpeed * 1.6) * reflWeight);
            }
        }
        else
        {
            tracedEnv = mirrorWeight > 0.55
                ? EvaluateSkyFull(reflDir, lighting.Environment)
                : EvaluateSkyAmbient(reflDir, lighting.Environment);
        }

        if (computeReactive && pixelSpeed > 0.15)
        {
            float reflWeight = mirrorWeight * saturate(max(totalSpecAlbedo.r, max(totalSpecAlbedo.g, totalSpecAlbedo.b)) * 4.0);
            reactivity = max(reactivity, saturate(pixelSpeed * 0.55) * reflParallax * reflWeight);
        }

        tracedEnv = min(tracedEnv, lerp(float3(28.0), float3(6.0), saturate((lobeRoughness - 0.08) * 3.0)));

        float coneSpread = min(reflDistance, 1200.0) * lobeRoughness * lobeRoughness;
        float effectiveMirrorWeight = mirrorWeight / (1.0 + coneSpread * 0.045);
        envColor = (effectiveMirrorWeight >= 0.98 || (hitCeilingLight && mirrorWeight > 0.5))
            ? tracedEnv
            : lerp(ddgiIrradiance, tracedEnv, effectiveMirrorWeight);
    }

    float areaSpecScale = areaVis * (1.0 - mirrorWeight);
    float3 analyticAreaSpec = 0.0;
    if (areaSpecScale > 1e-4)
    {
        float3 baseAreaSpec = EvaluateAreaLightSpecular(lighting.Environment, worldPos, normal, viewDir, baseRoughness, F0) * (1.0 - envCoatSpec.r);
        float3 coatAreaSpec = clearcoat > 0.0
            ? EvaluateAreaLightSpecular(lighting.Environment, worldPos, normal, viewDir, clearcoatRoughness, float3(0.04)) * clearcoat
            : 0.0;
        analyticAreaSpec = (baseAreaSpec + coatAreaSpec) * areaSpecScale;
    }

    float3 specularRadiance = envColor * totalSpecAlbedo * lerp(1.0, ao, 0.45) + analyticAreaSpec;

    float3 totalColor = ApplyAerialPerspective(directAndDiffuse + specularRadiance, linearDepth, lighting.Environment);
    lighting.Color.Store(pixel, float4(totalColor, 1.0));

    if (computeReactive)
    {
        if (materialId == WaterMaterialId && !lighting.Environment.IsPaused)
        {
            reactivity = max(reactivity, 0.25);
        }

        lighting.ReactiveMask.Store(pixel, reactivity);
    }
}
