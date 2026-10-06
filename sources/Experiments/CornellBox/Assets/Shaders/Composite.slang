struct Constants
{
    uint4 OutputSize;

    DescriptorHandle<Texture2D<float4>> Hudless;

    DescriptorHandle<Texture2D<float4>> UI;

    DescriptorHandle<RWTexture2D<float4>> Output;

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
    float3 hudless = constants.Hudless.Load(int3(pixel, 0)).rgb;

    // The UI is premultiplied, which matches drawing it over the scene.
    constants.Output[pixel] = float4(ui.rgb + (1.0 - ui.a) * hudless, 1.0);
}
