#include "Material.slang"
#include "Sky.slang"

struct SceneFrameConstants
{
    float4x4 ViewProjection;

    float4x4 UnjitteredViewProjection;

    float4x4 PreviousViewProjection;

    float4x4 InverseViewProjection;

    SkyParameters Sky;

    float4 CameraPositionWorld;

    float4x4 ShadowViewProjection;

    float4 ShadowParameters;

    DescriptorHandle<StructuredBuffer<MaterialConstants>> Materials;

    DescriptorHandle<SamplerState> MaterialSampler;

    DescriptorHandle<Texture2D<float>> ShadowTexture;

    DescriptorHandle<SamplerState> ShadowSampler;

    DescriptorHandle<TextureCube<float4>> EnvironmentTexture;

    DescriptorHandle<SamplerState> EnvironmentSampler;

    float4 RenderSize;
};

struct SceneConstants
{
    float4x4 World;

    float4x4 NormalWorld;

    SceneFrameConstants Frame;

    uint MaterialIndex;

    float WorldOrientation;

    uint Padding0;

    uint Padding1;
};

struct VSInput
{
    float3 Position : POSITION;

    float3 Normal : NORMAL;

    float4 Tangent : TANGENT;

    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;

    float3 PositionWorld : TEXCOORD0;

    float3 NormalWorld : TEXCOORD1;

    float4 TangentWorld : TEXCOORD2;

    float2 TexCoord : TEXCOORD3;

    float4 CurrentClip : TEXCOORD4;

    float4 PreviousClip : TEXCOORD5;
};

struct SceneTargets
{
    float4 HdrColor : SV_TARGET0;

    float4 IndirectDiffuse : SV_TARGET1;

    float DeviceDepth : SV_TARGET2;

    float2 EncodedMotion : SV_TARGET3;
};

struct SkyVertex
{
    float4 Position : SV_POSITION;

    float2 TexCoord : TEXCOORD0;
};

ConstantBuffer<SceneConstants> scene;

float2 EncodeMotion(float4 currentClip, float4 previousClip)
{
    if (currentClip.w <= 0.00001 || previousClip.w <= 0.00001)
    {
        return float2(0.0);
    }

    float2 motion = currentClip.xy / currentClip.w - previousClip.xy / previousClip.w;
    if (any(abs(motion) >= 1.99))
    {
        return float2(0.0);
    }

    return motion * 0.2495 + 32767.0 / 65535.0;
}

float3 FresnelSchlick(float cosine, float3 f0)
{
    return f0 + (1.0 - f0) * pow(1.0 - cosine, 5.0);
}

float ShadowVisibility(float3 positionWorld, float3 normalWorld, float nDotL, SceneFrameConstants frame)
{
    float3 biasedPosition = positionWorld + normalWorld * frame.ShadowParameters.x * (1.0 - nDotL);
    float4 shadowClip = mul(float4(biasedPosition, 1.0), frame.ShadowViewProjection);
    float3 shadowNdc = shadowClip.xyz / shadowClip.w;
    float2 uv = shadowNdc.xy * float2(0.5, -0.5) + 0.5;
    if (any(uv <= 0.0) || any(uv >= 1.0) || shadowNdc.z <= 0.0 || shadowNdc.z >= 1.0)
    {
        return 1.0;
    }

    float depth = shadowNdc.z - frame.ShadowParameters.y * (1.0 + 2.0 * (1.0 - nDotL));
    float visibility = 0.0;
    for (int y = -2; y <= 2; y++)
    {
        for (int x = -2; x <= 2; x++)
        {
            float sampledDepth = frame.ShadowTexture.SampleLevel(*frame.ShadowSampler, uv + float2(x, y) * frame.ShadowParameters.z, 0.0);
            visibility += depth <= sampledDepth ? 1.0 : 0.0;
        }
    }

    return visibility / 25.0;
}

