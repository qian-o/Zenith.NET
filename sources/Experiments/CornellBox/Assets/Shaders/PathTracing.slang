static const float3 LightMin = float3(213.0, 547.0, 227.0);
static const float3 LightMax = float3(343.0, 547.0, 332.0);
static const float LightArea = (343.0 - 213.0) * (332.0 - 227.0);
static const float3 LightNormal = float3(0.0, -1.0, 0.0);
static const uint LightMaterial = 3;
static const uint CheckerPattern = 1;
static const uint GridPattern = 2;
static const float RayOffset = 0.01;
static const float MissDistance = 10000.0;
static const float PI = 3.14159265;
static const int MaxBounces = 8;

struct Vertex
{
    private float4 PositionAndPadding;

    private float4 NormalAndMaterialId;

    property float3 Position
    {
        get {
            return PositionAndPadding.xyz;
        }
    }

    property float3 Normal
    {
        get {
            return NormalAndMaterialId.xyz;
        }
    }

    property uint MaterialId
    {
        get {
            return asuint(NormalAndMaterialId.w);
        }
    }
};

struct Material
{
    private float4 AlbedoAndEmission;

    private float4 MetallicRoughnessAndPattern;

    property float3 Albedo
    {
        get {
            return AlbedoAndEmission.xyz;
        }
    }

    property float Emission
    {
        get {
            return AlbedoAndEmission.w;
        }
    }

    property float Metallic
    {
        get {
            return MetallicRoughnessAndPattern.x;
        }
    }

    property float Roughness
    {
        get {
            return MetallicRoughnessAndPattern.y;
        }
    }

    property uint Pattern
    {
        get {
            return asuint(MetallicRoughnessAndPattern.z);
        }
    }

    property float PatternScale
    {
        get {
            return MetallicRoughnessAndPattern.w;
        }
    }
};

struct Instance
{
    private float4x4 Transform;

    private float4x4 PreviousTransform;

    private uint4 FirstIndexAndPadding;

    property float4x4 ObjectToWorld
    {
        get {
            return Transform;
        }
    }

    property float4x4 PreviousObjectToWorld
    {
        get {
            return PreviousTransform;
        }
    }

    property uint FirstIndex
    {
        get {
            return FirstIndexAndPadding.x;
        }
    }
};

struct Surface
{
    float3 Position;

    float3 PreviousPosition;

    float3 Normal;

    float3 GeometricNormal;

    float3 Albedo;

    float Emission;

    float Metallic;

    float Roughness;
};

struct Constants
{
    float4x4 InverseView;

    float4x4 InverseProjection;

    float4x4 ViewProjection;

    float4x4 PreviousViewProjection;

    float4 PositionFrame;

    float4 RenderSizeJitter;

    uint4 HitDistancePadding;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<Vertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<Material>> Materials;

    DescriptorHandle<StructuredBuffer<Instance>> Instances;

    DescriptorHandle<RWTexture2D<float4>> Color;

    DescriptorHandle<RWTexture2D<float>> Depth;

    DescriptorHandle<RWTexture2D<float4>> Normal;

    DescriptorHandle<RWTexture2D<float2>> MotionVectors;

    DescriptorHandle<RWTexture2D<float4>> DiffuseAlbedo;

    DescriptorHandle<RWTexture2D<float4>> SpecularAlbedo;

    DescriptorHandle<RWTexture2D<float>> SpecularHitDistance;
};

ConstantBuffer<Constants> constants;

float DistributionGGX(float NdotH, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;

    return a2 / (PI * denom * denom);
}

float GeometrySchlickGGX(float NdotX, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;

    return NdotX / (NdotX * (1.0 - k) + k);
}

float GeometrySmith(float NdotV, float NdotL, float roughness)
{
    return GeometrySchlickGGX(NdotV, roughness) * GeometrySchlickGGX(NdotL, roughness);
}

