#include "SceneCommon.slang"
#include "SurfaceCommon.slang"

struct CompositeConstants
{
    float4x4 InvView;

    float4x4 InvProjection;

    private float4 CameraPositionAndIntensity;

    private float4 SunDirectionAndClarity;

    private float4 WaterColorAndRefraction;

    private float4 AbsorptionAndIor;

    private uint4 ScreenAndRenderMode;

    DescriptorHandle<Texture2D> SceneColor;

    DescriptorHandle<Texture2D> SceneDepth;

    DescriptorHandle<Texture2D> FluidDepth;

    DescriptorHandle<Texture2D> Thickness;

    DescriptorHandle<Texture2D<float4>> Normal;

    DescriptorHandle<Texture2D> Reflection;

    DescriptorHandle<SamplerState> Sampler;

    property float3 CameraPosition
    {
        get
        {
            return CameraPositionAndIntensity.xyz;
        }
    }

    property float LightIntensity
    {
        get
        {
            return CameraPositionAndIntensity.w;
        }
    }

    property float3 SunDirection
    {
        get
        {
            return SunDirectionAndClarity.xyz;
        }
    }

    property float Clarity
    {
        get
        {
            return SunDirectionAndClarity.w;
        }
    }

    property float3 WaterColor
    {
        get
        {
            return WaterColorAndRefraction.xyz;
        }
    }

    property float RefractionStrength
    {
        get
        {
            return WaterColorAndRefraction.w;
        }
    }

    property float3 Absorption
    {
        get
        {
            return AbsorptionAndIor.xyz;
        }
    }

    property float Ior
    {
        get
        {
            return AbsorptionAndIor.w;
        }
    }

    property uint Width
    {
        get
        {
            return ScreenAndRenderMode.x;
        }
    }

    property uint Height
    {
        get
        {
            return ScreenAndRenderMode.y;
        }
    }

    property uint RenderMode
    {
        get
        {
            return ScreenAndRenderMode.z;
        }
    }

