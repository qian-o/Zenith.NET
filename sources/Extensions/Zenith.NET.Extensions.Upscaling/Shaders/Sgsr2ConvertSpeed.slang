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
    float Exposure_co_rcp = constants.ExposureFovNearMinLerp.x;
    float2 ViewportSizeInverse = constants.ViewportDisplayRcp.xy;
    uint2 InputPos = dispatchThreadId.xy;

    float2 gatherCoord = float2(dispatchThreadId.xy) * ViewportSizeInverse;
    float2 ViewportUV = gatherCoord + float2(0.5, 0.5) * ViewportSizeInverse;

    float4 topleft = constants.Depth.Gather(constants.Sampler, gatherCoord);
    float2 v10 = float2(ViewportSizeInverse.x * 2.0, 0.0);
    float4 topRight = constants.Depth.Gather(constants.Sampler, (gatherCoord + v10));
    float2 v12 = float2(0.0, ViewportSizeInverse.y * 2.0);
    float4 bottomLeft = constants.Depth.Gather(constants.Sampler, (gatherCoord + v12));
    float2 v14 = float2(ViewportSizeInverse.x * 2.0, ViewportSizeInverse.y * 2.0);
    float4 bottomRight = constants.Depth.Gather(constants.Sampler, (gatherCoord + v14));
    float maxC = min(min(min(topleft.y, topRight.x), bottomLeft.z), bottomRight.w);
    float topleft4 = min(min(min(topleft.y, topleft.x), topleft.z), topleft.w);
    float topLeftMax9 = min(bottomLeft.w, min(min(maxC, topleft4), topRight.w));

    float depthclip = 0.0;
    if (maxC < 1.0 - 1.0e-05)
    {
        float topRight4 = min(min(min(topRight.y, topRight.x), topRight.z), topRight.w);
        float bottomLeft4 = min(min(min(bottomLeft.y, bottomLeft.x), bottomLeft.z), bottomLeft.w);
        float bottomRight4 = min(min(min(bottomRight.y, bottomRight.x), bottomRight.z), bottomRight.w);

        float Wdepth = 0.0;
        float Ksep = 1.37e-05;
        float Kfov = constants.ExposureFovNearMinLerp.y;
        float diagonal_length = length(float2(constants.RenderDisplaySize.xy));
        float Ksep_Kfov_diagonal = Ksep * Kfov * diagonal_length;

        float Depthsep = Ksep_Kfov_diagonal * (1.0 - maxC);
        float EPSILON = 1.19e-07;
        Wdepth += clamp((Depthsep / (abs(maxC - topleft4) + EPSILON)), 0.0, 1.0);
        Wdepth += clamp((Depthsep / (abs(maxC - topRight4) + EPSILON)), 0.0, 1.0);
        Wdepth += clamp((Depthsep / (abs(maxC - bottomLeft4) + EPSILON)), 0.0, 1.0);
        Wdepth += clamp((Depthsep / (abs(maxC - bottomRight4) + EPSILON)), 0.0, 1.0);
        depthclip = clamp(1.0 - Wdepth * 0.25, 0.0, 1.0);
    }

    float4 EncodedVelocity = constants.MotionVectors.Load(int3(int2(dispatchThreadId.xy), 0));

    float2 motion;
    if (EncodedVelocity.x > 0.0)
    {
        motion = DecodeVelocityFromTexture(EncodedVelocity.xy);
    }
    else
    {
        float2 ScreenPos = float2(2.0 * ViewportUV.x - 1.0, 1.0 - 2.0 * ViewportUV.y);
        float4 Position = float4(ScreenPos, topLeftMax9, 1.0);
        float4 PreClip = mul(Position, constants.ClipToPrevClip);
        float2 PreScreen = PreClip.xy / PreClip.w;
        motion = Position.xy - PreScreen;
    }

    float3 Colorrgb = constants.Input.Load(int3(int2(InputPos), 0)).xyz;
    float ColorMax = max(max(Colorrgb.x, Colorrgb.y), Colorrgb.z) + Exposure_co_rcp;
    Colorrgb /= float3(ColorMax, ColorMax, ColorMax);

    float3 Colorycocg;
    Colorycocg.x = 0.25 * (Colorrgb.x + 2.0 * Colorrgb.y + Colorrgb.z);
    Colorycocg.y = clamp(0.5 * Colorrgb.x + 0.5 - 0.5 * Colorrgb.z, 0.0, 1.0);
    Colorycocg.z = clamp(Colorycocg.x + Colorycocg.y - Colorrgb.x, 0.0, 1.0);
    uint x11 = uint(Colorycocg.x * 2047.5);
    uint y11 = uint(Colorycocg.y * 2047.5);
    uint z10 = uint(Colorycocg.z * 1023.5);

    constants.YCoCg[int2(dispatchThreadId.xy)] = ((x11 << 21) | (y11 << 10)) | z10;

    float4 v29 = float4(motion, depthclip, ColorMax);
    constants.Motion[int2(dispatchThreadId.xy)] = v29;
}
