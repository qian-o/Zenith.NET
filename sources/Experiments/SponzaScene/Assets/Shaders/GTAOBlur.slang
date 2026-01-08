static const float EPSILON = 0.0001;

static const float SPATIAL_SIGMA_SCALE = 0.5;
static const float RANGE_SIGMA = 0.15;
static const float DEPTH_SIGMA = 0.5;

struct BlurConstants
{
    float2 TexelSize;

    int BlurSize;
};

ConstantBuffer<BlurConstants> constants;
Texture2D positionTexture;
Texture2D gtaoTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;

// ============================================
// Utility
// ============================================

float GetLinearDepth(float2 uv)
{
    return positionTexture.SampleLevel(pointSampler, uv, 0).w;
}

float GaussianWeight(float offset, float sigma)
{
    float sigmaSq2 = 2.0 * sigma * sigma;

    return exp(-(offset * offset) / sigmaSq2);
}

float DepthWeight(float centerDepth, float sampleDepth)
{
    if (centerDepth < EPSILON || sampleDepth < EPSILON)
    {
        return 0.0;
    }

    float depthDiff = abs(centerDepth - sampleDepth) / max(centerDepth, EPSILON);

    return exp(-(depthDiff * depthDiff) / (2.0 * DEPTH_SIGMA * DEPTH_SIGMA));
}

float RangeWeight(float centerValue, float sampleValue)
{
    float diff = centerValue - sampleValue;

    return exp(-(diff * diff) / (2.0 * RANGE_SIGMA * RANGE_SIGMA));
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

    float centerValue = gtaoTexture.SampleLevel(pointSampler, uv, 0).r;
    float centerDepth = GetLinearDepth(uv);

    float result = centerValue;
    float totalWeight = 1.0;

    int blurSize = constants.BlurSize;
    float spatialSigma = float(blurSize) * SPATIAL_SIGMA_SCALE;

    for (int y = -blurSize; y <= blurSize; y++)
    {
        for (int x = -blurSize; x <= blurSize; x++)
        {
            if (x == 0 && y == 0)
            {
                continue;
            }

            float2 offset = float2(x, y) * constants.TexelSize;
            float2 sampleUV = uv + offset;

            if (any(sampleUV < 0.0) || any(sampleUV > 1.0))
            {
                continue;
            }

            float sampleValue = gtaoTexture.SampleLevel(pointSampler, sampleUV, 0).r;
            float sampleDepth = GetLinearDepth(sampleUV);

            float dist = length(float2(x, y));
            float wSpatial = GaussianWeight(dist, spatialSigma);
            float wDepth = DepthWeight(centerDepth, sampleDepth);
            float wRange = RangeWeight(centerValue, sampleValue);

            float weight = wSpatial * wDepth * wRange;

            result += sampleValue * weight;
            totalWeight += weight;
        }
    }

    result /= max(totalWeight, EPSILON);

    outputTexture[dispatchThreadID.xy] = result;
}