float2 EnvironmentBrdf(float roughness, float nDotV)
{
    // Fit the correlated Smith GGX energy and Schlick fraction
    float alpha = roughness * roughness;
    float alphaSquared = alpha * alpha;
    float t = nDotV / (nDotV + alpha);
    float u = 1.0 - t;
    float3 angularBasis = float3(u * u, 2.0 * t * u, t * t);
    float3 alphaBasis = float3((1.0 - alpha) * (1.0 - alpha), 2.0 * alpha * (1.0 - alpha), alphaSquared);

    float3 energyFit = float3(dot(float3(0.126325815, 0.325733885, 0.011800101), alphaBasis),
                             dot(float3(0.742679760, 0.689897533, 2.327906253), alphaBasis),
                             1.162216840 * alphaSquared);
    float roughLimit = -t * log(t) / u;
    float energy = saturate(1.0 - alphaSquared * roughLimit - (1.0 - alphaSquared) * t * u * dot(angularBasis, energyFit));

    float3 roughnessBasis = float3((1.0 - roughness) * (1.0 - roughness), 2.0 * roughness * (1.0 - roughness), alpha);
    float3 fresnelFit = float3(dot(float3(5.0, 2.340492967, 5.676812982), angularBasis),
                              dot(float3(2.552121422, 8.645267889, 0.0), angularBasis),
                              dot(float3(28.853900818, 25.158895819, 0.0), angularBasis));
    float inverseViewCosine = 1.0 - nDotV;
    float inverseViewCosineSquared = inverseViewCosine * inverseViewCosine;
    float viewCosineSquared = nDotV * nDotV;
    float grazing = inverseViewCosineSquared * inverseViewCosineSquared * inverseViewCosine;
    float numerator = grazing + alphaSquared * (1.922350872 * viewCosineSquared * inverseViewCosineSquared + 0.000860993953 * viewCosineSquared * viewCosineSquared);
    float denominator = 1.0 + alpha * log(1.0 + 1.0 / alpha) * u * dot(roughnessBasis, fresnelFit);
    float fresnelFraction = saturate(numerator / denominator);

    return energy * float2(1.0 - fresnelFraction, fresnelFraction);
}

[shader("vertex")]
VSOutput VSMain(VSInput input)
{
    SceneFrameConstants frame = scene.Frame;
    float4 positionWorld = mul(float4(input.Position, 1.0), scene.World);

    VSOutput output;
    output.Position = mul(positionWorld, frame.ViewProjection);
    output.PositionWorld = positionWorld.xyz;
    output.NormalWorld = mul(float4(input.Normal, 0.0), scene.NormalWorld).xyz;
    output.TangentWorld = float4(mul(float4(input.Tangent.xyz, 0.0), scene.World).xyz, input.Tangent.w * scene.WorldOrientation);
    output.TexCoord = input.TexCoord;
    output.CurrentClip = mul(positionWorld, frame.UnjitteredViewProjection);
    output.PreviousClip = mul(positionWorld, frame.PreviousViewProjection);

    return output;
}

