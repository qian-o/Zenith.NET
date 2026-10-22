static const float3 LightMin = float3(213.0, 547.0, 227.0);
static const float3 LightMax = float3(343.0, 547.0, 332.0);
static const float LightArea = (343.0 - 213.0) * (332.0 - 227.0);
static const float3 LightNormal = float3(0.0, -1.0, 0.0);
static const uint LightMaterial = 3;
static const uint CheckerPattern = 1;
static const uint GridPattern = 2;
static const float RayOffset = 0.01;
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

    private uint4 FirstIndexAndPadding;

    property float4x4 ObjectToWorld
    {
        get {
            return Transform;
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

    float3 Normal;

    float3 GeometricNormal;

    float3 Albedo;

    float Emission;

    float Metallic;

    float Roughness;
};

struct PathTracingConstants
{
    float4x4 InverseView;

    float4x4 InverseProjection;

    float4 PositionFrame;

    float2 RenderSize;

    uint2 Padding;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<Vertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<Material>> Materials;

    DescriptorHandle<StructuredBuffer<Instance>> Instances;

    DescriptorHandle<RWTexture2D<float4>> Color;
};

ConstantBuffer<PathTracingConstants> pathTracing;

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
    shadowQuery.TraceRayInline(pathTracing.Scene, RAY_FLAG_NONE, 0xFF, shadowRay);

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

    float3 shadowOrigin =
        surface.Position + surface.GeometricNormal * (dot(surface.GeometricNormal, L) > 0.0 ? RayOffset : -RayOffset);

    if (TraceShadowRay(shadowOrigin, L, dist))
    {
        return float3(0.0, 0.0, 0.0);
    }

    Material lightMat = pathTracing.Materials[LightMaterial];
    float3 lightEmission = lightMat.Albedo * lightMat.Emission;

    float pdfLight = (dist * dist) / (lightCosine * LightArea);
    float pdfBrdf = ComputeBRDFPDF(surface.Normal, V, L, roughness, specProb);
    float misWeight = PowerHeuristic(pdfLight, pdfBrdf);

    float3 brdf = EvaluateBRDF(surface.Normal, V, L, surface, roughness);

    return lightEmission * brdf * NdotL * misWeight / pdfLight;
}

float3 SampleSunDirect(Surface surface, float3 V, float roughness, inout uint rng)
{
    const float AngularRadius = 0.1;

    float3 sunDirection = normalize(float3(-0.45, 1.0, 0.35));
    float3 u = normalize(cross(float3(0.0, 0.0, 1.0), sunDirection));
    float3 v = cross(sunDirection, u);
    float cosTheta = lerp(1.0, cos(AngularRadius), RandomFloat(rng));
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));
    float phi = 2.0 * PI * RandomFloat(rng);
    float3 L = normalize(sunDirection * cosTheta + (u * cos(phi) + v * sin(phi)) * sinTheta);
    float NdotL = dot(surface.Normal, L);

    if (NdotL <= 0.0)
    {
        return float3(0.0);
    }

    float3 shadowOrigin =
        surface.Position + surface.GeometricNormal * (dot(surface.GeometricNormal, L) > 0.0 ? RayOffset : -RayOffset);

    if (TraceShadowRay(shadowOrigin, L, 100000.0))
    {
        return float3(0.0);
    }

    return float3(2.5, 2.25, 1.95) * EvaluateBRDF(surface.Normal, V, L, surface, roughness) * NdotL;
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
    Instance instance = pathTracing.Instances[instanceIndex];
    uint index = instance.FirstIndex + primitiveIndex * 3;

    Vertex v0 = pathTracing.Vertices[pathTracing.Indices[index + 0]];
    Vertex v1 = pathTracing.Vertices[pathTracing.Indices[index + 1]];
    Vertex v2 = pathTracing.Vertices[pathTracing.Indices[index + 2]];

    float3 weights = float3(1.0 - barycentrics.x - barycentrics.y, barycentrics.x, barycentrics.y);
    float3 position = v0.Position * weights.x + v1.Position * weights.y + v2.Position * weights.z;
    float3 normal = normalize(v0.Normal * weights.x + v1.Normal * weights.y + v2.Normal * weights.z);
    float3 faceNormal = normalize(cross(v1.Position - v0.Position, v2.Position - v0.Position));

    if (dot(faceNormal, normal) < 0.0)
    {
        faceNormal = -faceNormal;
    }

    Material material = pathTracing.Materials[v0.MaterialId];

    Surface surface;
    surface.Position = mul(float4(position, 1.0), instance.ObjectToWorld).xyz;
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

bool TraceSurface(float3 origin, float3 direction, out Surface surface, out float distance)
{
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = 0.001;
    ray.TMax = 100000.0;

    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> query;
    query.TraceRayInline(pathTracing.Scene, RAY_FLAG_NONE, 0xFF, ray);

    query.Proceed();

    if (query.CommittedStatus() == COMMITTED_NOTHING)
    {
        surface = (Surface)0;
        distance = 0.0;

        return false;
    }

    distance = query.CommittedRayT();
    surface = GetSurface(query.CommittedInstanceIndex(), query.CommittedPrimitiveIndex(),
                         query.CommittedTriangleBarycentrics(), direction);

    return true;
}

float3 TracePath(float3 origin, float3 direction, Surface firstSurface, float firstDistance, bool hasFirstHit,
                 inout uint rng)
{
    float3 throughput = float3(1.0, 1.0, 1.0);
    float3 radiance = float3(0.0, 0.0, 0.0);
    float previousPdf = 0.0;

    for (int bounce = 0; bounce < MaxBounces; bounce++)
    {
        Surface surface = firstSurface;
        float t = firstDistance;
        bool hasHit = hasFirstHit;

        if (bounce > 0)
        {
            hasHit = TraceSurface(origin, direction, surface, t);
        }

        if (!hasHit)
        {
            float3 skyColor =
                lerp(float3(0.15, 0.12, 0.10), float3(0.30, 0.35, 0.45), saturate(direction.y * 0.5 + 0.5));
            radiance += throughput * skyColor;

            break;
        }

        float3 V = -direction;

        float3 F0 = lerp(float3(0.04, 0.04, 0.04), surface.Albedo, surface.Metallic);
        float roughness = max(surface.Roughness, 0.04);

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
        radiance += throughput * SampleSunDirect(surface, V, roughness, rng);

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
        origin = surface.Position +
                 surface.GeometricNormal * (dot(surface.GeometricNormal, newDir) > 0.0 ? RayOffset : -RayOffset);
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
void PathTracingCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    uint2 size = uint2(pathTracing.RenderSize);

    if (any(pixel >= size))
    {
        return;
    }

    uint seed = PCGHash(pixel.x + pixel.y * size.x + asuint(pathTracing.PositionFrame.w) * size.x * size.y);
    float2 uv = (float2(pixel) + 0.5) / float2(size);
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 target = mul(float4(ndc, 1.0, 1.0), pathTracing.InverseProjection);
    float3 direction = normalize(mul(float4(target.xyz / target.w, 0.0), pathTracing.InverseView).xyz);
    Surface firstSurface;
    float firstDistance;
    bool hasFirstHit = TraceSurface(pathTracing.PositionFrame.xyz, direction, firstSurface, firstDistance);

    float3 color =
        min(TracePath(pathTracing.PositionFrame.xyz, direction, firstSurface, firstDistance, hasFirstHit, seed),
            float3(30.0));

    pathTracing.Color[pixel] = float4(color, 1.0);
}