float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float3 EvaluateBRDF(float3 N, float3 V, float3 L, Surface surface, float roughness)
{
    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.001);
    float3 H = normalize(V + L);
    float NdotH = max(dot(N, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    float3 F0 = lerp(float3(0.04, 0.04, 0.04), surface.Albedo, surface.Metallic);

    float D = DistributionGGX(NdotH, roughness);
    float G = GeometrySmith(NdotV, NdotL, roughness);
    float3 F = FresnelSchlick(HdotV, F0);

    float3 specular = D * G * F / (4.0 * NdotV * NdotL + 0.0001);

    float3 kD = (1.0 - F) * (1.0 - surface.Metallic);
    float3 diffuse = kD * surface.Albedo / PI;

    return diffuse + specular;
}

float PowerHeuristic(float pdfA, float pdfB)
{
    float a2 = pdfA * pdfA;

    return a2 / (a2 + pdfB * pdfB + 0.0001);
}

float ComputeBRDFPDF(float3 N, float3 V, float3 L, float roughness, float specProb)
{
    float NdotL = max(dot(N, L), 0.0);
    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float3 H = normalize(V + L);
    float NdotH = max(dot(N, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    float a = roughness * roughness;
    float a2 = a * a;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;
    float D = a2 / (PI * denom * denom);

    float pdfSpec = D * NdotH / (4.0 * HdotV + 0.0001);
    float pdfDiff = NdotL / PI;

    return specProb * pdfSpec + (1.0 - specProb) * pdfDiff;
}

uint PCGHash(uint input)
{
    uint state = input * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

float RandomFloat(inout uint seed)
{
    seed = PCGHash(seed);

    return float(seed) / 4294967295.0;
}

float3 SampleGGXHalfVector(float3 N, float roughness, inout uint seed)
{
    float a = roughness * roughness;

    float r1 = RandomFloat(seed);
    float r2 = RandomFloat(seed);

    float phi = 2.0 * PI * r1;
    float cosTheta = sqrt((1.0 - r2) / (1.0 + (a * a - 1.0) * r2));
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

    float3 w = N;
    float3 helper = abs(w.x) > 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 u = normalize(cross(helper, w));
    float3 v = cross(w, u);

    return normalize(u * cos(phi) * sinTheta + v * sin(phi) * sinTheta + w * cosTheta);
}

float3 CosineSampleHemisphere(float3 normal, inout uint seed)
{
    float r1 = RandomFloat(seed);
    float r2 = RandomFloat(seed);

    float phi = 2.0 * PI * r1;
    float sinTheta = sqrt(r2);
    float cosTheta = sqrt(1.0 - r2);

    float3 w = normal;
    float3 helper = abs(w.x) > 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 u = normalize(cross(helper, w));
    float3 v = cross(w, u);

    return normalize(u * cos(phi) * sinTheta + v * sin(phi) * sinTheta + w * cosTheta);
}

bool TraceShadowRay(float3 origin, float3 direction, float maxDist)
{
    RayDesc shadowRay;
    shadowRay.Origin = origin;
    shadowRay.Direction = direction;
    shadowRay.TMin = 0.001;
    shadowRay.TMax = maxDist - 0.01;

    if (shadowRay.TMax <= shadowRay.TMin)
    {
        return false;
    }

    RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES>
        shadowQuery;
    shadowQuery.TraceRayInline(constants.Scene, RAY_FLAG_NONE, 0xFF, shadowRay);

    shadowQuery.Proceed();

    return shadowQuery.CommittedStatus() != COMMITTED_NOTHING;
}

float3 SampleLightDirect(Surface surface, float3 V, float roughness, float specProb, inout uint rng)
{
    float r1 = RandomFloat(rng);
    float r2 = RandomFloat(rng);

    float3 lightPoint = float3(lerp(LightMin.x, LightMax.x, r1), LightMin.y, lerp(LightMin.z, LightMax.z, r2));

    float3 toLight = lightPoint - surface.Position;
    float dist = length(toLight);
    float3 L = toLight / dist;

    float NdotL = dot(surface.Normal, L);
    if (NdotL <= 0.0)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float lightCosine = max(dot(LightNormal, -L), 0.0);
    if (lightCosine <= 0.0)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float3 shadowOrigin = surface.Position + surface.GeometricNormal * (dot(surface.GeometricNormal, L) > 0.0 ? RayOffset : -RayOffset);
    if (TraceShadowRay(shadowOrigin, L, dist))
    {
        return float3(0.0, 0.0, 0.0);
    }

    Material lightMat = constants.Materials[LightMaterial];
    float3 lightEmission = lightMat.Albedo * lightMat.Emission;

    float pdfLight = (dist * dist) / (lightCosine * LightArea);
    float pdfBrdf = ComputeBRDFPDF(surface.Normal, V, L, roughness, specProb);
    float misWeight = PowerHeuristic(pdfLight, pdfBrdf);

    float3 brdf = EvaluateBRDF(surface.Normal, V, L, surface, roughness);

    return lightEmission * brdf * NdotL * misWeight / pdfLight;
}

float3 PatternAlbedo(Material material, float3 position, float3 normal)
{
    if (material.Pattern != CheckerPattern && material.Pattern != GridPattern)
    {
        return material.Albedo;
    }

    float3 axis = abs(normal);
    float2 uv = axis.y >= max(axis.x, axis.z) ? position.xz : (axis.x >= axis.z ? position.zy : position.xy);
    uv /= material.PatternScale;

    if (material.Pattern == CheckerPattern)
    {
        int2 cell = int2(floor(uv));

        return ((cell.x + cell.y) & 1) == 0 ? material.Albedo : material.Albedo * 0.08;
    }

    float2 tile = abs(frac(uv) - 0.5);

    return max(tile.x, tile.y) > 0.47 ? material.Albedo * 0.15 : material.Albedo;
}

Surface GetSurface(uint instanceIndex, uint primitiveIndex, float2 barycentrics, float3 direction)
{
    Instance instance = constants.Instances[instanceIndex];
    uint index = instance.FirstIndex + primitiveIndex * 3;

    Vertex v0 = constants.Vertices[constants.Indices[index + 0]];
    Vertex v1 = constants.Vertices[constants.Indices[index + 1]];
    Vertex v2 = constants.Vertices[constants.Indices[index + 2]];

    float3 weights = float3(1.0 - barycentrics.x - barycentrics.y, barycentrics.x, barycentrics.y);
    float3 position = v0.Position * weights.x + v1.Position * weights.y + v2.Position * weights.z;
    float3 normal = normalize(v0.Normal * weights.x + v1.Normal * weights.y + v2.Normal * weights.z);
    float3 faceNormal = normalize(cross(v1.Position - v0.Position, v2.Position - v0.Position));

    // Winding differs between meshes; vertex normals define the outside.
    if (dot(faceNormal, normal) < 0.0)
    {
        faceNormal = -faceNormal;
    }

    Material material = constants.Materials[v0.MaterialId];

    Surface surface;
    surface.Position = mul(float4(position, 1.0), instance.ObjectToWorld).xyz;
    surface.PreviousPosition = mul(float4(position, 1.0), instance.PreviousObjectToWorld).xyz;
    surface.Normal = normalize(mul(float4(normal, 0.0), instance.ObjectToWorld).xyz);
    surface.GeometricNormal = normalize(mul(float4(faceNormal, 0.0), instance.ObjectToWorld).xyz);
    surface.Albedo = PatternAlbedo(material, position, normal);
    surface.Emission = material.Emission;
    surface.Metallic = material.Metallic;
    surface.Roughness = material.Roughness;

    if (dot(surface.GeometricNormal, direction) > 0.0)
    {
        surface.Normal = -surface.Normal;
        surface.GeometricNormal = -surface.GeometricNormal;
    }

    return surface;
}

// Specular albedo approximation from the DLSS Ray Reconstruction integration guide.
float3 EnvBRDFApprox2(float3 specularColor, float alpha, float NdotV)
{
    float4 x = float4(1.0, NdotV, NdotV * NdotV, NdotV * NdotV * NdotV);
    float4 y = float4(1.0, alpha, alpha * alpha, alpha * alpha * alpha);

    float2x2 m1 = float2x2(0.99044, -1.28514, 1.29678, -0.755907);
    float3x3 m2 = float3x3(1.0, 2.92338, 59.4188, 20.3225, -27.0302, 222.592, 121.563, 626.13, 316.627);
    float2x2 m3 = float2x2(0.0365463, 3.32707, 9.0632, -9.04756);
    float3x3 m4 = float3x3(1.0, 3.59685, -1.36772, 9.04401, -16.3174, 9.22949, 5.56589, 19.7886, -20.2123);

    float bias = dot(mul(m1, x.xy), y.xy) / dot(mul(m2, x.xyw), y.xyw);
    float scale = dot(mul(m3, x.xy), y.xy) / dot(mul(m4, x.xzw), y.xyw);

    // Surfaces without specular reflectance get no bias.
    bias *= saturate(specularColor.g * 50.0);

    return specularColor * max(scale, 0.0) + max(bias, 0.0);
}

float TraceHitDistance(Surface surface, float3 direction)
{
    RayDesc ray;
    ray.Origin = surface.Position + surface.GeometricNormal * (dot(surface.GeometricNormal, direction) > 0.0 ? RayOffset : -RayOffset);
    ray.Direction = direction;
    ray.TMin = 0.001;
    ray.TMax = MissDistance;

    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> query;
    query.TraceRayInline(constants.Scene, RAY_FLAG_NONE, 0xFF, ray);

    query.Proceed();

    return query.CommittedStatus() == COMMITTED_NOTHING ? MissDistance : query.CommittedRayT();
}

void WriteGuides(uint2 pixel, Surface surface, float3 V, float3 F0)
{
    float4 currentClip = mul(float4(surface.Position, 1.0), constants.ViewProjection);
    float4 previousClip = mul(float4(surface.PreviousPosition, 1.0), constants.PreviousViewProjection);
    float2 motion = currentClip.xy / currentClip.w - previousClip.xy / previousClip.w;
    float NdotV = saturate(dot(surface.Normal, V));

    constants.Depth[pixel] = saturate(currentClip.z / currentClip.w);
    constants.Normal[pixel] = float4(surface.Normal, surface.Roughness);
    constants.MotionVectors[pixel] = motion;
    constants.DiffuseAlbedo[pixel] = float4(surface.Albedo * (1.0 - surface.Metallic), 1.0);
    constants.SpecularAlbedo[pixel] = float4(EnvBRDFApprox2(F0, surface.Roughness * surface.Roughness, NdotV), 1.0);

    if (constants.HitDistancePadding.x != 0)
    {
        constants.SpecularHitDistance[pixel] = TraceHitDistance(surface, reflect(-V, surface.Normal));
    }
}

void WriteSkyGuides(uint2 pixel, float3 direction)
{
    float4 currentClip = mul(float4(direction, 0.0), constants.ViewProjection);
    float4 previousClip = mul(float4(direction, 0.0), constants.PreviousViewProjection);
    float2 motion = currentClip.xy / currentClip.w - previousClip.xy / previousClip.w;

    constants.Depth[pixel] = 1.0;
    constants.Normal[pixel] = float4(0.0);
    constants.MotionVectors[pixel] = motion;
    constants.DiffuseAlbedo[pixel] = float4(0.0);
    constants.SpecularAlbedo[pixel] = float4(0.0);

    if (constants.HitDistancePadding.x != 0)
    {
        constants.SpecularHitDistance[pixel] = MissDistance;
    }
}

float3 TracePath(uint2 pixel, float3 origin, float3 direction, inout uint rng)
{
    float3 throughput = float3(1.0, 1.0, 1.0);
    float3 radiance = float3(0.0, 0.0, 0.0);
    float previousPdf = 0.0;

    for (int bounce = 0; bounce < MaxBounces; bounce++)
    {
        RayDesc ray;
        ray.Origin = origin;
        ray.Direction = direction;
        ray.TMin = 0.001;
        ray.TMax = 100000.0;

        RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> query;
        query.TraceRayInline(constants.Scene, RAY_FLAG_NONE, 0xFF, ray);

        query.Proceed();

        if (query.CommittedStatus() == COMMITTED_NOTHING)
        {
            if (bounce == 0)
            {
                WriteSkyGuides(pixel, direction);
            }

            float3 skyColor =
                lerp(float3(0.15, 0.12, 0.10), float3(0.30, 0.35, 0.45), saturate(direction.y * 0.5 + 0.5));
            radiance += throughput * skyColor;

            break;
        }

        float t = query.CommittedRayT();
        Surface surface = GetSurface(query.CommittedInstanceIndex(), query.CommittedPrimitiveIndex(),
                                     query.CommittedTriangleBarycentrics(), direction);

        float3 V = -direction;

        float3 F0 = lerp(float3(0.04, 0.04, 0.04), surface.Albedo, surface.Metallic);
        float roughness = max(surface.Roughness, 0.04);

        if (bounce == 0)
        {
            WriteGuides(pixel, surface, V, F0);
        }

        if (surface.Emission > 0.0)
        {
            float3 emission = surface.Albedo * surface.Emission;
            float lightCosine = dot(LightNormal, V);

            if (bounce == 0)
            {
                radiance += throughput * emission;
            }
            else if (lightCosine > 0.0)
            {
                // Next event estimation covers the rest of this light's contribution.
                float pdfLight = (t * t) / (lightCosine * LightArea);
                radiance += throughput * emission * PowerHeuristic(previousPdf, pdfLight);
            }

            break;
        }

        float specWeight = max(F0.r, max(F0.g, F0.b));
        float diffWeight = (1.0 - specWeight) * (1.0 - surface.Metallic);
        float total = specWeight + diffWeight;
        float specProb = specWeight / total;

        radiance += throughput * SampleLightDirect(surface, V, roughness, specProb, rng);

        if (bounce == MaxBounces - 1)
        {
            break;
        }

        float3 newDir;
        float NdotV = max(dot(surface.Normal, V), 0.001);

        if (RandomFloat(rng) < specProb)
        {
            float3 H = SampleGGXHalfVector(surface.Normal, roughness, rng);
            newDir = reflect(-V, H);

            float NdotL = dot(surface.Normal, newDir);
            if (NdotL <= 0.0)
            {
                break;
            }

            float NdotH = max(dot(surface.Normal, H), 0.0);
            float HdotV = max(dot(H, V), 0.0);

            float3 F = FresnelSchlick(HdotV, F0);
            float G = GeometrySmith(NdotV, NdotL, roughness);

            float3 specThroughput = F * G * HdotV / (NdotV * NdotH * specProb + 0.0001);
            throughput *= min(specThroughput, float3(10.0, 10.0, 10.0));
        }
        else
        {
            newDir = CosineSampleHemisphere(surface.Normal, rng);

            float3 H = normalize(V + newDir);
            float HdotV = max(dot(H, V), 0.0);

            float3 F = FresnelSchlick(HdotV, F0);
            float3 kD = (1.0 - F) * (1.0 - surface.Metallic);

            throughput *= kD * surface.Albedo / (1.0 - specProb + 0.0001);
        }

        previousPdf = ComputeBRDFPDF(surface.Normal, V, newDir, roughness, specProb);
        origin = surface.Position + surface.GeometricNormal * (dot(surface.GeometricNormal, newDir) > 0.0 ? RayOffset : -RayOffset);
        direction = newDir;

        if (bounce >= 2)
        {
            float p = max(throughput.r, max(throughput.g, throughput.b));

            if (RandomFloat(rng) > p)
            {
                break;
            }

            throughput /= p;
        }
    }

    return radiance;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void CSMain(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    uint2 size = uint2(constants.RenderSizeJitter.xy);

    if (any(pixel >= size))
    {
        return;
    }

    uint seed = PCGHash(pixel.x + pixel.y * size.x + asuint(constants.PositionFrame.w) * size.x * size.y);
    float2 uv = (float2(pixel) + 0.5 - constants.RenderSizeJitter.zw) / float2(size);
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 target = mul(float4(ndc, 1.0, 1.0), constants.InverseProjection);
    float3 direction = normalize(mul(float4(target.xyz / target.w, 0.0), constants.InverseView).xyz);
    float3 color = TracePath(pixel, constants.PositionFrame.xyz, direction, seed);

    constants.Color[pixel] = float4(min(color, float3(30.0)), 1.0);
}
