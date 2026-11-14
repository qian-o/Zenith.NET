struct CaptureConstants
{
    uint Width;

    uint Height;

    uint2 Padding0;

    DescriptorHandle<Texture2D<float4>> SrcColor;

    DescriptorHandle<Texture2D<float>> SrcDepth;

    DescriptorHandle<Texture2D<float2>> SrcMotionVectors;

    DescriptorHandle<RWTexture2D<float>> DstDepth;

    DescriptorHandle<RWTexture2D<float2>> DstMotionVectors;

    uint2 Padding1;
};

ConstantBuffer<CaptureConstants> capture;

[shader("compute")]
[numthreads(8, 8, 1)]
void CaptureInputs(uint3 id: SV_DispatchThreadID)
{
    if (id.x >= capture.Width || id.y >= capture.Height)
    {
        return;
    }

    int3 location = int3(int2(id.xy), 0);
    capture.DstDepth[id.xy] = capture.SrcDepth.Load(location);
    capture.DstMotionVectors[id.xy] = capture.SrcMotionVectors.Load(location);
}

[shader("vertex")]
float4 FullscreenVS(uint vertexId: SV_VertexID) : SV_POSITION
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);

    return float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
}

[shader("fragment")]
float4 CaptureColorFS(float4 position: SV_POSITION) : SV_TARGET
{
    return capture.SrcColor.Load(int3(int2(position.xy), 0));
}
