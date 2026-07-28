struct BlurConstants
{
    private uint4 DimensionsAndDirection;
    private float4 FilterParameters;
    DescriptorHandle<Texture2D<float>> InputTexture;
    DescriptorHandle<RWTexture2D<float>> OutputTexture;

    property uint Width
    {
        get {
            return DimensionsAndDirection.x;
        }
    }
    property uint Height
    {
        get {
            return DimensionsAndDirection.y;
        }
    }
    property uint Direction
    {
        get {
            return DimensionsAndDirection.z;
        }
    }
    property uint Radius
    {
        get {
            return DimensionsAndDirection.w;
        }
    }
    property float SpatialSigma
    {
        get {
            return FilterParameters.x;
        }
    }
    property float DepthSigma
    {
        get {
            return FilterParameters.y;
        }
    }
    property float ProjectedRadiusScale
    {
        get {
            return FilterParameters.z;
        }
    }
};

ConstantBuffer<BlurConstants> blur;

[shader("compute")]
[numthreads(16, 16, 1)]
void BlurCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    if (pixel.x >= blur.Width || pixel.y >= blur.Height)
    {
        return;
    }

    float centerDepth = (*blur.InputTexture).Load(int3(pixel, 0));
    if (centerDepth <= 0.0)
    {
        int2 direction = blur.Direction == 0 ? int2(1, 0) : int2(0, 1);
        int bridgeRadius = min(int(blur.Radius), 4);
        float negativeDepth = 0.0;
        float positiveDepth = 0.0;
        int negativeDistance = 0;
        int positiveDistance = 0;

        for (int offset = 1; offset <= bridgeRadius; offset++)
        {
            int2 negativePixel = clamp(int2(pixel) - direction * offset, int2(0, 0), int2(int(blur.Width) - 1, int(blur.Height) - 1));
            int2 positivePixel = clamp(int2(pixel) + direction * offset, int2(0, 0), int2(int(blur.Width) - 1, int(blur.Height) - 1));

            if (negativeDepth <= 0.0)
            {
                negativeDepth = (*blur.InputTexture).Load(int3(negativePixel, 0));
                negativeDistance = negativeDepth > 0.0 ? offset : 0;
            }

            if (positiveDepth <= 0.0)
            {
                positiveDepth = (*blur.InputTexture).Load(int3(positivePixel, 0));
                positiveDistance = positiveDepth > 0.0 ? offset : 0;
            }

            if (negativeDepth > 0.0 && positiveDepth > 0.0)
            {
                break;
            }
        }

        float depthDifference = abs(negativeDepth - positiveDepth);
        float bridgeRange = blur.DepthSigma * (0.85 + min(negativeDepth, positiveDepth) * 0.015) * 2.5;

        if (negativeDepth > 0.0 && positiveDepth > 0.0 && depthDifference < bridgeRange)
        {
            float distanceSum = float(negativeDistance + positiveDistance);
            (*blur.OutputTexture)[pixel] = (negativeDepth * positiveDistance + positiveDepth * negativeDistance) / distanceSum;

            return;
        }

        (*blur.OutputTexture)[pixel] = 0.0;

        return;
    }

    float weightedDepth = centerDepth;
    float weightSum = 1.0;
    int2 direction = blur.Direction == 0 ? int2(1, 0) : int2(0, 1);
    float projectedRadius = blur.ProjectedRadiusScale / max(centerDepth, 0.1);
    int localRadius = clamp(int(projectedRadius * 0.62 + 0.5), 4, int(blur.Radius));
    float spatialSigma = max(blur.SpatialSigma, float(localRadius) * 0.58);
    float range = blur.DepthSigma * (0.85 + centerDepth * 0.015);

    for (int offset = 1; offset <= localRadius; offset++)
    {
        float spatialWeight = exp(-float(offset * offset) / (2.0 * spatialSigma * spatialSigma));

        for (int sign = -1; sign <= 1; sign += 2)
        {
            int2 samplePixel = clamp(int2(pixel) + direction * offset * sign, int2(0, 0), int2(int(blur.Width) - 1, int(blur.Height) - 1));
            float sampleDepth = (*blur.InputTexture).Load(int3(samplePixel, 0));

            if (sampleDepth <= 0.0)
            {
                continue;
            }

            float difference = abs(sampleDepth - centerDepth);

            if (difference > range * 2.5)
            {
                continue;
            }

            float depthWeight = exp(-(difference * difference) / (2.0 * range * range));
            float weight = spatialWeight * depthWeight;

            weightedDepth += sampleDepth * weight;
            weightSum += weight;
        }
    }

    (*blur.OutputTexture)[pixel] = weightedDepth / weightSum;
}

[shader("compute")]
[numthreads(16, 16, 1)]
void BlurThicknessCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    if (pixel.x >= blur.Width || pixel.y >= blur.Height)
    {
        return;
    }

    float weightedThickness = (*blur.InputTexture).Load(int3(pixel, 0));
    float weightSum = 1.0;
    int2 direction = blur.Direction == 0 ? int2(1, 0) : int2(0, 1);

    for (int offset = 1; offset <= int(blur.Radius); offset++)
    {
        float weight = exp(-float(offset * offset) / (2.0 * blur.SpatialSigma * blur.SpatialSigma));

        for (int sign = -1; sign <= 1; sign += 2)
        {
            int2 samplePixel = clamp(int2(pixel) + direction * offset * sign, int2(0, 0), int2(int(blur.Width) - 1, int(blur.Height) - 1));
            weightedThickness += (*blur.InputTexture).Load(int3(samplePixel, 0)) * weight;
            weightSum += weight;
        }
    }

    (*blur.OutputTexture)[pixel] = weightedThickness / weightSum;
}
