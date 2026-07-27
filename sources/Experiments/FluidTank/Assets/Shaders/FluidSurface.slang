#include "FluidCommon.slang"

struct SurfaceConstants
{
    float4x4 View;
    float4x4 Projection;
    private float4 CameraRightAndRadius;
    private float4 CameraUpAndRestDensity;
    private uint4 CountsAndDimensions;
    private float4 InterpolationParameters;
    DescriptorHandle<StructuredBuffer<Particle>> Particles;
    DescriptorHandle<StructuredBuffer<float4>> PreviousPositions;
    DescriptorHandle<Texture2D> SceneDepth;
    DescriptorHandle<SamplerState> Sampler;

    property float3 CameraRight { get { return CameraRightAndRadius.xyz; } }
    property float ParticleRadius { get { return CameraRightAndRadius.w; } }
    property float3 CameraUp { get { return CameraUpAndRestDensity.xyz; } }
    property float RestDensity { get { return CameraUpAndRestDensity.w; } }
    property uint ParticleCount { get { return CountsAndDimensions.x; } }
    property uint RenderMode { get { return CountsAndDimensions.y; } }
    property uint Width { get { return CountsAndDimensions.z; } }
    property uint Height { get { return CountsAndDimensions.w; } }
    property float InterpolationAlpha { get { return InterpolationParameters.x; } }
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

bool OccludedByScene(SurfaceVSOutput input, float linearDepth)
{
    float2 uv = input.Position.xy / float2(surface.Width, surface.Height);
    float sceneDepth = (*surface.SceneDepth).SampleLevel(*surface.Sampler, uv, 0.0).r;

    return surface.RenderMode == 0 && sceneDepth > 0.0 && linearDepth >= sceneDepth;
}

float2 QuadCorner(uint vertexId)
{
    if (vertexId == 0) return float2(-1.0, -1.0);
    if (vertexId == 1) return float2(1.0, -1.0);
    if (vertexId == 2) return float2(-1.0, 1.0);

    return float2(1.0, 1.0);
}

[shader("vertex")]
SurfaceVSOutput SurfaceVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    Particle particle = surface.Particles[instanceId];
    float2 corner = QuadCorner(vertexId);
    float3 worldPosition = lerp(surface.PreviousPositions[instanceId].xyz, particle.PositionDensity.xyz, surface.InterpolationAlpha);
    float3 velocity = particle.VelocityLambda.xyz;
    float speed = length(velocity);
    float density = particle.PositionDensity.w / surface.RestDensity;
    float2 offset = corner;

    if (surface.RenderMode == 0)
    {
        float2 projectedVelocity = float2(dot(velocity, surface.CameraRight), dot(velocity, surface.CameraUp));
        float projectedSpeed = length(projectedVelocity);
        float2 direction = projectedSpeed > 0.001 ? projectedVelocity / projectedSpeed : float2(1.0, 0.0);
        float stretchWeight = smoothstep(0.35, 3.2, speed) * (0.45 + 0.55 * (1.0 - smoothstep(0.68, 1.05, density)));
        float majorScale = 1.0 + stretchWeight * 1.65;
        float minorScale = 1.0 - stretchWeight * 0.25;
        offset = direction * corner.x * majorScale + float2(-direction.y, direction.x) * corner.y * minorScale;
    }

    float3 billboardPosition = worldPosition + (surface.CameraRight * offset.x + surface.CameraUp * offset.y) * surface.ParticleRadius;
    float4 centerView = mul(float4(worldPosition, 1.0), surface.View);

    SurfaceVSOutput output;
    output.Position = mul(mul(float4(billboardPosition, 1.0), surface.View), surface.Projection);
    output.Corner = corner;
    output.CenterView = centerView.xyz;
    output.Speed = speed;
    output.Density = density;

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

    if (OccludedByScene(input, -viewPosition.z))
    {
        discard;
    }

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

    if (OccludedByScene(input, -viewPosition.z))
    {
        discard;
    }

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