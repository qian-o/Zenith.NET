//============================================================================================================
//
//
//                  Copyright (c) 2024, Qualcomm Innovation Center, Inc. All rights reserved.
//                              SPDX-License-Identifier: BSD-3-Clause
//
//============================================================================================================

struct Constants
{
    uint4 RenderDisplaySize;

    float4 ViewportDisplayRcp;

    float4 JitterPadding;

    float4x4 ClipToPrevClip;

    float4 ExposureFovNearMinLerp;

    uint4 SameCameraReset;

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<Texture2D<float4>> OpaqueInput;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float4>> MotionVectors;

    DescriptorHandle<RWTexture2D<uint>> YCoCg;

    DescriptorHandle<RWTexture2D<float4>> Motion;

    DescriptorHandle<SamplerState> Sampler;

    uint2 Pad;
};

ConstantBuffer<Constants> constants;

float2 DecodeVelocityFromTexture(float2 ev)
{
    const float inv_div = 1.0 / (0.499 * 0.5);
    float2 dv;
    dv.xy = ev.xy * inv_div - 32767.0 / 65535.0 * inv_div;
    return dv;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    float h0 = constants.ExposureFovNearMinLerp.x;
    float2 ViewportSizeInverse = constants.ViewportDisplayRcp.xy;
    uint2 InputPos = dispatchThreadId.xy;

    float2 gatherCoord = float2(dispatchThreadId.xy) * ViewportSizeInverse;
    float2 ViewportUV = gatherCoord + float2(0.5, 0.5) * ViewportSizeInverse;

    int2 InputPosBtmRight = int2(1, 1) + int2(dispatchThreadId.xy);
    float NearestZ = constants.Depth.Load(int3(InputPosBtmRight, 0)).x;
    float4 topleft = constants.Depth.Gather(constants.Sampler, gatherCoord);

    NearestZ = min(topleft.x, NearestZ);
    NearestZ = min(topleft.y, NearestZ);
    NearestZ = min(topleft.z, NearestZ);
    NearestZ = min(topleft.w, NearestZ);

    float2 v11 = float2(ViewportSizeInverse.x, 0.0);
    float2 topRight = constants.Depth.Gather(constants.Sampler, (gatherCoord + v11)).yz;

    NearestZ = min(topRight.x, NearestZ);
    NearestZ = min(topRight.y, NearestZ);

    float2 v13 = float2(0.0, ViewportSizeInverse.y);
    float2 bottomLeft = constants.Depth.Gather(constants.Sampler, (gatherCoord + v13)).xy;

    NearestZ = min(bottomLeft.x, NearestZ);
    NearestZ = min(bottomLeft.y, NearestZ);

    float4 EncodedVelocity = constants.MotionVectors.Load(int3(int2(dispatchThreadId.xy), 0));

    float2 motion;
    if (EncodedVelocity.x > 0.0)
    {
        motion = DecodeVelocityFromTexture(EncodedVelocity.xy);
    }
    else
    {
        float2 ScreenPos = float2(2.0 * ViewportUV.x - 1.0, 1.0 - 2.0 * ViewportUV.y);
        float4 Position = float4(ScreenPos, NearestZ, 1.0);
        float4 PreClip = mul(Position, constants.ClipToPrevClip);
        float2 PreScreen = PreClip.xy / PreClip.w;
        motion = Position.xy - PreScreen;
    }

    float3 Colorrgb = constants.Input.Load(int3(int2(InputPos), 0)).xyz;
    Colorrgb /= max(max(Colorrgb.x, Colorrgb.y), Colorrgb.z) + h0;

    float3 Colorycocg;
    Colorycocg.x = 0.25 * (Colorrgb.x + 2.0 * Colorrgb.y + Colorrgb.z);
    Colorycocg.y = clamp(0.5 * Colorrgb.x + 0.5 - 0.5 * Colorrgb.z, 0.0, 1.0);
    Colorycocg.z = clamp(Colorycocg.x + Colorycocg.y - Colorrgb.x, 0.0, 1.0);
    uint x11 = uint(Colorycocg.x * 2047.5);
    uint y11 = uint(Colorycocg.y * 2047.5);
    uint z10 = uint(Colorycocg.z * 1023.5);

    float3 Colorprergb = constants.OpaqueInput.Load(int3(int2(InputPos), 0)).xyz;
    Colorprergb /= max(max(Colorprergb.x, Colorprergb.y), Colorprergb.z) + h0;
    float3 delta = abs(Colorrgb - Colorprergb);
    float alpha_mask = max(delta.x, max(delta.y, delta.z));
    alpha_mask = (0.35 * 1000.0) * alpha_mask;

    constants.YCoCg[int2(dispatchThreadId.xy)] = ((x11 << 21) | (y11 << 10)) | z10;

    float4 v29 = float4(motion, NearestZ, alpha_mask);
    constants.Motion[int2(dispatchThreadId.xy)] = v29;
}
