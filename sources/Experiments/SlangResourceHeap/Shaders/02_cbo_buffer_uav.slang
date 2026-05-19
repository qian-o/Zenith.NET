struct BufferConstants
{
    DescriptorHandle<StructuredBuffer<float4>> Source;
    DescriptorHandle<RWStructuredBuffer<float4>> Destination;
    uint Count;
    float4 Scale;
};

[[vk::binding(0, 0)]]
ConstantBuffer<BufferConstants> gBuffers;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint index = dispatchThreadId.x;
    if (index >= gBuffers.Count)
    {
        return;
    }

    (*gBuffers.Destination)[index] = (*gBuffers.Source)[index] * gBuffers.Scale;
}