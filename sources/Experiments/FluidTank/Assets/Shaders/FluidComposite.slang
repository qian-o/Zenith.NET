#include "SceneCommon.slang"

struct CompositeConstants
{
    float4x4 InvView;
    float4x4 InvProjection;
    private float4 CameraPositionAndTime;
    private float4 SunDirectionAndClarity;
    private float4 WaterColorAndRefraction;
    private float4 AbsorptionAndIor;
    private uint4 ScreenAndRenderMode;

    DescriptorHandle<Texture2D> SceneColor;
    DescriptorHandle<Texture2D> SceneDepth;
    DescriptorHandle<Texture2D> FluidDepth;
    DescriptorHandle<Texture2D> Thickness;
    DescriptorHandle<Texture2D> Attributes;
    DescriptorHandle<Texture2D> Reflection;
    DescriptorHandle<SamplerState> Sampler;

    property float3 CameraPosition { get { return CameraPositionAndTime.xyz; } }
    property float Time { get { return CameraPositionAndTime.w; } }
    property float3 SunDirection { get { return SunDirectionAndClarity.xyz; } }
    property float Clarity { get { return SunDirectionAndClarity.w; } }
    property float3 WaterColor { get { return WaterColorAndRefraction.xyz; } }
    property float RefractionStrength { get { return WaterColorAndRefraction.w; } }
    property float3 Absorption { get { return AbsorptionAndIor.xyz; } }
    property float Ior { get { return AbsorptionAndIor.w; } }
    property uint Width { get { return ScreenAndRenderMode.x; } }
    property uint Height { get { return ScreenAndRenderMode.y; } }
    property uint RenderMode { get { return ScreenAndRenderMode.z; } }
    property bool RayTracingEnabled { get { return ScreenAndRenderMode.w != 0; } }
};

struct FullscreenOutput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

ConstantBuffer<CompositeConstants> composite;

[shader("vertex")]
FullscreenOutput FullscreenVS(uint vertexId : SV_VertexID)
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);

    FullscreenOutput output;
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.UV = uv;

    return output;
}

float3 ViewRay(float2 uv)
{
    float2 ndc = uv * 2.0 - 1.0;
    ndc.y = -ndc.y;
    float4 target = mul(float4(ndc, 1.0, 1.0), composite.InvProjection);

    return normalize(target.xyz / target.w);
}

float3 ReconstructViewPosition(float2 uv, float linearDepth)
{
    float3 ray = ViewRay(uv);

    return ray * linearDepth / max(-ray.z, 0.0001);
}

float3 ReconstructWorldPosition(float2 uv, float linearDepth)
{
    return mul(float4(ReconstructViewPosition(uv, linearDepth), 1.0), composite.InvView).xyz;
}

float3 ReconstructNormal(float2 uv, float centerDepth)
{
    float2 texel = 8.0 / float2(composite.Width, composite.Height);
    float depthLeft = (*composite.FluidDepth).SampleLevel(*composite.Sampler, uv - float2(texel.x, 0.0), 0.0).r;
    float depthRight = (*composite.FluidDepth).SampleLevel(*composite.Sampler, uv + float2(texel.x, 0.0), 0.0).r;
    float depthUp = (*composite.FluidDepth).SampleLevel(*composite.Sampler, uv - float2(0.0, texel.y), 0.0).r;
    float depthDown = (*composite.FluidDepth).SampleLevel(*composite.Sampler, uv + float2(0.0, texel.y), 0.0).r;

    depthLeft = depthLeft > 0.0 && abs(depthLeft - centerDepth) < 0.75 ? depthLeft : centerDepth;
    depthRight = depthRight > 0.0 && abs(depthRight - centerDepth) < 0.75 ? depthRight : centerDepth;
    depthUp = depthUp > 0.0 && abs(depthUp - centerDepth) < 0.75 ? depthUp : centerDepth;
    depthDown = depthDown > 0.0 && abs(depthDown - centerDepth) < 0.75 ? depthDown : centerDepth;

    float3 left = ReconstructViewPosition(uv - float2(texel.x, 0.0), depthLeft);
    float3 right = ReconstructViewPosition(uv + float2(texel.x, 0.0), depthRight);
    float3 up = ReconstructViewPosition(uv - float2(0.0, texel.y), depthUp);
    float3 down = ReconstructViewPosition(uv + float2(0.0, texel.y), depthDown);
    float3 normal = normalize(cross(down - up, right - left));

    if (normal.z < 0.0)
    {
        normal = -normal;
    }

    return normal;
}

