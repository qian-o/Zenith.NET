#include "Common.slang"

struct GBufferConstants
{
    float4x4 JitteredViewProjection;

    float4x4 ViewProjection;

    float4x4 PreviousViewProjection;

    float4x4 InverseView;

    float4x4 InverseProjection;

    float4x4 UnjitteredInverseProjection;

    private float4 CameraPositionAndBaseInstance;

    private float4 Timing;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<StructuredBuffer<SceneInstance>> Instances;

    DescriptorHandle<Texture2D<float>> StaticDepth;

    property float3 CameraPosition
    {
        get
        {
            return CameraPositionAndBaseInstance.xyz;
        }
    }

    property uint BaseInstance
    {
        get
        {
            return asuint(CameraPositionAndBaseInstance.w);
        }
    }

    property float ElapsedSeconds
    {
        get
        {
            return Timing.x;
        }
    }
};

struct SkyVSOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;
};

struct MeshVSInput
{
    float4 Position : POSITION;

    float4 NormalAndMaterial : NORMAL;
};

struct MeshVSOutput
{
    float4 Position : SV_POSITION;

    float3 WorldPosition : TEXCOORD0;

    float3 WorldNormal : TEXCOORD1;

    float3 ObjectPosition : TEXCOORD2;

    float3 ObjectNormal : TEXCOORD3;

    float4 CurrentClip : TEXCOORD4;

    float4 PreviousClip : TEXCOORD5;

    nointerpolation uint MaterialId : TEXCOORD6;

    nointerpolation uint IsDynamic : TEXCOORD7;
};

struct GBufferFSOutput
{
    float4 AlbedoAndMaterial : SV_TARGET0;

    float4 NormalAndRoughness : SV_TARGET1;

    float Depth : SV_TARGET2;

    float2 MotionVectors : SV_TARGET3;
};

ConstantBuffer<GBufferConstants> gBuffer;

[shader("vertex")]
SkyVSOutput SkyVS(uint vertexId: SV_VertexID)
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);

    SkyVSOutput output;
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 1.0, 1.0);
    output.UV = uv;

    return output;
}

[shader("fragment")]
GBufferFSOutput SkyMotionFS(SkyVSOutput input)
{
    float3 direction = GetCameraRayDirection(input.UV, gBuffer.InverseProjection, gBuffer.InverseView);
    float4 currentClip = mul(float4(direction, 0.0), gBuffer.ViewProjection);
    float4 previousClip = mul(float4(direction, 0.0), gBuffer.PreviousViewProjection);

    GBufferFSOutput output;
    output.AlbedoAndMaterial = float4(0.0, 0.0, 0.0, 1.0);
    output.NormalAndRoughness = float4(0.0, 0.0, 1.0, 0.0);
    output.Depth = 1.0;
    output.MotionVectors = ComputeMotionVector(currentClip, previousClip);

    return output;
}

[shader("vertex")]
MeshVSOutput VSMain(MeshVSInput input, uint instanceId: SV_InstanceID)
{
    SceneInstance instance = gBuffer.Instances[gBuffer.BaseInstance + instanceId];
    float3 localPos = input.Position.xyz;
    float3 localNormal = input.NormalAndMaterial.xyz;

    float4 worldPos = mul(float4(localPos, 1.0), instance.ObjectToWorld);
    float4 previousWorldPos = mul(float4(localPos, 1.0), instance.PreviousObjectToWorld);
    float3 worldNormal = normalize(mul(float4(localNormal, 0.0), instance.ObjectToWorld).xyz);

    MeshVSOutput output;
    output.Position = mul(worldPos, gBuffer.JitteredViewProjection);
    output.WorldPosition = worldPos.xyz;
    output.WorldNormal = worldNormal;
    output.ObjectPosition = localPos;
    output.ObjectNormal = localNormal;
    output.CurrentClip = mul(worldPos, gBuffer.ViewProjection);
    output.PreviousClip = mul(previousWorldPos, gBuffer.PreviousViewProjection);
    output.MaterialId = instance.MaterialOverride != InvalidIndex ? instance.MaterialOverride : asuint(input.NormalAndMaterial.w);
    output.IsDynamic = instance.IsDynamic;

    return output;
}