    property bool RayTracingEnabled
    {
        get
        {
            return ScreenAndRenderMode.w != 0;
        }
    }
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

float3 ReconstructViewPosition(float2 uv, float linearDepth)
{
    return SurfaceViewPosition(uv, linearDepth, composite.InvProjection);
}

float3 SceneAt(float2 uv)
{
    return composite.SceneColor.SampleLevel(composite.Sampler, uv, 0.0).rgb;
}

float3 SampleFluid(float2 uv, float sceneDepth, out float3 normal)
{
    int2 dimensions = int2(composite.Width, composite.Height);
    float2 position = uv * float2(dimensions) - 0.5;
    int2 basePixel = int2(floor(position));
    float2 fraction = frac(position);
    float closestDepth = 0.0;

    for (int y = 0; y < 2; y++)
    {
        for (int x = 0; x < 2; x++)
        {
            int2 samplePixel = clamp(basePixel + int2(x, y), int2(0, 0), dimensions - 1);
            float sampleDepth = composite.FluidDepth.Load(int3(samplePixel, 0)).r;

            if (sampleDepth > 0.0 && (closestDepth <= 0.0 || sampleDepth < closestDepth))
            {
                closestDepth = sampleDepth;
            }
        }
    }

    normal = float3(0.0);
    float depth = 0.0;
    float thickness = 0.0;
    float weightSum = 0.0;
    float range = SurfaceDepthRange(closestDepth);

    for (int y = 0; y < 2; y++)
    {
        for (int x = 0; x < 2; x++)
        {
            int2 samplePixel = clamp(basePixel + int2(x, y), int2(0, 0), dimensions - 1);
            float sampleDepth = composite.FluidDepth.Load(int3(samplePixel, 0)).r;

            if (sampleDepth <= 0.0 || abs(sampleDepth - closestDepth) > range || (sceneDepth > 0.0 && sampleDepth >= sceneDepth))
            {
                continue;
            }

            float weight = (x == 0 ? 1.0 - fraction.x : fraction.x) * (y == 0 ? 1.0 - fraction.y : fraction.y);
            depth += sampleDepth * weight;
            thickness += composite.Thickness.Load(int3(samplePixel, 0)).r * weight;
            normal += composite.Normal.Load(int3(samplePixel, 0)).xyz * weight;
            weightSum += weight;
        }
    }

    return weightSum > 0.0 ? float3(depth / weightSum, thickness / weightSum, weightSum) : float3(0.0);
}

float2 ProjectViewPosition(float3 position)
{
    float2 ndc = position.xy / -position.z / float2(composite.InvProjection[0][0], composite.InvProjection[1][1]);

    return ndc * float2(0.5, -0.5) + 0.5;
}

float3 RefractionPositionAtDepth(float3 origin, float3 incident, float3 direction, float thickness, float depth, out float waterDistance)
{
    waterDistance = min((depth + origin.z) / -direction.z, thickness);
    float3 position = origin + direction * waterDistance;
    float airDistance = max((depth + position.z) / -incident.z, 0.0);

    return position + incident * airDistance;
}

float4 TraceRefraction(float2 uv, float sceneDepth, float3 viewPosition, float3 incident, float3 normal, float thickness)
{
    float3 direction = normalize(lerp(incident, refract(incident, normal, 1.0 / composite.Ior), composite.RefractionStrength));
    float fluidDepth = -viewPosition.z;

    if (sceneDepth <= 0.0 || direction.z >= 0.0)
    {
        return float4(uv, thickness, 0.0);
    }

    uint width;
    uint height;
    composite.SceneDepth.GetDimensions(width, height);
    float2 dimensions = float2(width, height);
    float targetDepth = sceneDepth;
    float waterDistance = thickness;
    float depthError = 0.0;
    float2 sampleUv = uv;

    for (int iteration = 0; iteration < 4; iteration++)
    {
        float3 position = RefractionPositionAtDepth(viewPosition, incident, direction, thickness, targetDepth, waterDistance);
        sampleUv = ProjectViewPosition(position);

        if (any(sampleUv < 0.0) || any(sampleUv >= 1.0))
        {
            return float4(uv, thickness, 0.0);
        }

        float sampledDepth = composite.SceneDepth.Load(int3(int2(sampleUv * dimensions), 0)).r;

        if (sampledDepth <= fluidDepth)
        {
            return float4(uv, thickness, 0.0);
        }

        depthError = abs(sampledDepth - targetDepth);
        targetDepth = sampledDepth;
    }

    float pixelSize = 2.0 * targetDepth * max(composite.InvProjection[0][0] / dimensions.x, composite.InvProjection[1][1] / dimensions.y);
    float tolerance = max(pixelSize * 2.0, 0.025);
    float confidence = 1.0 - smoothstep(tolerance, tolerance * 4.0, depthError);

    return float4(sampleUv, waterDistance, confidence);
}

float4 ShadeWater(FullscreenOutput input)
{
    float2 uv = input.UV;
    float sceneDepth = composite.SceneDepth.Load(int3(int2(input.Position.xy), 0)).r;
    float3 scene = SceneAt(uv);

    if (composite.RenderMode == 1)
    {
        return float4(scene, 1.0);
    }

    float3 normal;
    float3 fluid = SampleFluid(uv, sceneDepth, normal);
    float fluidDepth = fluid.x;
    float thickness = fluid.y;
    float surfaceConfidence = fluid.z * smoothstep(0.0, 0.025, thickness);

    if (surfaceConfidence <= 0.01)
    {
        return float4(scene, 1.0);
    }

    float3 normalWorld = normalize(normal);
    float3 normalView = normalize(mul((float3x3)composite.InvView, normalWorld));

    float3 viewPosition = ReconstructViewPosition(uv, fluidDepth);
    float3 worldPosition = mul(float4(viewPosition, 1.0), composite.InvView).xyz;
    float3 viewDirection = normalize(composite.CameraPosition - worldPosition);
    float ndotv = saturate(dot(normalWorld, viewDirection));
    float iorRatio = (composite.Ior - 1.0) / (composite.Ior + 1.0);
    float3 f0 = float3(iorRatio * iorRatio);
    float3 fresnel = FresnelSchlick(ndotv, f0);
    float3 incident = normalize(viewPosition);
    float opticalDepth = sceneDepth > 0.0 ? min(thickness, (sceneDepth - fluidDepth) / -incident.z) : thickness;
    float4 refraction = TraceRefraction(uv, sceneDepth, viewPosition, incident, normalView, thickness);
    opticalDepth = lerp(opticalDepth, refraction.z, refraction.w);

    float3 reflected = EnvironmentColor(reflect(-viewDirection, normalWorld));

    if (composite.RayTracingEnabled)
    {
        float4 reflectionSample = composite.Reflection.SampleLevel(composite.Sampler, uv, 0.0);
        reflected = reflectionSample.rgb / reflectionSample.a;
    }

    float3 transmission = exp(-composite.Absorption * opticalDepth / composite.Clarity);
    float3 inScatter = composite.WaterColor * (1.0 - transmission) * EnvironmentColor(normalWorld);
    float3 refracted = lerp(scene, SceneAt(refraction.xy), refraction.w) * transmission + inScatter;
    float3 lightDirection = normalize(-composite.SunDirection);
    float3 sunSpecular = DirectSpecular(normalWorld, viewDirection, lightDirection, 0.12, f0) * SunColor * composite.LightIntensity;

    float3 color = lerp(refracted, reflected, fresnel);
    color += sunSpecular;

    color = lerp(scene, color, surfaceConfidence);

    return float4(color, 1.0);
}

[shader("fragment")]
float4 CompositeFS(FullscreenOutput input) : SV_TARGET
{
    return ShadeWater(input);
}
