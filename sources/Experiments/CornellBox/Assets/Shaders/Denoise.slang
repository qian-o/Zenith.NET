struct TemporalConstants
{
    float4 SizeRcp;

    float4 Jitter;

    uint4 ResetCameraPadding;

    DescriptorHandle<Texture2D<float4>> Color;

    DescriptorHandle<Texture2D<float4>> Normal;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float4>> PreviousColor;

    DescriptorHandle<Texture2D<float4>> PreviousMoments;

    DescriptorHandle<Texture2D<float4>> PreviousGeometry;

    DescriptorHandle<RWTexture2D<float4>> MomentsHistory;

    DescriptorHandle<RWTexture2D<float4>> GeometryHistory;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<RWTexture2D<float4>> ColorHistory;

    float4x4 ClipToPrevClip;
};

struct AtrousConstants
{
    float4 SizeRcp;

    uint4 StepResetCamera;

    DescriptorHandle<Texture2D<float4>> Normal;

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float4>> PreviousGeometry;

    DescriptorHandle<Texture2D<float4>> PreviousResolved;

    DescriptorHandle<RWTexture2D<float4>> ResolvedHistory;

    float4 Jitter;

    float4x4 ClipToPrevClip;
};

ConstantBuffer<TemporalConstants> temporal : register(b0);

ConstantBuffer<AtrousConstants> atrous : register(b0);

float Kernel(int offset)
{
    return offset == 0 ? 0.375 : (abs(offset) == 1 ? 0.25 : 0.0625);
}

float4 CubicWeights(float fraction)
{
    float square = fraction * fraction;
    float cube = square * fraction;

    return float4(-0.5 * fraction + square - 0.5 * cube, 1.0 - 2.5 * square + 1.5 * cube,
                  0.5 * fraction + 2.0 * square - 1.5 * cube, -0.5 * square + 0.5 * cube);
}

float GeometryWeight(float4 center, float4 sample, float colorError = 0.0)
{
    // Static quads store identical normal and plane offset values.
    return all(center == sample) ? exp(-colorError) : 0.0;
}

bool ReprojectMotion(int2 pixel, float4 sizeRcp, float4 jitter, float depth, float4x4 clipToPrevClip, out float2 motion)
{
    motion = 0.0;

    float2 uv = (float2(pixel) + 0.5 - jitter.xy) / sizeRcp.xy;
    float2 currentNdc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 previousClip = mul(float4(currentNdc, depth, 1.0), clipToPrevClip);

    if (previousClip.w <= 0.0)
    {
        return false;
    }

    float2 previousNdc = previousClip.xy / previousClip.w;

    if (any(abs(previousNdc) > 1.0))
    {
        return false;
    }

    motion = currentNdc - previousNdc;

    return true;
}

