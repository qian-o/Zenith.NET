struct Constants
{
    uint4 OutputSize;

    DescriptorHandle<Texture2D<float4>> Color;

    DescriptorHandle<Texture2D<float4>> GeneratedColor;

    DescriptorHandle<Texture2D<float4>> UI;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<RWTexture2D<float4>> GeneratedOutput;

    uint2 Padding;
};

ConstantBuffer<Constants> constants;

[shader("compute")]
[numthreads(8, 8, 1)]
void CSMain(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;

    if (any(pixel >= constants.OutputSize.xy))
    {
        return;
    }

    float4 ui = constants.UI.Load(int3(pixel, 0));
    if (dispatchThreadId.z == 0)
    {
        float3 color = constants.Color.Load(int3(pixel, 0)).rgb;
        constants.Output[pixel] = float4(ui.rgb + (1.0 - ui.a) * color, 1.0);
    }
    else
    {
        float3 color = constants.GeneratedColor.Load(int3(pixel, 0)).rgb;
        constants.GeneratedOutput[pixel] = float4(ui.rgb + (1.0 - ui.a) * color, 1.0);
    }
}
