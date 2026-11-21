static const float PI = 3.14159265359;
static const float TwoPI = 6.28318530718;
static const float InvPI = 0.31830988618;
static const float RayOffset = 0.015;
static const float RayMinDistance = 0.002;
static const float RayMaxDistance = 50000.0;
static const float MissDistance = 65504.0;
static const float MinimumRoughness = 0.035;

static const uint InvalidIndex = 0xFFFFFFFFu;

static const uint PatternSolid = 0u;
static const uint PatternChecker = 1u;
static const uint PatternGrid = 2u;
static const uint PatternStripes = 3u;
static const uint PatternMarble = 4u;

static const float3 SunDirection = float3(0.44068358, 0.52080786, -0.73113406);
static const float SunCosRadius = 0.99968752;
static const float3 SunIrradiance = float3(5.8, 4.40, 2.95);
static const float SunPickWeight = 0.50;
static const float3 SkyZenithColor = float3(0.09, 0.24, 0.58);
static const float3 SkyHorizonColor = float3(0.50, 0.63, 0.78);
static const float SkyIntensity = 1.05;
static const float3 CeilingLightCenter = float3(0.0, 553.5, 275.0);
static const float2 CeilingLightHalfSize = float2(93.0, 83.0);
static const float CeilingLightArea = 30876.0;
static const float3 CeilingLightRadiance = float3(25.0, 22.25, 17.5);

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

struct SurfaceHit
{
    float3 Position;
    uint InstanceIndex;

    float3 ObjectPosition;
    uint IsDynamic;

    float3 Normal;
    float Roughness;

    float3 GeometricNormal;
    float Metallic;

    float3 Albedo;
    float Emission;

    float Transmission;
    float Ior;
    float Clearcoat;
    float ClearcoatRoughness;
};