[shader("compute")]
[numthreads(16, 16, 1)]
void TemporalMain(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    int2 pixel = int2(dispatchThreadId.xy);
    int2 size = int2(temporal.SizeRcp.xy);

    if (any(pixel >= size))
    {
        return;
    }

    float3 current = max(temporal.Color.Load(int3(pixel, 0)).rgb, 0.0);
    float4 normal = temporal.Normal.Load(int3(pixel, 0));

    if (all(normal.xyz == 0.0))
    {
        temporal.MomentsHistory[pixel] = float4(0.0, 0.0, 1.0, 1.0);
        temporal.GeometryHistory[pixel] = normal;
        temporal.Output[pixel] = float4(current, 0.0);
        temporal.ColorHistory[pixel] = float4(current, 1.0);

        return;
    }

    float3 neighborhoodMean = 0.0;
    float3 neighborhoodSecond = 0.0;
    float3 neighborhoodMin = current;
    float3 neighborhoodMax = current;
    float neighborhoodWeight = 0.0;

    for (int y = -2; y <= 2; y++)
    {
        for (int x = -2; x <= 2; x++)
        {
            int2 offset = int2(x, y);
            int2 samplePixel = clamp(pixel + offset, 0, size - 1);
            float4 sampleNormal = temporal.Normal.Load(int3(samplePixel, 0));
            float3 sampleColor = max(temporal.Color.Load(int3(samplePixel, 0)).rgb, 0.0);
            float weight = GeometryWeight(normal, sampleNormal) * Kernel(x) * Kernel(y);

            neighborhoodMean += sampleColor * weight;
            neighborhoodSecond += sampleColor * sampleColor * weight;
            neighborhoodWeight += weight;

            if (weight > 0.001)
            {
                neighborhoodMin = min(neighborhoodMin, sampleColor);
                neighborhoodMax = max(neighborhoodMax, sampleColor);
            }
        }
    }

    neighborhoodMean /= neighborhoodWeight;
    neighborhoodSecond /= neighborhoodWeight;

    float3 previousColor = 0.0;
    float3 cubicColor = 0.0;
    bool cubicValid = false;
    float4 previousMoments = 0.0;
    float historyWeight = 0.0;
    float2 motion = 0.0;
    bool validHistory = true;

    if (temporal.ResetCameraPadding.y == 0)
    {
        validHistory = ReprojectMotion(pixel, temporal.SizeRcp, temporal.Jitter, temporal.Depth.Load(int3(pixel, 0)),
                                       temporal.ClipToPrevClip, motion);
    }

    float2 previousUV = (float2(pixel) + 0.5 + temporal.Jitter.zw - temporal.Jitter.xy) * temporal.SizeRcp.zw +
                        motion * float2(-0.5, 0.5);
    float2 previousPosition = previousUV * temporal.SizeRcp.xy - 0.5;
    int2 previousBase = int2(floor(previousPosition));
    float2 fraction = frac(previousPosition);

    if (temporal.ResetCameraPadding.x == 0 && validHistory)
    {
        float4 weightsX = CubicWeights(fraction.x);
        float4 weightsY = CubicWeights(fraction.y);

        // Signed reconstruction requires the complete geometry footprint.
        cubicValid = true;

        for (int y = -1; y <= 2; y++)
        {
            for (int x = -1; x <= 2; x++)
            {
                int2 samplePixel = previousBase + int2(x, y);

                if (any(samplePixel < 0) || any(samplePixel >= size))
                {
                    cubicValid = false;

                    continue;
                }

                float4 sampleGeometry = temporal.PreviousGeometry.Load(int3(samplePixel, 0));

                if (any(normal != sampleGeometry))
                {
                    cubicValid = false;

                    continue;
                }

                float3 sampleColor = temporal.PreviousColor.Load(int3(samplePixel, 0)).rgb;
                cubicColor += sampleColor * weightsX[x + 1] * weightsY[y + 1];

                if (x >= 0 && x <= 1 && y >= 0 && y <= 1)
                {
                    float weight = (x == 0 ? 1.0 - fraction.x : fraction.x) * (y == 0 ? 1.0 - fraction.y : fraction.y);
                    previousColor += sampleColor * weight;
                    float4 sampleMoments = temporal.PreviousMoments.Load(int3(samplePixel, 0));
                    previousMoments += sampleMoments * weight;
                    historyWeight += weight;
                }
            }
        }
    }

    float historyLength = 1.0;
    float squaredWeightSum = 1.0;
    float3 accumulated = current;
    float colorVariance = 0.0;

    if (historyWeight > 0.01)
    {
        previousColor = cubicValid ? cubicColor : previousColor / historyWeight;
        previousMoments /= historyWeight;

        float3 deviation = sqrt(max(neighborhoodSecond - neighborhoodMean * neighborhoodMean, 0.0));
        float3 minimum = max(neighborhoodMin, neighborhoodMean - 3.0 * deviation);
        float3 maximum = min(neighborhoodMax, neighborhoodMean + 3.0 * deviation);
        previousColor = clamp(previousColor, minimum, maximum);
        historyLength = min(previousMoments.z + 1.0, 64.0);

        float motionPixels = length(motion * temporal.SizeRcp.xy * 0.5);
        float motionWeight = saturate(motionPixels * 0.25);
        float alpha =
            max(temporal.ResetCameraPadding.y != 0 ? 0.025 : lerp(0.25, 0.5, motionWeight), 1.0 / historyLength);

        // Track temporal weights without modeling spatial covariance.
        squaredWeightSum = (1.0 - alpha) * (1.0 - alpha) * previousMoments.w + alpha * alpha;
        accumulated = lerp(previousColor, current, alpha);
        float3 difference = current - previousColor;

        // Update the sum of RGB central variances with the color history weights.
        colorVariance = (1.0 - alpha) * (previousMoments.x + alpha * dot(difference, difference));

        if (temporal.ResetCameraPadding.y == 0)
        {
            historyLength = min(historyLength, 1.0 / alpha);
        }
    }

    // Estimate variance of the temporal mean from its weighted central moment.
    float variance = colorVariance * squaredWeightSum;

    if (historyLength > 1.0)
    {
        variance /= 1.0 - squaredWeightSum;
    }

    if (historyLength < 4.0)
    {
        float3 spatialVarianceRgb = max(neighborhoodSecond - neighborhoodMean * neighborhoodMean, 0.0);
        float spatialVariance = spatialVarianceRgb.x + spatialVarianceRgb.y + spatialVarianceRgb.z;
        variance = max(variance, spatialVariance * 4.0 / historyLength);
    }

    temporal.MomentsHistory[pixel] = float4(colorVariance, 0.0, historyLength, squaredWeightSum);
    temporal.GeometryHistory[pixel] = normal;
    temporal.Output[pixel] = float4(accumulated, min(variance, 65504.0));

    // Keep temporal history independent of spatial filtering.
    temporal.ColorHistory[pixel] = float4(accumulated, 1.0);
}

