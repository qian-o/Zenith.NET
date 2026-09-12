#include "FluidCommon.slang"

struct ParticleConstants
{
    float4x4 View;

    float4x4 Projection;

    private float4 CameraRightAndRadius;

    private float4 CameraUpAndInterpolation;

    DescriptorHandle<StructuredBuffer<Particle>> Particles;

    DescriptorHandle<StructuredBuffer<float4>> PreviousPositions;

    property float3 CameraRight
    {
        get
        {
            return CameraRightAndRadius.xyz;
        }
    }

    property float Radius
    {
        get
        {
            return CameraRightAndRadius.w;
        }
    }

    property float3 CameraUp
    {
        get
        {
            return CameraUpAndInterpolation.xyz;
        }
    }

    property float InterpolationAlpha
    {
        get
        {
            return CameraUpAndInterpolation.w;
        }
    }
};

struct ParticleVertex
{
    float4 Position : SV_POSITION;

    float2 Corner : TEXCOORD0;

    float3 CenterView : TEXCOORD1;

    float Speed : TEXCOORD2;
};

struct ParticleOutput
{
    float4 Color : SV_TARGET;

    float DeviceDepth : SV_DEPTH;
};

ConstantBuffer<ParticleConstants> particles;

[shader("vertex")]
ParticleVertex ParticleVS(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    float2 corner = float2(vertexId & 1, vertexId >> 1) * 2.0 - 1.0;
    Particle particle = particles.Particles[instanceId];
    float3 position = lerp(particles.PreviousPositions[instanceId].xyz, particle.PositionDensity.xyz, particles.InterpolationAlpha);
    float3 billboard = position + (particles.CameraRight * corner.x + particles.CameraUp * corner.y) * particles.Radius;

    ParticleVertex output;
    output.Position = mul(mul(float4(billboard, 1.0), particles.View), particles.Projection);
    output.Corner = corner;
    output.CenterView = mul(float4(position, 1.0), particles.View).xyz;
    output.Speed = length(particle.VelocityLambda.xyz);

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
ParticleOutput ParticleFS(ParticleVertex input)
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
    float3 viewPosition = input.CenterView + float3(input.Corner, sphereZ) * particles.Radius;
    float4 clipPosition = mul(float4(viewPosition, 1.0), particles.Projection);

    ParticleOutput output;
    output.Color = float4(color, 1.0);
    output.DeviceDepth = clipPosition.z / clipPosition.w;

    return output;
}
