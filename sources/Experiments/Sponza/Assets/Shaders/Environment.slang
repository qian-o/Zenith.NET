#include "Sky.slang"

struct EnvironmentConstants
{
    SkyParameters Sky;

    DescriptorHandle<RWTexture2DArray<float4>> Output;

    uint Size;

    float Roughness;
};

ConstantBuffer<EnvironmentConstants> environment;

float3 CubeDirection(uint face, float2 position)
{
    switch (face)
    {
        case 0: return normalize(float3(1.0, -position.y, -position.x));
        case 1: return normalize(float3(-1.0, -position.y, position.x));
        case 2: return normalize(float3(position.x, 1.0, position.y));
        case 3: return normalize(float3(position.x, -1.0, -position.y));
        case 4: return normalize(float3(position.x, -position.y, 1.0));
        default: return normalize(float3(-position.x, -position.y, -1.0));
    }
}

float RadicalInverse(uint bits)
{
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xaaaaaaaau) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xccccccccu) >> 2u);
    bits = ((bits & 0x0f0f0f0fu) << 4u) | ((bits & 0xf0f0f0f0u) >> 4u);
    bits = ((bits & 0x00ff00ffu) << 8u) | ((bits & 0xff00ff00u) >> 8u);

    return float(bits) * 2.3283064365386963e-10;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= environment.Size || id.y >= environment.Size || id.z >= 6)
    {
        return;
    }

    float2 position = (float2(id.xy) + 0.5) / float(environment.Size) * 2.0 - 1.0;
    float3 normal = CubeDirection(id.z, position);
    float3 color;

    if (environment.Roughness <= 0.0)
    {
        color = EvaluateSky(normal, environment.Sky, false);
    }
    else
    {
        float3 reference = abs(normal.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(0.0, 0.0, 1.0);
        float3 tangent = normalize(cross(reference, normal));
        float3 bitangent = cross(normal, tangent);
        float alpha = environment.Roughness * environment.Roughness;
        float alphaSquared = alpha * alpha;
        float3 totalColor = 0.0;
        float totalWeight = 0.0;

        for (uint sampleIndex = 0; sampleIndex < 128; sampleIndex++)
        {
            float2 sample = float2((float(sampleIndex) + 0.5) / 128.0, RadicalInverse(sampleIndex));
            float phi = 6.28318530718 * sample.x;
            float cosine = sqrt((1.0 - sample.y) / (1.0 + (alphaSquared - 1.0) * sample.y));
            float sine = sqrt(max(1.0 - cosine * cosine, 0.0));
            float3 halfVector = tangent * (cos(phi) * sine) + bitangent * (sin(phi) * sine) + normal * cosine;
            float3 light = normalize(2.0 * dot(normal, halfVector) * halfVector - normal);
            float weight = saturate(dot(normal, light));
            totalColor += EvaluateSky(light, environment.Sky, false) * weight;
            totalWeight += weight;
        }

        color = totalColor / max(totalWeight, 0.0001);
    }

    environment.Output[id] = float4(color, 1.0);
}
