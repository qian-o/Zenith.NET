#include "FluidCommon.slang"

struct SurfaceConstants
{
    float4x4 View;
    float4x4 Projection;
    private float4 CameraRightAndRadius;
    private float4 CameraUpAndRestDensity;
    private uint4 CountsAndPadding;
    DescriptorHandle<StructuredBuffer<Particle>> Particles;

    property float3 CameraRight { get { return CameraRightAndRadius.xyz; } }
    property float ParticleRadius { get { return CameraRightAndRadius.w; } }
    property float3 CameraUp { get { return CameraUpAndRestDensity.xyz; } }
    property float RestDensity { get { return CameraUpAndRestDensity.w; } }
    property uint ParticleCount { get { return CountsAndPadding.x; } }
};

struct SurfaceVSOutput
{
    float4 Position : SV_POSITION;
    float2 Corner : TEXCOORD0;
    float3 CenterView : TEXCOORD1;
    float Speed : TEXCOORD2;
    float Density : TEXCOORD3;
};

struct DepthOutput
{
    float LinearDepth : SV_TARGET0;
    float4 Attributes : SV_TARGET1;
    float DeviceDepth : SV_DEPTH;
};

struct ThicknessOutput
{
    float Thickness : SV_TARGET;
    float DeviceDepth : SV_DEPTH;
};

struct ParticleOutput
{
    float4 Color : SV_TARGET;
    float DeviceDepth : SV_DEPTH;
};

ConstantBuffer<SurfaceConstants> surface;

float2 QuadCorner(uint vertexId)
{
    if (vertexId == 0) return float2(-1.0, -1.0);
    if (vertexId == 1) return float2(1.0, -1.0);
    if (vertexId == 2) return float2(1.0, 1.0);
    if (vertexId == 3) return float2(-1.0, -1.0);
    if (vertexId == 4) return float2(1.0, 1.0);

    return float2(-1.0, 1.0);
}

[shader("vertex")]
SurfaceVSOutput SurfaceVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    Particle particle = surface.Particles[instanceId];
    float2 corner = QuadCorner(vertexId);
    float3 worldPosition = particle.PositionDensity.xyz;
    float3 billboardPosition = worldPosition + (surface.CameraRight * corner.x + surface.CameraUp * corner.y) * surface.ParticleRadius;
    float4 centerView = mul(float4(worldPosition, 1.0), surface.View);

    SurfaceVSOutput output;
    output.Position = mul(mul(float4(billboardPosition, 1.0), surface.View), surface.Projection);
    output.Corner = corner;
    output.CenterView = centerView.xyz;
    output.Speed = length(particle.VelocityLambda.xyz);
    output.Density = particle.PositionDensity.w / surface.RestDensity;

    return output;
}

[shader("fragment")]
DepthOutput DepthFS(SurfaceVSOutput input)
{
    float radiusSquared = dot(input.Corner, input.Corner);
    if (radiusSquared > 1.0)
    {
        discard;
    }

    float sphereZ = sqrt(max(1.0 - radiusSquared, 0.0));
    float3 viewPosition = input.CenterView + float3(input.Corner * surface.ParticleRadius, surface.ParticleRadius * 0.62);
    float4 clipPosition = mul(float4(viewPosition, 1.0), surface.Projection);

    DepthOutput output;
    output.LinearDepth = -viewPosition.z;
    output.Attributes = float4(input.Speed, input.Density, sphereZ, 1.0);
    output.DeviceDepth = clipPosition.z / clipPosition.w;

    return output;
}

[shader("fragment")]
ThicknessOutput ThicknessFS(SurfaceVSOutput input)
{
    float radiusSquared = dot(input.Corner, input.Corner);
    if (radiusSquared > 1.0)
    {
        discard;
    }

    float sphereZ = sqrt(max(1.0 - radiusSquared, 0.0));
    float3 viewPosition = input.CenterView + float3(input.Corner * surface.ParticleRadius, sphereZ * surface.ParticleRadius);
    float4 clipPosition = mul(float4(viewPosition, 1.0), surface.Projection);

    ThicknessOutput output;
    output.Thickness = 1.08 * surface.ParticleRadius * sphereZ;
    output.DeviceDepth = clipPosition.z / clipPosition.w;

    return output;
}

float3 SpeedColor(float speed)
{
    float value = saturate(speed / 6.5);
    float3 blue = float3(0.015, 0.12, 0.95);
    float3 cyan = float3(0.0, 0.82, 1.0);
    float3 yellow = float3(1.0, 0.84, 0.02);
    float3 red = float3(0.95, 0.025, 0.01);

    if (value < 0.34)
    {
        return lerp(blue, cyan, value / 0.34);
    }

    if (value < 0.70)
    {
        return lerp(cyan, yellow, (value - 0.34) / 0.36);
    }

    return lerp(yellow, red, (value - 0.70) / 0.30);
}

[shader("fragment")]
ParticleOutput ParticleFS(SurfaceVSOutput input)
{
    float radiusSquared = dot(input.Corner, input.Corner);
    if (radiusSquared > 1.0)
    {
        discard;
    }

    float sphereZ = sqrt(max(1.0 - radiusSquared, 0.0));
    float3 normal = normalize(float3(input.Corner, sphereZ));
    float3 lightDirection = normalize(float3(-0.38, 0.62, 0.69));
    float diffuse = 0.24 + 0.76 * saturate(dot(normal, lightDirection));
    float specular = pow(saturate(dot(reflect(-lightDirection, normal), float3(0.0, 0.0, 1.0))), 36.0) * 0.42;
    float rim = pow(1.0 - sphereZ, 3.0) * 0.12;
    float3 color = SpeedColor(input.Speed) * (diffuse + rim) + specular;
    float3 viewPosition = input.CenterView + float3(input.Corner * surface.ParticleRadius, sphereZ * surface.ParticleRadius);
    float4 clipPosition = mul(float4(viewPosition, 1.0), surface.Projection);

    ParticleOutput output;
    output.Color = float4(pow(saturate(color), 1.0 / 2.2), 1.0);
    output.DeviceDepth = clipPosition.z / clipPosition.w;

    return output;
}