void Resolve(int2 pixel, int2 size, float3 color, float4 normal)
{
    float4 resolved = float4(color, 1.0);

    if (atrous.StepResetCamera.y == 0)
    {
        if (atrous.StepResetCamera.z != 0)
        {
            // Each jittered sample belongs to this fixed pixel's footprint.
            float4 previous = atrous.PreviousResolved.Load(int3(pixel, 0));
            float historyLength = min(previous.a + 1.0, 64.0);
            resolved = float4(lerp(previous.rgb, color, 1.0 / historyLength), historyLength);
        }
        else if (any(normal.xyz != 0.0))
        {
            float2 motion;

            if (ReprojectMotion(pixel, atrous.SizeRcp, atrous.Jitter, atrous.Depth.Load(int3(pixel, 0)),
                                atrous.ClipToPrevClip, motion))
            {
                float2 motionPixels = motion * atrous.SizeRcp.xy * float2(-0.5, 0.5);
                float2 previousPosition = float2(pixel) + motionPixels;
                int2 previousBase = int2(floor(previousPosition));
                float2 fraction = frac(previousPosition);
                float4 previous = 0.0;
                float historyWeight = 0.0;

                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        int2 samplePixel = previousBase + int2(x, y);

                        if (any(samplePixel < 0) || any(samplePixel >= size))
                        {
                            continue;
                        }

                        int2 sampleGeometryPixel =
                            int2(floor(float2(samplePixel) + atrous.Jitter.zw - atrous.Jitter.xy + 0.5));

                        if (any(sampleGeometryPixel < 0) || any(sampleGeometryPixel >= size))
                        {
                            continue;
                        }

                        if (any(atrous.PreviousGeometry.Load(int3(sampleGeometryPixel, 0)) != normal))
                        {
                            continue;
                        }

                        float weight =
                            (x == 0 ? 1.0 - fraction.x : fraction.x) * (y == 0 ? 1.0 - fraction.y : fraction.y);
                        previous += atrous.PreviousResolved.Load(int3(samplePixel, 0)) * weight;
                        historyWeight += weight;
                    }
                }

                if (historyWeight > 0.01)
                {
                    previous /= historyWeight;
                    float3 minimum = color;
                    float3 maximum = color;

                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            int2 samplePixel = clamp(pixel + int2(x, y), 0, size - 1);

                            if (any(atrous.Normal.Load(int3(samplePixel, 0)) != normal))
                            {
                                continue;
                            }

                            float3 sampleColor = atrous.Input.Load(int3(samplePixel, 0)).rgb;
                            minimum = min(minimum, sampleColor);
                            maximum = max(maximum, sampleColor);
                        }
                    }

                    float historyLength = min(previous.a + 1.0, 64.0);
                    float alpha = max(lerp(0.125, 0.5, saturate(length(motionPixels) * 0.25)), 1.0 / historyLength);
                    resolved = float4(lerp(clamp(previous.rgb, minimum, maximum), color, alpha),
                                      min(historyLength, 1.0 / alpha));
                }
            }
        }
    }

    atrous.ResolvedHistory[pixel] = resolved;
}

