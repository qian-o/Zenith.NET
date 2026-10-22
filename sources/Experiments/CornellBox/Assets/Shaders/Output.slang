struct OutputConstants
{
    uint4 FrameIndexPadding;

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<Texture2D<float4>> Overlay;
};

ConstantBuffer<OutputConstants> output;

uint Hash(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

[shader("vertex")]
float4 FullscreenVS(uint vertexId: SV_VertexID) : SV_POSITION
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);

    return float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
}

[shader("fragment")]
float4 ToneMappingFS(float4 position: SV_POSITION) : SV_TARGET
{
    uint2 pixel = uint2(position.xy);
    uint width;
    uint height;
    output.Input.GetDimensions(width, height);

    float3 color = max(output.Input.Load(int3(pixel, 0)).rgb, 0.0);
    float3 numerator = color * (color * 2.51 + 0.03);
    float3 denominator = color * (color * 2.43 + 0.59) + 0.14;
    color = pow(saturate(numerator / denominator), 1.0 / 2.2);

    uint seed = Hash(pixel.x + pixel.y * width + output.FrameIndexPadding.x * 1664525u);
    float first = float(seed >> 8u) * (1.0 / 16777216.0);
    float second = float(Hash(seed) >> 8u) * (1.0 / 16777216.0);
    color += (first - second) / 255.0;

    return float4(saturate(color), 1.0);
}

[shader("fragment")]
float4 CompositeFS(float4 position: SV_POSITION) : SV_TARGET
{
    uint2 pixel = uint2(position.xy);
    float3 color = output.Input.Load(int3(pixel, 0)).rgb;
    float4 overlay = output.Overlay.Load(int3(pixel, 0));

    return float4(overlay.rgb + color * (1.0 - overlay.a), 1.0);
}