[shader("fragment")]
GBufferFSOutput FSMain(MeshVSOutput input)
{
    SceneMaterial material = gBuffer.Materials[input.MaterialId];

    float3 normal = normalize(input.WorldNormal);
    float3 toCamera = normalize(gBuffer.CameraPosition - input.WorldPosition);
    if (dot(normal, toCamera) < 0.0)
    {
        normal = -normal;
    }

    float3 objectNormal = normalize(input.ObjectNormal);
    float3 albedo = material.Albedo;
    float roughness = max(material.Roughness, MinimumRoughness);
    float3 dpObj = fwidth(input.ObjectPosition);
    float3 dpWorld = fwidth(input.WorldPosition);
    float detailFade = input.MaterialId == WaterMaterialId
        ? saturate(1.0 - max(dpWorld.x, dpWorld.z) * 0.35)
        : saturate(1.0 - max(dpObj.x, max(dpObj.y, dpObj.z)) * 0.25);

    if (material.PatternType != PatternSolid)
    {
        float3 dp = dpObj / max(material.PatternScale, 0.001);
        float fw = clamp(max(dp.x, max(dp.y, dp.z)), 1e-4, 0.5);
        EvaluateProceduralPattern(material, input.ObjectPosition, objectNormal, float2(fw, fw), albedo, roughness);
    }
    else
    {
        EvaluateSolidMaterialDetail(input.MaterialId, input.ObjectPosition, objectNormal, detailFade, albedo, roughness);
    }

    normal = EvaluateProceduralNormal(input.MaterialId, material, input.WorldPosition, input.ObjectPosition,
                                      normal, objectNormal, gBuffer.ElapsedSeconds, detailFade);

    float3 dndu = ddx(normal);
    float3 dndv = ddy(normal);
    float normalVariance = 0.5 * (dot(dndu, dndu) + dot(dndv, dndv));
    float kernelRoughness2 = min(2.0 * normalVariance, 0.32);
    float filteredRoughness = clamp(sqrt(roughness * roughness + kernelRoughness2), MinimumRoughness, 1.0);

    GBufferFSOutput output;
    output.AlbedoAndMaterial = float4(saturate(albedo), float(input.MaterialId) * (1.0 / 255.0));
    output.NormalAndRoughness = float4(normal, input.IsDynamic != 0u ? -filteredRoughness : filteredRoughness);
    output.Depth = saturate(input.Position.z);
    output.MotionVectors = ComputeMotionVector(input.CurrentClip, input.PreviousClip);

    return output;
}

struct MotionDepthVSOutput
{
    float4 Position : SV_POSITION;

    float4 CurrentClip : TEXCOORD0;

    float4 PreviousClip : TEXCOORD1;
};

struct MotionDepthFSOutput
{
    float Depth : SV_TARGET0;

    float2 MotionVectors : SV_TARGET1;
};

[shader("fragment")]
MotionDepthFSOutput SkyMotionDepthFS(SkyVSOutput input)
{
    float3 direction = GetCameraRayDirection(input.UV, gBuffer.UnjitteredInverseProjection, gBuffer.InverseView);
    float4 currentClip = mul(float4(direction, 0.0), gBuffer.ViewProjection);
    float4 previousClip = mul(float4(direction, 0.0), gBuffer.PreviousViewProjection);

    MotionDepthFSOutput output;
    output.Depth = 1.0;
    output.MotionVectors = ComputeMotionVector(currentClip, previousClip);

    return output;
}

[shader("vertex")]
MotionDepthVSOutput MotionDepthVS(MeshVSInput input, uint instanceId: SV_InstanceID)
{
    SceneInstance instance = gBuffer.Instances[gBuffer.BaseInstance + instanceId];
    float3 localPos = input.Position.xyz;

    float4 worldPos = mul(float4(localPos, 1.0), instance.ObjectToWorld);
    float4 previousWorldPos = mul(float4(localPos, 1.0), instance.PreviousObjectToWorld);
    float4 currentClip = mul(worldPos, gBuffer.ViewProjection);

    MotionDepthVSOutput output;
    output.Position = currentClip;
    output.CurrentClip = currentClip;
    output.PreviousClip = mul(previousWorldPos, gBuffer.PreviousViewProjection);

    return output;
}

[shader("fragment")]
MotionDepthFSOutput MotionDepthFS(MotionDepthVSOutput input)
{
    MotionDepthFSOutput output;
    output.Depth = saturate(input.Position.z);
    output.MotionVectors = ComputeMotionVector(input.CurrentClip, input.PreviousClip);

    return output;
}

[shader("fragment")]
float StaticDepthFS(MotionDepthVSOutput input) : SV_TARGET0
{
    return saturate(input.Position.z);
}

[shader("fragment")]
MotionDepthFSOutput DynamicMotionDepthFS(MotionDepthVSOutput input)
{
    float depth = saturate(input.Position.z);
    if (depth > gBuffer.StaticDepth.Load(int3(uint2(input.Position.xy), 0)))
    {
        discard;
    }

    MotionDepthFSOutput output;
    output.Depth = depth;
    output.MotionVectors = ComputeMotionVector(input.CurrentClip, input.PreviousClip);

    return output;
}
