#include "SceneCommon.slang"
#include "SurfaceCommon.slang"

struct ReflectionConstants
{
    float4x4 InvView;

    float4x4 InvProjection;

    private float4 CameraPositionAndTime;

    private float4 SunDirectionAndIntensity;

    private uint2 Dimensions;

    DescriptorHandle<Texture2D<float4>> Normal;

    DescriptorHandle<Texture2D<float>> FluidDepth;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<SceneVertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<RWTexture2D<float4>> OutputTexture;

    property float3 CameraPosition
    {
        get
        {
            return CameraPositionAndTime.xyz;
        }
    }

    property float Time
    {
        get
        {
            return CameraPositionAndTime.w;
        }
    }

    property float3 SunDirection
    {
        get
        {
            return SunDirectionAndIntensity.xyz;
        }
    }

    property float LightIntensity
    {
        get
        {
            return SunDirectionAndIntensity.w;
        }
    }

    property uint Width
    {
        get
        {
            return Dimensions.x;
        }
    }

    property uint Height
    {
        get
        {
            return Dimensions.y;
        }
    }
};

ConstantBuffer<ReflectionConstants> reflection;

float3 ViewPosition(float2 uv, float linearDepth)
{
    return SurfaceViewPosition(uv, linearDepth, reflection.InvProjection);
}

float3 WorldPosition(float2 uv, float linearDepth)
{
    return mul(float4(ViewPosition(uv, linearDepth), 1.0), reflection.InvView).xyz;
}

float3 TraceScene(float3 origin, float3 direction)
{
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = 0.02;
    ray.TMax = 120.0;

    RayQuery<RAY_FLAG_NONE> query;
    query.TraceRayInline(reflection.Scene, RAY_FLAG_CULL_BACK_FACING_TRIANGLES, 0xFF, ray);
    while (query.Proceed())
    {
    }

    if (query.CommittedStatus() == COMMITTED_NOTHING)
    {
        return EnvironmentColor(direction);
    }

    uint primitive = query.CommittedPrimitiveIndex();
    float2 barycentric = query.CommittedTriangleBarycentrics();
    uint i0 = reflection.Indices[primitive * 3 + 0];
    uint i1 = reflection.Indices[primitive * 3 + 1];
    uint i2 = reflection.Indices[primitive * 3 + 2];
    SceneVertex v0 = reflection.Vertices[i0];
    SceneVertex v1 = reflection.Vertices[i1];
    SceneVertex v2 = reflection.Vertices[i2];
    float3 weights = float3(1.0 - barycentric.x - barycentric.y, barycentric.x, barycentric.y);
    float3 normal = normalize(v0.Normal * weights.x + v1.Normal * weights.y + v2.Normal * weights.z);
    SceneMaterial material = reflection.Materials[v0.MaterialId];
    float3 worldPosition = v0.Position * weights.x + v1.Position * weights.y + v2.Position * weights.z;
    float3 lightDirection = normalize(-reflection.SunDirection);

    return ShadeSceneMaterial(material, v0.MaterialId, worldPosition, normal, -direction, lightDirection, reflection.LightIntensity);
}

[shader("compute")]
[numthreads(16, 16, 1)]
void ReflectionCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    if (pixel.x >= reflection.Width || pixel.y >= reflection.Height)
    {
        return;
    }

    float depth = reflection.FluidDepth.Load(int3(pixel, 0));
    if (depth <= 0.0)
    {
        reflection.OutputTexture[pixel] = float4(0.0, 0.0, 0.0, 0.0);

        return;
    }

    float2 uv = (float2(pixel) + 0.5) / float2(reflection.Width, reflection.Height);
    float3 position = WorldPosition(uv, depth);
    float3 normal = normalize(reflection.Normal.Load(int3(pixel, 0)).xyz);
    float3 viewDirection = normalize(reflection.CameraPosition - position);
    float3 direction = reflect(-viewDirection, normal);
    float3 color = TraceScene(position + normal * 0.025, direction);

    reflection.OutputTexture[pixel] = float4(color, 1.0);
}