uint PCGHash(uint input)
{
    uint state = input * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

uint InitRandomSeed(uint2 pixel, uint2 size, uint frameIndex)
{
    uint baseSeed = pixel.x + pixel.y * size.x + frameIndex * 1664525u + 1013904223u;

    return PCGHash(baseSeed);
}

float NextRandom(inout uint seed)
{
    seed = PCGHash(seed);

    return float(seed) * (1.0 / 4294967296.0);
}

uint PackBarycentrics(float2 barycentrics)
{
    uint bx = uint(round(saturate(barycentrics.x) * 65535.0));
    uint by = uint(round(saturate(barycentrics.y) * 65535.0));

    return (bx & 0xFFFFu) | ((by & 0xFFFFu) << 16u);
}

float2 UnpackBarycentrics(uint packedValue)
{
    float bx = float(packedValue & 0xFFFFu) * (1.0 / 65535.0);
    float by = float((packedValue >> 16u) & 0xFFFFu) * (1.0 / 65535.0);

    return float2(bx, by);
}

float2 ComputeMotionVector(float4 currentClip, float4 previousClip)
{
    float currentInvW = 1.0 / max(abs(currentClip.w), 1e-5);
    float previousInvW = 1.0 / max(abs(previousClip.w), 1e-5);
    float2 currentNdc = currentClip.xy * currentInvW;
    float2 previousNdc = previousClip.xy * previousInvW;

    return (previousNdc - currentNdc) * float2(0.5, -0.5);
}

float3 ComputeCameraRayDirection(uint2 pixel, float2 renderSize, float2 jitterOffset, float4x4 inverseProjection,
                                 float4x4 inverseView)
{
    float2 uv = (float2(pixel) + 0.5 + jitterOffset) / renderSize;
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 viewTarget = mul(float4(ndc, 1.0, 1.0), inverseProjection);
    float3 viewDir = viewTarget.xyz / max(abs(viewTarget.w), 1e-5);

    return normalize(mul(float4(viewDir, 0.0), inverseView).xyz);
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

float3 EvaluateSkyRadiance(float3 direction, bool includeSunDisk)
{
    float upFactor = saturate(direction.y * 0.72 + 0.28);
    float3 gradient = lerp(SkyHorizonColor, SkyZenithColor, pow(upFactor, 0.78)) * SkyIntensity;

    float sunAlign = saturate(dot(direction, SunDirection));
    float mieGlow = pow(sunAlign, 14.0) * 0.55 + pow(sunAlign, 3.0) * 0.14;
    gradient += SunIrradiance * (mieGlow * 0.10);

    if (direction.y < 0.0)
    {
        gradient *= lerp(1.0, 0.30, saturate(-direction.y * 3.0));
    }

    if (includeSunDisk && sunAlign >= SunCosRadius)
    {
        float solidAngle = max(TwoPI * (1.0 - SunCosRadius), 1e-4);
        gradient += SunIrradiance / solidAngle;
    }

    return gradient;
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
    float2 uv = axis.y >= max(axis.x, axis.z) ? position.xz : (axis.x >= axis.z ? position.zy : position.xy);

    return uv / max(scale, 0.001);
}

float FbmNoise2D(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;
    float2 q = p;
    for (int i = 0; i < 4; i++)
    {
        sum += ValueNoise2D(q) * amp;
        q = float2(q.x * 1.6 - q.y * 1.2, q.x * 1.2 + q.y * 1.6);
        amp *= 0.5;
    }

    return sum;
}

void EvaluateProceduralPattern(SceneMaterial material, float3 objectPos, float3 objectNormal, inout float3 albedo,
                               inout float roughness)
{
    if (material.PatternType == PatternSolid)
    {
        return;
    }

    float2 uv = TriplanarCoords(objectPos, objectNormal, material.PatternScale);

    if (material.PatternType == PatternChecker)
    {
        int2 cell = int2(floor(uv));
        float2 localTile = abs(frac(uv) - 0.5);
        bool isSeam = max(localTile.x, localTile.y) > 0.482;
        bool alt = ((cell.x + cell.y) & 1) != 0;
        float3 darkTile = float3(0.09, 0.095, 0.105);
        albedo = isSeam ? (material.Albedo * 0.22) : (alt ? darkTile : material.Albedo);
        roughness = isSeam ? 0.55 : (alt ? clamp(roughness * 0.75, MinimumRoughness, 1.0) : roughness);
    }
    else if (material.PatternType == PatternGrid)
    {
        int2 cell = int2(floor(uv));
        uint tileHash = PCGHash(uint(cell.x) * 73856093u ^ uint(cell.y) * 19349663u);
        float tileTone = lerp(0.91, 1.07, float(tileHash & 0xFFu) * (1.0 / 255.0));
        float subtleGrain = lerp(0.95, 1.05, ValueNoise2D(uv * 4.5));
        float2 tile = abs(frac(uv) - 0.5);
        bool isGrout = max(tile.x, tile.y) > 0.472;
        albedo = isGrout ? (material.Albedo * 0.36) : (material.Albedo * tileTone * subtleGrain);
        roughness = isGrout ? clamp(roughness + 0.18, MinimumRoughness, 1.0) : roughness;
    }
    else if (material.PatternType == PatternStripes)
    {
        float fiber = FbmNoise2D(float2(uv.x * 6.0, uv.y * 0.55));
        float rings = 0.5 + 0.5 * sin((uv.x * 3.5 + fiber * 2.8) * PI);
        albedo = material.Albedo * lerp(0.76, 1.12, rings);
        roughness = clamp(roughness + (1.0 - rings) * 0.06, MinimumRoughness, 1.0);
    }
    else if (material.PatternType == PatternMarble)
    {
        float warp = FbmNoise2D(uv * 1.6);
        float fine = FbmNoise2D(uv * 4.5 + warp * 1.8);
        float wave = abs(sin((uv.x * 1.1 + uv.y * 0.7 + warp * 3.0 + fine * 0.8) * PI));
        float whiteVein = pow(saturate(1.0 - wave), 14.0);
        float goldThread = pow(saturate(1.0 - abs(wave - 0.14) * 8.0), 10.0);
        albedo = lerp(material.Albedo, float3(0.72, 0.71, 0.68), whiteVein * 0.42);
        albedo = lerp(albedo, float3(0.75, 0.58, 0.28), goldThread * 0.30);
    }
}

SurfaceHit ReconstructSurface(uint instanceIndex, uint primitiveIndex, float2 barycentrics, float3 rayDirection,
                              StructuredBuffer<SceneVertex> vertices, StructuredBuffer<uint> indices,
                              StructuredBuffer<SceneMaterial> materials, StructuredBuffer<SceneInstance> instances,
                              bool flipFacingToRay)
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

    uint materialId = instance.MaterialOverride != InvalidIndex ? instance.MaterialOverride : v0.MaterialId;
    SceneMaterial mat = materials[materialId];

    float3 albedo = mat.Albedo;
    float roughness = max(mat.Roughness, MinimumRoughness);
    EvaluateProceduralPattern(mat, localPos, localNormal, albedo, roughness);

    SurfaceHit surface;
    surface.Position = mul(float4(localPos, 1.0), instance.ObjectToWorld).xyz;
    surface.ObjectPosition = localPos;
    surface.InstanceIndex = instanceIndex;
    surface.IsDynamic = instance.IsDynamic;
    surface.Normal = normalize(mul(float4(localNormal, 0.0), instance.ObjectToWorld).xyz);
    surface.GeometricNormal = normalize(mul(float4(localFaceNormal, 0.0), instance.ObjectToWorld).xyz);
    surface.Albedo = albedo;
    surface.Roughness = roughness;
    surface.Metallic = saturate(mat.Metallic);
    surface.Emission = mat.Emission;
    surface.Transmission = saturate(mat.Transmission);
    surface.Ior = max(mat.Ior, 1.0);
    surface.Clearcoat = saturate(mat.Clearcoat);
    surface.ClearcoatRoughness = max(mat.ClearcoatRoughness, MinimumRoughness);

    if (flipFacingToRay && dot(surface.GeometricNormal, rayDirection) > 0.0)
    {
        surface.Normal = -surface.Normal;
        surface.GeometricNormal = -surface.GeometricNormal;
    }

    return surface;
}

float DistributionGGX(float NdotH, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;

    return a2 / max(PI * denom * denom, 1e-6);
}

float GeometrySchlickGGX(float NdotX, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) * 0.125;

    return NdotX / max(NdotX * (1.0 - k) + k, 1e-5);
}

