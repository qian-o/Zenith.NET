struct BilinearConstants
{
    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<SamplerState> Sampler;

    uint2 Size;
};

ConstantBuffer<BilinearConstants> parameters;

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 thread : SV_DispatchThreadID)
{
    if (any(thread.xy >= parameters.Size))
    {
        return;
    }

    float2 uv = (float2(thread.xy) + 0.5) / float2(parameters.Size);
    parameters.Output[thread.xy] = parameters.Input.SampleLevel(parameters.Sampler, uv, 0.0);
}
