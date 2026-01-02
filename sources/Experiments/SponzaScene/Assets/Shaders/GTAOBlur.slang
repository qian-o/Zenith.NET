struct BlurConstants
{
    float2 TexelSize;

    int BlurSize;
};

ConstantBuffer<BlurConstants> constants;
Texture2D gtaoTexture;
RWTexture2D<float> outputTexture;
SamplerState pointSampler;

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
    
    float result = centerValue;
    float totalWeight = 1.0;
    
    int blurSize = constants.BlurSize;
    
    // 使用高斯权重的边缘保留模糊
    float sigma = float(blurSize) * 0.5;
    float sigmaSq2 = 2.0 * sigma * sigma;
    
    // 值域 sigma - 控制边缘保留程度
    float rangeSigma = 0.1;
    float rangeSigmaSq2 = 2.0 * rangeSigma * rangeSigma;

    for (int x = -blurSize; x <= blurSize; ++x)
    {
        for (int y = -blurSize; y <= blurSize; ++y)
        {
            if (x == 0 && y == 0)
            {
                continue;
            }

            float2 offset = float2(float(x), float(y)) * constants.TexelSize;
            float sampleValue = gtaoTexture.SampleLevel(pointSampler, uv + offset, 0).r;
            
            // 空间权重（高斯）
            float distSq = float(x * x + y * y);
            float spatialWeight = exp(-distSq / sigmaSq2);
            
            // 值域权重（边缘保留）
            float valueDiff = sampleValue - centerValue;
            float rangeWeight = exp(-(valueDiff * valueDiff) / rangeSigmaSq2);
            
            float weight = spatialWeight * rangeWeight;
            
            result += sampleValue * weight;
            totalWeight += weight;
        }
    }

    result /= totalWeight;

    outputTexture[dispatchThreadID.xy] = result;
}