float GeometrySmith(float NdotV, float NdotL, float roughness)
{
    return GeometrySchlickGGX(NdotV, roughness) * GeometrySchlickGGX(NdotL, roughness);
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

float3 SampleGGXHalfVector(float3 normal, float roughness, inout uint rng)
{
    float a = roughness * roughness;
    float r1 = NextRandom(rng);
    float r2 = NextRandom(rng);

    float phi = TwoPI * r1;
    float cosTheta = sqrt((1.0 - r2) / max(1.0 + (a * a - 1.0) * r2, 1e-6));
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));

    float3 t;
    float3 b;
    BuildOrthonormalBasis(normal, t, b);

    return normalize(t * (cos(phi) * sinTheta) + b * (sin(phi) * sinTheta) + normal * cosTheta);
}

float3 SampleCosineHemisphere(float3 normal, inout uint rng)
{
    float r1 = NextRandom(rng);
    float r2 = NextRandom(rng);

    float phi = TwoPI * r1;
    float sinTheta = sqrt(r2);
    float cosTheta = sqrt(max(0.0, 1.0 - r2));

    float3 t;
    float3 b;
    BuildOrthonormalBasis(normal, t, b);

    return normalize(t * (cos(phi) * sinTheta) + b * (sin(phi) * sinTheta) + normal * cosTheta);
}

void ComputeLobeProbabilities(SurfaceHit surface, float3 V, out float specProb, out float coatProb, out float diffProb)
{
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), surface.Albedo, surface.Metallic);
    float NdotV = saturate(dot(surface.Normal, V));
    float3 F = FresnelSchlick(NdotV, F0);

    float coatWeight = surface.Clearcoat * (0.04 + 0.96 * pow(1.0 - NdotV, 5.0)) * 0.5;
    float specWeight = max(F.r, max(F.g, F.b));
    float diffWeight = (1.0 - specWeight) * (1.0 - surface.Metallic) * (1.0 - surface.Transmission);

    float sum = max(coatWeight + specWeight + diffWeight, 1e-4);
    coatProb = coatWeight / sum;
    specProb = specWeight / sum;
    diffProb = diffWeight / sum;
}

