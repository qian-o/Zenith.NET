static const float PI = 3.14159265359;
static const float EPSILON = 0.0001;

struct VolumetricLightConstants
{
    float4 CameraPosition;

    float4 LightDirection;

    float4 LightColor;

    float4x4 InverseViewProjection;

    float2 ScreenSize;

    int SampleCount;

    float Intensity;

    float Scattering;

    float MaxDistance;
};

struct CSMData
{
    float4x4 View;

    float4x4 Projection;

    float NearPlane;

    float FarPlane;
};

ConstantBuffer<VolumetricLightConstants> constants;
StructuredBuffer<CSMData> csmDatas;
Texture2D positionTexture;
Texture2DArray csmTextures;
RWTexture2D<float> outputTexture;
SamplerState pointSampler;
SamplerComparisonState shadowSampler;

float InterleavedGradientNoise(float2 pos)
{
    const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);

    return frac(magic.z * frac(dot(pos, magic.xy)));
}

float HenyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;

    return (1.0 - g2) / (4.0 * PI * pow(max(denom, EPSILON), 1.5));
}

int GetCascadeIndex(float distanceToCamera)
{
    uint cascadeCount = csmDatas.getCount();

    for (uint i = 0; i < cascadeCount; i++)
    {
        if (distanceToCamera < csmDatas[i].FarPlane)
        {
            return int(i);
        }
    }

    return int(cascadeCount - 1);
}

float3 WorldToShadowCoord(float3 worldPos, int cascadeIndex)
{
    float4 lightSpacePos = mul(mul(float4(worldPos, 1.0), csmDatas[cascadeIndex].View), csmDatas[cascadeIndex].Projection);
    lightSpacePos.xyz /= lightSpacePos.w;

    return float3(lightSpacePos.x * 0.5 + 0.5, -lightSpacePos.y * 0.5 + 0.5, lightSpacePos.z);
}

float SampleShadow(float3 worldPos, float3 cameraPos)
{
    float distanceToCamera = length(worldPos - cameraPos);
    int cascadeIndex = GetCascadeIndex(distanceToCamera);

    float3 shadowCoord = WorldToShadowCoord(worldPos, cascadeIndex);

    if (any(shadowCoord.xy < 0.0) || any(shadowCoord.xy > 1.0) || shadowCoord.z < 0.0 || shadowCoord.z > 1.0)
    {
        return 1.0;
    }

    return csmTextures.SampleCmpLevelZero(shadowSampler, float3(shadowCoord.xy, cascadeIndex), shadowCoord.z);
}

float3 GetViewDirection(float2 uv)
{
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 worldPos = mul(float4(ndc, 1.0, 1.0), constants.InverseViewProjection);

    return normalize(worldPos.xyz / worldPos.w - constants.CameraPosition.xyz);
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputTexture.GetDimensions(width, height);

    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
    {
        return;
    }

    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);

    float3 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;
    float3 cameraPos = constants.CameraPosition.xyz;
    float3 lightDir = normalize(constants.LightDirection.xyz);
    float3 lightColor = constants.LightColor.rgb;
    float lightIntensity = constants.LightColor.w;

    float3 rayDir = GetViewDirection(uv);
    float3 rayOrigin = cameraPos;

    float maxRayDistance = length(worldPos) > EPSILON ? length(worldPos - cameraPos) : constants.MaxDistance;
    maxRayDistance = min(maxRayDistance, constants.MaxDistance);

    float cosTheta = dot(rayDir, -lightDir);
    float phase = HenyeyGreenstein(cosTheta, constants.Scattering);

    float noise = InterleavedGradientNoise(float2(dispatchThreadID.xy));
    float stepSize = maxRayDistance / float(constants.SampleCount);

    float accumulation = 0.0;

    for (int i = 0; i < constants.SampleCount; i++)
    {
        float t = (float(i) + noise) * stepSize;
        float3 samplePos = rayOrigin + rayDir * t;

        float shadow = SampleShadow(samplePos, cameraPos);

        float density = 1.0;

        accumulation += shadow * density * stepSize;
    }

    float volumetric = accumulation * phase * lightIntensity * constants.Intensity * 0.01;

    outputTexture[dispatchThreadID.xy] = volumetric;
}
