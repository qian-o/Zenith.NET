struct OutputConstants
{
    uint Width;

    uint FrameIndex;

    uint2 Padding;

    DescriptorHandle<Texture2D<float4>> Input;
};

ConstantBuffer<OutputConstants> output;

uint Hash(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

float3 AcesFilm(float3 x)
{
    float3 numerator = x * (x * 2.51 + 0.03);
    float3 denominator = x * (x * 2.43 + 0.59) + 0.14;

    return saturate(numerator / denominator);
}

float3 LinearToSrgb(float3 linearColor)
{
    float3 clamped = max(linearColor, 0.0);

    return select(clamped <= 0.0031308, clamped * 12.92, 1.055 * pow(clamped, 1.0 / 2.4) - 0.055);
}

[shader("vertex")]
float4 FullscreenVS(uint vertexId: SV_VertexID) : SV_POSITION
{
    float2 uv = float2((vertexId << 1u) & 2u, vertexId & 2u);

    return float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
}

[shader("fragment")]
float4 ToneMappingFS(float4 position: SV_POSITION) : SV_TARGET
{
    uint2 pixel = uint2(position.xy);
    float3 hdr = output.Input.Load(int3(pixel, 0)).rgb;
    float3 srgbColor = LinearToSrgb(AcesFilm(max(hdr, 0.0)));

    uint seed = Hash(pixel.x + pixel.y * max(output.Width, 1u) + output.FrameIndex * 1664525u);
    float ditherA = float(seed >> 8u) * (1.0 / 16777216.0);
    float ditherB = float(Hash(seed) >> 8u) * (1.0 / 16777216.0);
    srgbColor += (ditherA - ditherB) * (1.0 / 255.0);

    return float4(saturate(srgbColor), 1.0);
}
