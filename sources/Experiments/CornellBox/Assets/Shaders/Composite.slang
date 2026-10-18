struct CompositeConstants
{
    uint4 OutputSize;

    DescriptorHandle<Texture2D<float4>> Color;

    DescriptorHandle<Texture2D<float4>> UI;

    DescriptorHandle<RWTexture2D<float4>> Output;

    uint2 Padding;
};

ConstantBuffer<CompositeConstants> composite;

[shader("compute")]
[numthreads(8, 8, 1)]
void CompositeCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;

    if (any(pixel >= composite.OutputSize.xy))
    {
        return;
    }

    float4 ui = composite.UI.Load(int3(pixel, 0));
    float3 color = composite.Color.Load(int3(pixel, 0)).rgb;
    composite.Output[pixel] = float4(ui.rgb + (1.0 - ui.a) * color, 1.0);
}
