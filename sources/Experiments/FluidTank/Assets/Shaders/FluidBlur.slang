struct BlurConstants
{
    private uint4 DimensionsAndDirection;
    private float4 FilterParameters;
    private uint4 SourceDimensionsAndPadding;
    DescriptorHandle<Texture2D<float>> InputTexture;
    DescriptorHandle<RWTexture2D<float>> OutputTexture;

    property uint Width { get { return DimensionsAndDirection.x; } }
    property uint Height { get { return DimensionsAndDirection.y; } }
    property uint Direction { get { return DimensionsAndDirection.z; } }
    property uint Radius { get { return DimensionsAndDirection.w; } }
    property float SpatialSigma { get { return FilterParameters.x; } }
    property float DepthSigma { get { return FilterParameters.y; } }
    property float ProjectedRadiusScale { get { return FilterParameters.z; } }
    property uint SourceWidth { get { return SourceDimensionsAndPadding.x; } }
    property uint SourceHeight { get { return SourceDimensionsAndPadding.y; } }
};

ConstantBuffer<BlurConstants> blur;

[shader("compute")]
[numthreads(16, 16, 1)]
void DownsampleCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    if (pixel.x >= blur.Width || pixel.y >= blur.Height)
    {
        return;
    }

    uint2 source = pixel * 2;
    float nearestDepth = 0.0;

    for (uint y = 0; y < 2; y++)
    {
        for (uint x = 0; x < 2; x++)
        {
            uint2 samplePixel = min(source + uint2(x, y), uint2(blur.SourceWidth - 1, blur.SourceHeight - 1));
            float depth = (*blur.InputTexture).Load(int3(samplePixel, 0));

            if (depth > 0.0 && (nearestDepth <= 0.0 || depth < nearestDepth))
            {
                nearestDepth = depth;
            }
        }
    }

    (*blur.OutputTexture)[pixel] = nearestDepth;
}

[shader("compute")]
[numthreads(16, 16, 1)]
void DownsampleThicknessCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    if (pixel.x >= blur.Width || pixel.y >= blur.Height)
    {
        return;
    }

    uint2 source = pixel * 2;
    float thickness = 0.0;

    for (uint y = 0; y < 2; y++)
    {
        for (uint x = 0; x < 2; x++)
        {
            uint2 samplePixel = min(source + uint2(x, y), uint2(blur.SourceWidth - 1, blur.SourceHeight - 1));
            thickness += (*blur.InputTexture).Load(int3(samplePixel, 0));
        }
    }

    (*blur.OutputTexture)[pixel] = thickness * 0.25;
}

[shader("compute")]
[numthreads(16, 16, 1)]
void BlurCS(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    if (pixel.x >= blur.Width || pixel.y >= blur.Height)
    {
        return;
    }

    float centerDepth = (*blur.InputTexture).Load(int3(pixel, 0));
    if (centerDepth <= 0.0)
    {
        (*blur.OutputTexture)[pixel] = 0.0;

        return;
    }

    float weightedDepth = centerDepth;
    float weightSum = 1.0;
    int2 direction = blur.Direction == 0 ? int2(1, 0) : int2(0, 1);
    float projectedRadius = blur.ProjectedRadiusScale / max(centerDepth, 0.1);
    int localRadius = clamp(int(projectedRadius * 0.62 + 0.5), 4, int(blur.Radius));
    float spatialSigma = max(blur.SpatialSigma, float(localRadius) * 0.58);

    for (int offset = 1; offset <= localRadius; offset++)
    {
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int2 samplePixel = clamp(int2(pixel) + direction * offset * sign, int2(0, 0), int2(int(blur.Width) - 1, int(blur.Height) - 1));
            float sampleDepth = (*blur.InputTexture).Load(int3(samplePixel, 0));

            if (sampleDepth <= 0.0)
            {
                continue;
            }

            float spatialWeight = exp(-float(offset * offset) / (2.0 * spatialSigma * spatialSigma));
            float difference = abs(sampleDepth - centerDepth);
            float range = blur.DepthSigma * (0.85 + centerDepth * 0.015);

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
void BlurThicknessCS(uint3 dispatchThreadID : SV_DispatchThreadID)
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