float3 EvaluateOpaqueBRDF(SurfaceHit surface, float3 V, float3 L)
{
    float NdotL = max(dot(surface.Normal, L), 0.0);
    float NdotV = max(dot(surface.Normal, V), 1e-3);
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float3 H = normalize(V + L);
    float NdotH = max(dot(surface.Normal, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    float3 F0 = lerp(float3(0.04, 0.04, 0.04), surface.Albedo, surface.Metallic);
    float D = DistributionGGX(NdotH, surface.Roughness);
    float G = GeometrySmith(NdotV, NdotL, surface.Roughness);
    float3 F = FresnelSchlick(HdotV, F0);
    float3 specular = (D * G * F) / max(4.0 * NdotV * NdotL, 1e-4);

    float3 kD = (1.0 - F) * (1.0 - surface.Metallic) * (1.0 - surface.Transmission);
    float3 diffuse = kD * surface.Albedo * InvPI;

    float3 clearcoat = 0.0;
    if (surface.Clearcoat > 0.0)
    {
        float Dc = DistributionGGX(NdotH, surface.ClearcoatRoughness);
        float Gc = GeometrySmith(NdotV, NdotL, surface.ClearcoatRoughness);
        float Fc = (0.04 + 0.96 * pow(saturate(1.0 - HdotV), 5.0)) * surface.Clearcoat;
        clearcoat = float3(Dc * Gc * Fc / max(4.0 * NdotV * NdotL, 1e-4));
        diffuse *= (1.0 - Fc * 0.5);
    }

    return diffuse + specular + clearcoat;
}

float ComputeOpaqueBRDFPdf(SurfaceHit surface, float3 V, float3 L)
{
    float NdotL = max(dot(surface.Normal, L), 0.0);
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float specProb;
    float coatProb;
    float diffProb;
    ComputeLobeProbabilities(surface, V, specProb, coatProb, diffProb);

    float3 H = normalize(V + L);
    float NdotH = max(dot(surface.Normal, H), 0.0);
    float HdotV = max(dot(H, V), 1e-4);

    float pdfSpec = DistributionGGX(NdotH, surface.Roughness) * NdotH / (4.0 * HdotV);
    float pdfCoat =
        surface.Clearcoat > 0.0 ? DistributionGGX(NdotH, surface.ClearcoatRoughness) * NdotH / (4.0 * HdotV) : 0.0;
    float pdfDiff = NdotL * InvPI;

    return specProb * pdfSpec + coatProb * pdfCoat + diffProb * pdfDiff;
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

float PowerHeuristic(float pdfA, float pdfB)
{
    float a2 = pdfA * pdfA;

    return a2 / max(a2 + pdfB * pdfB, 1e-5);
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

    RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES>
        query;
    query.TraceRayInline(scene, RAY_FLAG_NONE, 0x01u, ray);
    query.Proceed();

    return query.CommittedStatus() == COMMITTED_NOTHING;
}

float3 SampleCeilingDirectLight(RaytracingAccelerationStructure scene, SurfaceHit surface, float3 V,
                                float pickProbability, inout uint rng)
{
    float u = NextRandom(rng) * 2.0 - 1.0;
    float v = NextRandom(rng) * 2.0 - 1.0;
    float3 lightPoint = CeilingLightCenter + float3(u * CeilingLightHalfSize.x, 0.0, v * CeilingLightHalfSize.y);

    float3 toLight = lightPoint - surface.Position;
    float distSq = dot(toLight, toLight);
    float dist = sqrt(max(distSq, 1e-4));
    float3 L = toLight / dist;

    float NdotL = dot(surface.Normal, L);
    if (NdotL <= 0.0 || L.y <= 0.0)
    {
        return 0.0;
    }

    float3 shadowOrigin = OffsetRayOrigin(surface.Position, surface.GeometricNormal, L);
    if (!TraceVisibilityRay(scene, shadowOrigin, L, dist))
    {
        return 0.0;
    }

    float pdfSolidAngle = distSq / max(L.y * CeilingLightArea, 1e-4);
    float pdfLight = max(pickProbability * pdfSolidAngle, 1e-5);
    float pdfBrdf = ComputeOpaqueBRDFPdf(surface, V, L);
    float misWeight = PowerHeuristic(pdfLight, pdfBrdf);
    float3 brdf = EvaluateOpaqueBRDF(surface, V, L);

    return CeilingLightRadiance * brdf * (NdotL * misWeight / pdfLight);
}

float3 SampleSunDirectLight(RaytracingAccelerationStructure scene, SurfaceHit surface, float3 V,
                            float sunPickProbability, inout uint rng)
{
    float3 t;
    float3 b;
    BuildOrthonormalBasis(SunDirection, t, b);

    float cosTheta = lerp(1.0, SunCosRadius, NextRandom(rng));
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));
    float phi = TwoPI * NextRandom(rng);
    float3 L = normalize(SunDirection * cosTheta + (t * cos(phi) + b * sin(phi)) * sinTheta);

    float NdotL = dot(surface.Normal, L);
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float3 shadowOrigin = OffsetRayOrigin(surface.Position, surface.GeometricNormal, L);
    if (!TraceVisibilityRay(scene, shadowOrigin, L, RayMaxDistance))
    {
        return 0.0;
    }

    float solidAngle = max(TwoPI * (1.0 - SunCosRadius), 1e-4);
    float pdfSun = max(sunPickProbability / solidAngle, 1e-5);
    float pdfBrdf = ComputeOpaqueBRDFPdf(surface, V, L);
    float misWeight = PowerHeuristic(pdfSun, pdfBrdf);
    float3 brdf = EvaluateOpaqueBRDF(surface, V, L);

    return (SunIrradiance / solidAngle) * brdf * (NdotL * misWeight / pdfSun);
}

float3 SampleDirectLighting(RaytracingAccelerationStructure scene, SurfaceHit surface, float3 V, inout uint rng)
{
    bool canSampleSun = dot(surface.Normal, SunDirection) > 0.0;
    float3 toCeiling = CeilingLightCenter - surface.Position;
    bool canSampleCeiling = dot(surface.Normal, toCeiling * rsqrt(max(dot(toCeiling, toCeiling), 2500.0))) > -0.25;

    if (!canSampleSun && !canSampleCeiling)
    {
        return 0.0;
    }

    float sunProb = canSampleSun ? (canSampleCeiling ? SunPickWeight : 1.0) : 0.0;
    if (canSampleSun && NextRandom(rng) < sunProb)
    {
        return SampleSunDirectLight(scene, surface, V, sunProb, rng);
    }

    return canSampleCeiling ? SampleCeilingDirectLight(scene, surface, V, 1.0 - sunProb, rng) : 0.0;
}