[shader("fragment")]
SceneTargets FSMain(VSOutput input, bool frontFace : SV_IsFrontFace)
{
    SceneFrameConstants frame = scene.Frame;
    MaterialConstants material = frame.Materials[scene.MaterialIndex];
    float4 baseColor = SampleBaseColor(material, input.TexCoord, *frame.MaterialSampler);
    ClipMaterial(material, baseColor.a);

    float3 geometricNormal = normalize(input.NormalWorld);
    float3 normal = geometricNormal;
    if ((material.Flags & 4u) != 0u && dot(input.TangentWorld.xyz, input.TangentWorld.xyz) > 0.00000001)
    {
        float3 tangent = normalize(input.TangentWorld.xyz - geometricNormal * dot(input.TangentWorld.xyz, geometricNormal));
        float3 bitangent = cross(geometricNormal, tangent) * input.TangentWorld.w;
        float3 normalSample = material.Normal.Sample(*frame.MaterialSampler, input.TexCoord).xyz * 2.0 - 1.0;
        normalSample.xy *= material.NormalScale;
        normal = normalize(tangent * normalSample.x + bitangent * normalSample.y + geometricNormal * normalSample.z);
    }

    if (!frontFace && (material.Flags & 16u) != 0u)
    {
        geometricNormal = -geometricNormal;
        normal = -normal;
    }

    float roughness = material.RoughnessFactor;
    float metallic = material.MetallicFactor;
    if ((material.Flags & 2u) != 0u)
    {
        float4 metallicRoughness = material.MetallicRoughness.Sample(*frame.MaterialSampler, input.TexCoord);
        roughness *= metallicRoughness.g;
        metallic *= metallicRoughness.b;
    }
    roughness = clamp(roughness, 0.045, 1.0);
    metallic = saturate(metallic);

    float3 view = normalize(frame.CameraPositionWorld.xyz - input.PositionWorld);
    float3 light = frame.Sky.SunDirectionAndIntensity.xyz;
    float3 halfVector = view + light;
    halfVector *= rsqrt(max(dot(halfVector, halfVector), 0.00000001));
    float nDotL = saturate(dot(normal, light));
    float nDotV = max(saturate(dot(normal, view)), 0.0001);
    float nDotH = saturate(dot(normal, halfVector));
    float vDotH = saturate(dot(view, halfVector));
    float alpha = roughness * roughness;
    float alphaSquared = alpha * alpha;
    float denominator = nDotH * nDotH * (alphaSquared - 1.0) + 1.0;
    float distribution = alphaSquared / max(3.14159265 * denominator * denominator, 0.0000001);
    float visibility = 0.5 / max(nDotL * sqrt(nDotV * nDotV * (1.0 - alphaSquared) + alphaSquared) + nDotV * sqrt(nDotL * nDotL * (1.0 - alphaSquared) + alphaSquared), 0.0001);
    float3 f0 = lerp(float3(0.04), baseColor.rgb, metallic);
    float3 fresnel = FresnelSchlick(vDotH, f0);
    float3 diffuse = baseColor.rgb * (1.0 - metallic) * (1.0 - fresnel) / 3.14159265;
    float3 specular = distribution * visibility * fresnel;
    float shadow = ShadowVisibility(input.PositionWorld, geometricNormal, saturate(dot(geometricNormal, light)), frame);
    float3 direct = (diffuse + specular) * frame.Sky.SunRadiance.xyz * (nDotL * shadow);

    float skyWeight = saturate(normal.y * 0.5 + 0.5);
    float3 ambientSky = lerp(frame.Sky.HorizonColor.xyz, frame.Sky.ZenithColor.xyz, 0.45);
    float3 hemisphere = lerp(frame.Sky.GroundColor.xyz, ambientSky, skyWeight) * frame.Sky.SunDirectionAndIntensity.w;
    float3 indirectDiffuse = baseColor.rgb * (1.0 - metallic) * (1.0 - f0) * hemisphere * 0.65;
    float3 reflection = reflect(-view, normal);
    float3 environment = frame.EnvironmentTexture.SampleLevel(*frame.EnvironmentSampler, reflection, roughness * frame.ShadowParameters.w).rgb;
    float2 brdf = EnvironmentBrdf(roughness, nDotV);
    float3 indirectSpecular = environment * max(f0 * brdf.x + brdf.y, 0.0) * 0.75;

    SceneTargets output;
    output.HdrColor = float4(max(direct + indirectDiffuse + indirectSpecular, 0.0), 1.0);
    output.IndirectDiffuse = float4(indirectDiffuse, 0.0);
    output.DeviceDepth = input.Position.z;
    output.EncodedMotion = EncodeMotion(input.CurrentClip, input.PreviousClip);

    return output;
}

[shader("vertex")]
SkyVertex SkyVSMain(uint vertexId : SV_VertexID)
{
    SkyVertex output;
    output.TexCoord = float2((vertexId << 1) & 2, vertexId & 2);
    output.Position = float4(output.TexCoord * float2(2.0, -2.0) + float2(-1.0, 1.0), 1.0, 1.0);

    return output;
}

[shader("fragment")]
SceneTargets SkyFSMain(SkyVertex input)
{
    SceneFrameConstants frame = scene.Frame;
    float2 ndc = input.TexCoord * float2(2.0, -2.0) + float2(-1.0, 1.0);
    float4 world = mul(float4(ndc, 1.0, 1.0), frame.InverseViewProjection);
    float3 direction = normalize(world.xyz / world.w - frame.CameraPositionWorld.xyz);
    float4 currentClip = mul(float4(direction, 0.0), frame.UnjitteredViewProjection);
    float4 previousClip = mul(float4(direction, 0.0), frame.PreviousViewProjection);

    SceneTargets output;
    output.HdrColor = float4(EvaluateSky(direction, frame.Sky, true), 1.0);
    output.IndirectDiffuse = float4(0.0);
    output.DeviceDepth = 1.0;
    output.EncodedMotion = EncodeMotion(currentClip, previousClip);

    return output;
}
