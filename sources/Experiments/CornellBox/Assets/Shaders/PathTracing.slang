static const float3 LightMin = float3(213.0, 547.0, 227.0);
static const float3 LightMax = float3(343.0, 547.0, 332.0);
static const float LightArea = (343.0 - 213.0) * (332.0 - 227.0);
static const float3 LightNormal = float3(0.0, -1.0, 0.0);
static const float PI = 3.14159265;

struct Vertex
{
    private float4 PositionAndPadding;

    private float4 NormalAndMaterialID;

    property float3 Position
    {
        get {
            return PositionAndPadding.xyz;
        }
    }

    property float3 Normal
    {
        get {
            return NormalAndMaterialID.xyz;
        }
    }

    property uint MaterialID
    {
        get {
            return asuint(NormalAndMaterialID.w);
        }
    }
};

struct Material
{
    private float4 AlbedoAndEmission;

    private float4 MetallicRoughnessAndPadding;

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
            return MetallicRoughnessAndPadding.x;
        }
    }

    property float Roughness
    {
        get {
            return MetallicRoughnessAndPadding.y;
        }
    }
};

struct PathTracingConstants
{
    float4x4 InvView;

    float4x4 InvProjection;

    private float4 PositionAndPadding;

    private uint4 FrameCountWidthHeightAndPadding;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<Vertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<Material>> Materials;

    DescriptorHandle<RWTexture2D<float4>> AccumTexture;

    DescriptorHandle<RWTexture2D<float4>> OutputTexture;

    property float3 Position
    {
        get {
            return PositionAndPadding.xyz;
        }
    }

    property uint FrameCount
    {
        get {
            return FrameCountWidthHeightAndPadding.x;
        }
    }

    property uint Width
    {
        get {
            return FrameCountWidthHeightAndPadding.y;
        }
    }

    property uint Height
    {
        get {
            return FrameCountWidthHeightAndPadding.z;
        }
    }
};

uniform PathTracingConstants pathTracing;

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