float3 SceneAt(float2 uv)
{
    float depth = (*composite.SceneDepth).SampleLevel(*composite.Sampler, uv, 0.0).r;
    if (depth <= 0.0)
    {
        float3 rayView = ViewRay(uv);
        float3 rayWorld = normalize(mul(float4(rayView, 0.0), composite.InvView).xyz);

        return SkyColor(rayWorld);
    }

    return (*composite.SceneColor).SampleLevel(*composite.Sampler, uv, 0.0).rgb;
}

float4 ShadeWater(FullscreenOutput input)
{
    float2 uv = input.UV;

    if (composite.RenderMode == 1)
    {
        return float4(ToSRGB(ACESFilm(SceneAt(uv))), 1.0);
    }

    float fluidDepth = (*composite.FluidDepth).SampleLevel(*composite.Sampler, uv, 0.0).r;

    if (fluidDepth <= 0.0)
    {
        float3 background = SceneAt(uv);

        return float4(ToSRGB(ACESFilm(background)), 1.0);
    }

    float thickness = (*composite.Thickness).SampleLevel(*composite.Sampler, uv, 0.0).r;
    float4 attributes = (*composite.Attributes).SampleLevel(*composite.Sampler, uv, 0.0);
    float surfaceConfidence = smoothstep(0.105, 0.135, thickness);

    if (surfaceConfidence <= 0.01)
    {
        return float4(ToSRGB(ACESFilm(SceneAt(uv))), 1.0);
    }

    float3 normalView = ReconstructNormal(uv, fluidDepth);
    float3 normalWorld = normalize(mul(float4(normalView, 0.0), composite.InvView).xyz);

    float3 worldPosition = ReconstructWorldPosition(uv, fluidDepth);
    float3 viewDirection = normalize(composite.CameraPosition - worldPosition);
    float ndotv = saturate(dot(normalWorld, viewDirection));
    float f0 = pow((composite.Ior - 1.0) / (composite.Ior + 1.0), 2.0);
    float fresnel = f0 + (1.0 - f0) * pow(1.0 - ndotv, 5.0);

    float distortionScale = composite.RefractionStrength * (0.008 + min(thickness, 3.0) * 0.0035) / (0.35 + fluidDepth * 0.055);
    float2 refractedUv = clamp(uv + normalView.xy * distortionScale, 0.001, 0.999);

    float3 reflected = composite.RayTracingEnabled ? (*composite.Reflection).SampleLevel(*composite.Sampler, uv, 0.0).rgb : SkyColor(reflect(-viewDirection, normalWorld));
    float3 refracted = SceneAt(refractedUv);

    float opticalDepth = min(thickness * 0.82, 6.0);
    float3 transmission = exp(-composite.Absorption * opticalDepth / max(composite.Clarity, 0.05));
    float3 inScatter = composite.WaterColor * (1.0 - transmission) * (0.34 + 0.26 * saturate(normalWorld.y));
    refracted = refracted * transmission + inScatter;

    float3 lightDirection = normalize(-composite.SunDirection);
    float3 halfVector = normalize(viewDirection + lightDirection);
    float sunSpecular = pow(saturate(dot(normalWorld, halfVector)), 240.0) * 3.2;

    float densityEdge = saturate((0.68 - attributes.y) * 3.0);
    float speedFoam = saturate((attributes.x - 0.28) * 1.8);
    float thinFoam = saturate((0.22 - thickness) * 5.0);
    float foam = saturate(speedFoam * thinFoam * 0.72 + densityEdge * speedFoam * 0.22);

    float3 color = lerp(refracted, reflected, fresnel);
    color += sunSpecular * float3(1.0, 0.94, 0.78);
    color = lerp(color, float3(0.76, 0.92, 0.96), saturate(foam) * 0.72);

    color = lerp(SceneAt(uv), color, surfaceConfidence);

    return float4(ToSRGB(ACESFilm(color)), 1.0);
}

[shader("fragment")]
float4 CompositeFS(FullscreenOutput input) : SV_TARGET
{
    return ShadeWater(input);
}