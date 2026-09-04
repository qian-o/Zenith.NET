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

    DescriptorHandle<Texture2D<uint>> PrevLumaHistory;

    DescriptorHandle<Texture2D<float4>> MotionDepthAlpha;

    DescriptorHandle<Texture2D<uint>> YCoCg;

    DescriptorHandle<RWTexture2D<float4>> MotionDepthClipAlpha;

    DescriptorHandle<RWTexture2D<uint>> LumaHistory;

    DescriptorHandle<SamplerState> Sampler;
};

ConstantBuffer<Constants> constants;

static const float EPSILON = 1.19e-07;

float DecodeColorY(uint sample32)
{
    uint x11 = sample32 >> 21;
    return float(x11) * (1.0 / 2047.5);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int2 sampleOffset[4];
    sampleOffset[0] = int2(-1, -1);
    sampleOffset[1] = int2(-1, 0);
    sampleOffset[2] = int2(0, -1);
    sampleOffset[3] = int2(0, 0);

    float2 ViewportUV = (float2(dispatchThreadId.xy) + float2(0.5, 0.5)) * constants.ViewportDisplayRcp.xy;
    float2 gatherCoord = ViewportUV + 0.5 * constants.ViewportDisplayRcp.xy;
    uint luma_reference32 = constants.YCoCg.Gather(constants.Sampler, gatherCoord).w;
    float luma_reference = DecodeColorY(luma_reference32);

    float4 mda = constants.MotionDepthAlpha.Load(int3(int2(dispatchThreadId.xy), 0)).xyzw;
    float depth = mda.z;
    float alphamask = mda.w;
    float2 motion = mda.xy;

    float2 PrevUV = float2(-0.5 * motion.x + ViewportUV.x, 0.5 * motion.y + ViewportUV.y);
    float depthclip = 0.0;

    if (depth < 1.0 - 1.0e-05)
    {
        float2 Prevf_sample = PrevUV * float2(constants.RenderDisplaySize.xy) - 0.5;
        float2 Prevfrac = Prevf_sample - floor(Prevf_sample);
        float OneMinusPrevfacx = 1.0 - Prevfrac.x;

        float Bilinweights[4];
        Bilinweights[0] = OneMinusPrevfacx - OneMinusPrevfacx * Prevfrac.y;
        Bilinweights[1] = Prevfrac.x - Prevfrac.x * Prevfrac.y;
        Bilinweights[2] = OneMinusPrevfacx * Prevfrac.y;
        Bilinweights[3] = Prevfrac.x * Prevfrac.y;

        float diagonal_length = length(float2(constants.RenderDisplaySize.xy));
        float Wdepth = 0.0;
        float Ksep = 1.37e-05;
        float Kfov = constants.ExposureFovNearMinLerp.y;
        float Ksep_Kfov_diagonal = Ksep * Kfov * diagonal_length;
        for (int index = 0; index < 4; index += 2)
        {
            float4 gPrevdepth = constants.MotionDepthAlpha.GatherBlue(constants.Sampler, PrevUV, sampleOffset[index]);
            float tdepth1 = min(gPrevdepth.x, gPrevdepth.y);
            float tdepth2 = min(gPrevdepth.z, gPrevdepth.w);
            float fPrevdepth = min(tdepth1, tdepth2);

            float Depthsep = Ksep_Kfov_diagonal * (1.0 - min(fPrevdepth, depth));
            float weight = Bilinweights[index];
            Wdepth += clamp(Depthsep / (abs(fPrevdepth - depth) + EPSILON), 0.0, 1.0) * weight;

            float2 gPrevdepth2 = constants.MotionDepthAlpha.GatherBlue(constants.Sampler, PrevUV, sampleOffset[index + int(1)]).zw;
            fPrevdepth = min(min(gPrevdepth2.x, gPrevdepth2.y), tdepth2);
            Depthsep = Ksep_Kfov_diagonal * (1.0 - min(fPrevdepth, depth));
            weight = Bilinweights[index + int(1)];
            Wdepth += clamp(Depthsep / (abs(fPrevdepth - depth) + EPSILON), 0.0, 1.0) * weight;
        }
        depthclip = clamp(1.0 - Wdepth, 0.0, 1.0);
    }

    float2 current_luma_diff;
    uint prev_luma_diff_pack = constants.PrevLumaHistory.Gather(constants.Sampler, PrevUV).w;
    float2 prev_luma_diff;
    prev_luma_diff.x = f16tof32(prev_luma_diff_pack >> 16);
    prev_luma_diff.y = f16tof32(prev_luma_diff_pack & 0xFFFF);

    bool enable = false;
    if (depthclip + float(constants.SameCameraReset.y) < 0.1)
    {
        enable = (all(PrevUV >= float2(0.0, 0.0)) && all(PrevUV <= float2(1.0, 1.0)));
    }
    float luma_diff = luma_reference - prev_luma_diff.x;
    if (!enable)
    {
        current_luma_diff.x = 0.0;
        current_luma_diff.y = 0.0;
    }
    else
    {
        current_luma_diff.x = luma_reference;
        current_luma_diff.y = (prev_luma_diff.y != 0.0) ? ((sign(luma_diff) == sign(prev_luma_diff.y)) ? (sign(luma_diff) * min(abs(prev_luma_diff.y), abs(luma_diff))) : prev_luma_diff.y) : luma_diff;
    }

    alphamask = floor(alphamask) + 0.5 * float((current_luma_diff.x != 0.0) && (abs(current_luma_diff.y) != abs(luma_diff)));
    uint pack = (f32tof16(current_luma_diff.x) << 16) | f32tof16(current_luma_diff.y);
    constants.LumaHistory[int2(dispatchThreadId.xy)] = pack;
    constants.MotionDepthClipAlpha[int2(dispatchThreadId.xy)] = float4(motion, depthclip, alphamask);
}
