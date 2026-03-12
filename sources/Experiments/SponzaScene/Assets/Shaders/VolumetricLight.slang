static const float PI = 3.14159265359;
static const float EPSILON = 0.0001;

static const float MAX_PHASE = 0.5;
static const float SUN_VIEW_ATTENUATION = 0.95;
static const float INTENSITY_SCALE = 0.005;

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

    private float2 padding0;
};

struct CSMData
{
    float4x4 View;

    float4x4 Projection;

    float NearPlane;

    float FarPlane;

    private float2 padding0;
};

ConstantBuffer<VolumetricLightConstants> constants;
StructuredBuffer<CSMData> csmDatas;
Texture2D positionTexture;
Texture2DArray csmTextures;
RWTexture2D outputTexture;
SamplerState pointSampler;
SamplerComparisonState shadowSampler;

// ============================================
// Utility
// ============================================

float InterleavedGradientNoise(float2 pos)
{
    const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);

    return frac(magic.z * frac(dot(pos, magic.xy)));
}

float HenyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    float phase = (1.0 - g2) / (4.0 * PI * pow(max(denom, EPSILON), 1.5));

    return min(phase, MAX_PHASE);
}

// ============================================
// Shadow Sampling
// ============================================

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

float SampleShadow(float3 worldPos, int cascadeIndex)
{
    float4 lightSpacePos = mul(mul(float4(worldPos, 1.0), csmDatas[cascadeIndex].View), csmDatas[cascadeIndex].Projection);
    lightSpacePos.xyz /= lightSpacePos.w;

    float3 shadowCoord = float3(lightSpacePos.x * 0.5 + 0.5, -lightSpacePos.y * 0.5 + 0.5, lightSpacePos.z);

    if (any(shadowCoord.xy < 0.0) || any(shadowCoord.xy > 1.0) || shadowCoord.z < 0.0 || shadowCoord.z > 1.0)
    {
        return 1.0;
    }

    return csmTextures.SampleCmpLevelZero(shadowSampler, float3(shadowCoord.xy, cascadeIndex), shadowCoord.z);
}

// ============================================
// Ray Marching
// ============================================

float3 GetWorldPosition(float2 ndc)
{
    float4 worldPos = mul(float4(ndc, 1.0, 1.0), constants.InverseViewProjection);

    return worldPos.xyz / worldPos.w;
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
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);

    float3 cameraPos = constants.CameraPosition.xyz;
    float3 farWorldPos = GetWorldPosition(ndc);
    float3 rayDir = normalize(farWorldPos - cameraPos);

    float3 surfacePos = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;
    float surfaceDistSq = dot(surfacePos, surfacePos);
    float maxRayDistance = surfaceDistSq > EPSILON ? length(surfacePos - cameraPos) : constants.MaxDistance;
    maxRayDistance = min(maxRayDistance, constants.MaxDistance);

    float3 lightDir = normalize(constants.LightDirection.xyz);
    float cosTheta = dot(rayDir, -lightDir);

    float sunAlignment = max(cosTheta, 0.0);
    float sunViewFactor = saturate(1.0 - pow(sunAlignment, 2.0) * SUN_VIEW_ATTENUATION);
    float phase = HenyeyGreenstein(cosTheta, constants.Scattering);

    float noise = InterleavedGradientNoise(float2(dispatchThreadID.xy));
    float stepSize = maxRayDistance / float(constants.SampleCount);
    float3 rayStep = rayDir * stepSize;

    float accumulation = 0.0;
    float3 samplePos = cameraPos + rayDir * noise * stepSize;

    int currentCascade = 0;
    float currentCascadeFar = csmDatas[0].FarPlane;

    for (int i = 0; i < constants.SampleCount; i++)
    {
        float distanceToCamera = float(i + 1) * stepSize;

        while (distanceToCamera > currentCascadeFar && currentCascade < int(csmDatas.getCount()) - 1)
        {
            currentCascade++;
            currentCascadeFar = csmDatas[currentCascade].FarPlane;
        }

        float shadow = SampleShadow(samplePos, currentCascade);

        accumulation += shadow;

        samplePos += rayStep;
    }

    float volumetric = accumulation * stepSize * phase * sunViewFactor * constants.LightColor.w * constants.Intensity * INTENSITY_SCALE;

    outputTexture[dispatchThreadID.xy] = volumetric;
}