float3 EvaluateBRDF(float3 N, float3 V, float3 L, Material mat)
{
    float roughness = max(mat.Roughness, 0.04);
    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.001);
    float3 H = normalize(V + L);
    float NdotH = max(dot(N, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    float3 F0 = lerp(float3(0.04, 0.04, 0.04), mat.Albedo, mat.Metallic);

    float D = DistributionGGX(NdotH, roughness);
    float G = GeometrySmith(NdotV, NdotL, roughness);
    float3 F = FresnelSchlick(HdotV, F0);

    float3 specular = D * G * F / (4.0 * NdotV * NdotL + 0.0001);

    float3 kD = (1.0 - F) * (1.0 - mat.Metallic);
    float3 diffuse = kD * mat.Albedo / PI;

    return diffuse + specular;
}

float PowerHeuristic(float pdfA, float pdfB)
{
    float a2 = pdfA * pdfA;

    return a2 / (a2 + pdfB * pdfB + 0.0001);
}

float ComputeBrdfPdf(float3 N, float3 V, float3 L, float roughness, float specProb)
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

uint PcgHash(uint input)
{
    uint state = input * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

float RandomFloat(inout uint seed)
{
    seed = PcgHash(seed);

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

    RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> shadowQuery;
    shadowQuery.TraceRayInline(*pathTracing.Scene, RAY_FLAG_NONE, 0xFF, shadowRay);

    while (shadowQuery.Proceed())
    {
    }

    return shadowQuery.CommittedStatus() != COMMITTED_NOTHING;
}

float3 SampleLightDirect(float3 hitPos, float3 hitNormal, float3 geoNormal, float3 V, Material mat, float roughness,
                         float specProb, inout uint rng)
{
    float r1 = RandomFloat(rng);
    float r2 = RandomFloat(rng);

    float3 lightPoint = float3(lerp(LightMin.x, LightMax.x, r1), LightMin.y, lerp(LightMin.z, LightMax.z, r2));

    float3 toLight = lightPoint - hitPos;
    float dist = length(toLight);
    float3 L = toLight / dist;

    float NdotL = dot(hitNormal, L);
    if (NdotL <= 0.0)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float lightCosine = max(dot(LightNormal, -L), 0.0);
    if (lightCosine <= 0.0)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float3 shadowOrigin = dot(geoNormal, L) > 0.0 ? hitPos + geoNormal * 0.001 : hitPos - geoNormal * 0.001;
    if (TraceShadowRay(shadowOrigin, L, dist))
    {
        return float3(0.0, 0.0, 0.0);
    }

    Material lightMat = (*pathTracing.Materials)[3];
    float3 lightEmission = lightMat.Albedo * lightMat.Emission;

    float pdfLight = (dist * dist) / (lightCosine * LightArea);
    float pdfBrdf = ComputeBrdfPdf(hitNormal, V, L, roughness, specProb);
    float misWeight = PowerHeuristic(pdfLight, pdfBrdf);

    float3 brdf = EvaluateBRDF(hitNormal, V, L, mat);

    return lightEmission * brdf * NdotL * misWeight / pdfLight;
}

float3 TracePath(float3 origin, float3 direction, inout uint rng)
{
    float3 throughput = float3(1.0, 1.0, 1.0);
    float3 radiance = float3(0.0, 0.0, 0.0);

    for (int bounce = 0; bounce < 8; bounce++)
    {
        RayDesc ray;
        ray.Origin = origin;
        ray.Direction = direction;
        ray.TMin = 0.001;
        ray.TMax = 100000.0;

        RayQuery<RAY_FLAG_NONE> query;
        query.TraceRayInline(*pathTracing.Scene, RAY_FLAG_NONE, 0xFF, ray);

        while (query.Proceed())
        {
        }

        if (query.CommittedStatus() == COMMITTED_NOTHING)
        {
            float3 skyColor = lerp(float3(0.15, 0.12, 0.10), float3(0.30, 0.35, 0.45), saturate(direction.y * 0.5 + 0.5));
            radiance += throughput * skyColor;

            break;
        }

        uint primIdx = query.CommittedPrimitiveIndex();
        float2 bary = query.CommittedTriangleBarycentrics();
        float t = query.CommittedRayT();
        float3 hitPos = origin + direction * t;

        uint i0 = (*pathTracing.Indices)[primIdx * 3 + 0];
        uint i1 = (*pathTracing.Indices)[primIdx * 3 + 1];
        uint i2 = (*pathTracing.Indices)[primIdx * 3 + 2];

        Vertex v0 = (*pathTracing.Vertices)[i0];
        Vertex v1 = (*pathTracing.Vertices)[i1];
        Vertex v2 = (*pathTracing.Vertices)[i2];

        float3 baryWeights = float3(1.0 - bary.x - bary.y, bary.x, bary.y);
        float3 normal = normalize(v0.Normal * baryWeights.x + v1.Normal * baryWeights.y + v2.Normal * baryWeights.z);

        float3 geoNormal = normal;
        if (dot(normal, direction) > 0.0)
        {
            normal = -normal;
        }

        Material mat = (*pathTracing.Materials)[v0.MaterialID];

        if (mat.Emission > 0.0)
        {
            if (bounce == 0)
            {
                radiance += throughput * mat.Albedo * mat.Emission;
            }

            break;
        }

        float3 V = -direction;

        float3 F0 = lerp(float3(0.04, 0.04, 0.04), mat.Albedo, mat.Metallic);
        float roughness = max(mat.Roughness, 0.04);
        float specWeight = max(F0.r, max(F0.g, F0.b));
        float diffWeight = (1.0 - specWeight) * (1.0 - mat.Metallic);
        float total = specWeight + diffWeight;
        float specProb = specWeight / total;

        radiance += throughput * SampleLightDirect(hitPos, normal, geoNormal, V, mat, roughness, specProb, rng);

        float3 newDir;
        float NdotV = max(dot(normal, V), 0.001);

        if (RandomFloat(rng) < specProb)
        {
            float3 H = SampleGGXHalfVector(normal, roughness, rng);
            newDir = reflect(-V, H);

            float NdotL = dot(normal, newDir);
            if (NdotL <= 0.0)
            {
                break;
            }

            float NdotH = max(dot(normal, H), 0.0);
            float HdotV = max(dot(H, V), 0.0);

            float3 F = FresnelSchlick(HdotV, F0);
            float G = GeometrySmith(NdotV, NdotL, roughness);

            float3 specThroughput = F * G * HdotV / (NdotV * NdotH * specProb + 0.0001);
            throughput *= min(specThroughput, float3(10.0, 10.0, 10.0));
        }
        else
        {
            newDir = CosineSampleHemisphere(normal, rng);

            float3 H = normalize(V + newDir);
            float HdotV = max(dot(H, V), 0.0);

            float3 F = FresnelSchlick(HdotV, F0);
            float3 kD = (1.0 - F) * (1.0 - mat.Metallic);

            throughput *= kD * mat.Albedo / (1.0 - specProb + 0.0001);
        }

        origin = hitPos + normal * 0.001;
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

float Halton(uint index, uint base)
{
    float result = 0.0;
    float f = 1.0;

    uint i = index;

    while (i > 0)
    {
        f /= float(base);
        result += f * float(i % base);
        i /= base;
    }

    return result;
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;

    if (pixel.x >= pathTracing.Width || pixel.y >= pathTracing.Height)
    {
        return;
    }

    uint rng = PcgHash(pixel.x + pixel.y * pathTracing.Width + pathTracing.FrameCount * pathTracing.Width * pathTracing.Height);

    uint sampleIndex = pathTracing.FrameCount + 1;
    float hx = Halton(sampleIndex, 2);
    float hy = Halton(sampleIndex, 3);
    float ox = RandomFloat(rng) * 0.5 - 0.25;
    float oy = RandomFloat(rng) * 0.5 - 0.25;
    float2 jitter = frac(float2(hx + ox, hy + oy));
    float2 uv = (float2(pixel) + jitter) / float2(pathTracing.Width, pathTracing.Height);
    float2 ndc = uv * 2.0 - 1.0;
    ndc.y = -ndc.y;

    float4 target = mul(float4(ndc, 1.0, 1.0), pathTracing.InvProjection);
    float3 localDir = normalize(target.xyz / target.w);
    float3 direction = normalize(mul(float4(localDir, 0.0), pathTracing.InvView).xyz);
    float3 origin = pathTracing.Position;

    float3 color = TracePath(origin, direction, rng);
    color = min(color, float3(30.0, 30.0, 30.0));

    float4 prev = (*pathTracing.AccumTexture)[pixel];
    float4 accumulated;

    if (pathTracing.FrameCount == 0)
    {
        accumulated = float4(color, 1.0);
    }
    else
    {
        accumulated = prev + float4(color, 1.0);
    }

    (*pathTracing.AccumTexture)[pixel] = accumulated;

    float3 avg = accumulated.rgb / float(pathTracing.FrameCount + 1);

    // ACES tonemapping
    float3 a = avg * (avg * 2.51 + 0.03);
    float3 b = avg * (avg * 2.43 + 0.59) + 0.14;
    avg = saturate(a / b);

    avg = pow(avg, 1.0 / 2.2);
    (*pathTracing.OutputTexture) [pixel] = float4(avg, 1.0);
}