[shader("compute")]
[numthreads(16, 16, 1)]
void AtrousMain(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    int2 pixel = int2(dispatchThreadId.xy);
    int2 size = int2(atrous.SizeRcp.xy);

    if (any(pixel >= size))
    {
        return;
    }

    float4 normal = atrous.Normal.Load(int3(pixel, 0));

    if (all(normal.xyz == 0.0))
    {
        // Temporal and the first iteration refresh both ping-pong pixels.
        if (atrous.StepResetCamera.x == 1)
        {
            atrous.Output[pixel] = atrous.Input.Load(int3(pixel, 0));
        }

        if (atrous.StepResetCamera.x == 16)
        {
            Resolve(pixel, size, atrous.Input.Load(int3(pixel, 0)).rgb, normal);
        }

        return;
    }

    float4 center = atrous.Input.Load(int3(pixel, 0));
    float variance = 0.0;
    float varianceWeight = 0.0;

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            int2 offset = int2(x, y);
            int2 samplePixel = clamp(pixel + offset, 0, size - 1);
            float4 sampleNormal = atrous.Normal.Load(int3(samplePixel, 0));
            float weight = GeometryWeight(normal, sampleNormal) * (x == 0 ? 2.0 : 1.0) * (y == 0 ? 2.0 : 1.0);
            variance += atrous.Input.Load(int3(samplePixel, 0)).a * weight;
            varianceWeight += weight;
        }
    }

    float colorScale = 4.0 * sqrt(max(variance / varianceWeight, 0.0)) + sqrt(3.0) * 0.001;
    float3 sum = 0.0;
    float sumVariance = 0.0;
    float sumWeight = 0.0;

    for (int y = -2; y <= 2; y++)
    {
        for (int x = -2; x <= 2; x++)
        {
            int2 offset = int2(x, y) * int(atrous.StepResetCamera.x);
            int2 samplePixel = pixel + offset;

            if (any(samplePixel < 0) || any(samplePixel >= size))
            {
                continue;
            }

            float4 sampleNormal = atrous.Normal.Load(int3(samplePixel, 0));
            float4 sampleColor = atrous.Input.Load(int3(samplePixel, 0));
            float colorDifference = length(sampleColor.rgb - center.rgb);
            float weight =
                Kernel(x) * Kernel(y) * GeometryWeight(normal, sampleNormal, colorDifference / colorScale);

            sum += sampleColor.rgb * weight;
            sumVariance += sampleColor.a * weight * weight;
            sumWeight += weight;
        }
    }

    float3 color = sum / sumWeight;
    atrous.Output[pixel] = float4(color, sumVariance / (sumWeight * sumWeight));

    if (atrous.StepResetCamera.x == 16)
    {
        Resolve(pixel, size, color, normal);
    }
}
