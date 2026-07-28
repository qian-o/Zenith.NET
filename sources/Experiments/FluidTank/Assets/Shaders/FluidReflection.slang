#include "SceneCommon.slang"

struct ReflectionConstants
{
    float4x4 InvView;

    float4x4 InvProjection;

    private float4 CameraPositionAndTime;

    private float4 SunDirectionAndPadding;

    private uint4 DimensionsAndPadding;

    DescriptorHandle<Texture2D<float>> FluidDepth;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<SceneVertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<RWTexture2D<float4>> OutputTexture;

    property float3 CameraPosition
    {
        get {
            return CameraPositionAndTime.xyz;
        }
    }

    property float Time
    {
        get {
            return CameraPositionAndTime.w;
        }
    }

    property float3 SunDirection
    {
        get {
            return SunDirectionAndPadding.xyz;
        }
    }

    property uint Width
    {
        get {
            return DimensionsAndPadding.x;
        }
    }

    property uint Height
    {
        get {
            return DimensionsAndPadding.y;
        }
    }
};

ConstantBuffer<ReflectionConstants> reflection;

float3 ViewRay(float2 uv)
{
    float2 ndc = uv * 2.0 - 1.0;
    ndc.y = -ndc.y;
    float4 target = mul(float4(ndc, 1.0, 1.0), reflection.InvProjection);

    return normalize(target.xyz / target.w);
}

float3 ViewPosition(float2 uv, float linearDepth)
{
    float3 ray = ViewRay(uv);

    return ray * linearDepth / max(-ray.z, 0.0001);
}

float3 WorldPosition(float2 uv, float linearDepth)
{
    return mul(float4(ViewPosition(uv, linearDepth), 1.0), reflection.InvView).xyz;
}

float3 SurfaceNormal(uint2 pixel, float depth)
{
    const uint Radius = 5;
    uint2 leftPixel = uint2(pixel.x > Radius ? pixel.x - Radius : 0, pixel.y);
    uint2 rightPixel = uint2(min(pixel.x + Radius, reflection.Width - 1), pixel.y);
    uint2 upPixel = uint2(pixel.x, pixel.y > Radius ? pixel.y - Radius : 0);
    uint2 downPixel = uint2(pixel.x, min(pixel.y + Radius, reflection.Height - 1));
    float depthLeft = (*reflection.FluidDepth).Load(int3(leftPixel, 0));
    float depthRight = (*reflection.FluidDepth).Load(int3(rightPixel, 0));
    float depthUp = (*reflection.FluidDepth).Load(int3(upPixel, 0));
    float depthDown = (*reflection.FluidDepth).Load(int3(downPixel, 0));

    depthLeft = depthLeft > 0.0 && abs(depthLeft - depth) < 0.75 ? depthLeft : depth;
    depthRight = depthRight > 0.0 && abs(depthRight - depth) < 0.75 ? depthRight : depth;
    depthUp = depthUp > 0.0 && abs(depthUp - depth) < 0.75 ? depthUp : depth;
    depthDown = depthDown > 0.0 && abs(depthDown - depth) < 0.75 ? depthDown : depth;

    float2 dimensions = float2(reflection.Width, reflection.Height);
    float3 left = ViewPosition((float2(leftPixel) + 0.5) / dimensions, depthLeft);
    float3 right = ViewPosition((float2(rightPixel) + 0.5) / dimensions, depthRight);
    float3 up = ViewPosition((float2(upPixel) + 0.5) / dimensions, depthUp);
    float3 down = ViewPosition((float2(downPixel) + 0.5) / dimensions, depthDown);
    float3 normalVector = cross(down - up, right - left);
    float normalLengthSquared = dot(normalVector, normalVector);
    float3 normal = normalLengthSquared > 0.0 ? normalVector * rsqrt(normalLengthSquared) : float3(0.0, 0.0, 1.0);

    if (normal.z < 0.0)
    {
        normal = -normal;
    }

    return normalize(mul(float4(normal, 0.0), reflection.InvView).xyz);
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
        return SkyColor(direction);
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
    float3 lightDirection = normalize(-reflection.SunDirection);
    float diffuse = 0.18 + saturate(dot(normal, lightDirection)) * 1.65;
    float3 environment = SkyColor(reflect(direction, normal));
    float3 f0 = lerp(float3(0.035), material.Albedo, material.Metallic);
    float3 fresnel = FresnelSchlick(saturate(dot(-direction, normal)), f0);

    return material.Albedo * diffuse * (1.0 - material.Metallic) + environment * fresnel * (1.0 - material.Roughness * 0.7);
}

[shader("compute")]
[numthreads(16, 16, 1)]
void ReflectionCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    if (pixel.x >= reflection.Width || pixel.y >= reflection.Height)
    {
        return;
    }

    float depth = (*reflection.FluidDepth).Load(int3(pixel, 0));
    if (depth <= 0.0)
    {
        (*reflection.OutputTexture) [pixel] = float4(0.0, 0.0, 0.0, 0.0);

        return;
    }

    float2 uv = (float2(pixel) + 0.5) / float2(reflection.Width, reflection.Height);
    float3 position = WorldPosition(uv, depth);
    float3 normal = SurfaceNormal(pixel, depth);
    float3 viewDirection = normalize(reflection.CameraPosition - position);
    float3 direction = reflect(-viewDirection, normal);
    float3 color = TraceScene(position + normal * 0.025, direction);

    (*reflection.OutputTexture) [pixel] = float4(color, 1.0);
}
