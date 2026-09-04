using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe class Sgsr1Pass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline pipeline;

    public Sgsr1Pass(GraphicsContext context)
    {
        using Shader shader = context.CreateShader(ZenithCompiler.CompileFromSource(context.GraphicsApi, Source, "Main"));

        buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = context.CreateSampler(SamplerDesc.PointClamp());
        pipeline = context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public void Record(CommandBuffer commandBuffer, SpatialUpscalerDesc desc, SpatialUpscalerArgs args)
    {
        Constants constants = new(desc, args, sampler.Handle);

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((constants.OutputWidth + 7) / 8, (constants.OutputHeight + 7) / 8, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.All);
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        sampler.Dispose();
        buffer.Dispose();
    }

    private const string Source = """
//============================================================================================================
//
//
//                  Copyright (c) 2025, Qualcomm Innovation Center, Inc. All rights reserved.
//                              SPDX-License-Identifier: BSD-3-Clause
//
//============================================================================================================

struct Constants
{
    float4 ViewportInfo;

    uint4 Dimensions;

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<SamplerState> Sampler;

    uint2 Pad;
};

ConstantBuffer<Constants> constants;

static const int OperationMode = 1;

static const float EdgeThreshold = 8.0 / 255.0;

static const float EdgeSharpness = 2.0;

float FastLanczos2(float x)
{
    float wA = x - 4.0;
    float wB = x * wA - wA;
    wA *= wA;

    return wB * wA;
}

float2 WeightY(float dx, float dy, float c, float3 data)
{
    float std = data.x;
    float2 dir = data.yz;

    float edgeDis = ((dx * dir.y) + (dy * dir.x));
    float x = (((dx * dx) + (dy * dy)) + ((edgeDis * edgeDis) * ((clamp(((c * c) * std), 0.0, 1.0) * 0.7) + -1.0)));

    float w = FastLanczos2(x);

    return float2(w, w * c);
}

float2 EdgeDirection(float4 left, float4 right)
{
    float2 dir;
    float RxLz = (right.x + (-left.z));
    float RwLy = (right.w + (-left.y));
    float2 delta;
    delta.x = (RxLz + RwLy);
    delta.y = (RxLz + (-RwLy));
    float lengthInv = rsqrt((delta.x * delta.x + 3.075740e-05) + (delta.y * delta.y));
    dir.x = (delta.x * lengthInv);
    dir.y = (delta.y * lengthInv);

    return dir;
}

float4 GatherOperation(float2 coord)
{
    return constants.Input.GatherGreen(constants.Sampler, coord);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;

    if (pixel.x >= constants.Dimensions.z || pixel.y >= constants.Dimensions.w)
    {
        return;
    }

    float2 texCoord = (float2(pixel) + 0.5) / float2(constants.Dimensions.z, constants.Dimensions.w);

    float4 color;
    color.xyz = constants.Input.SampleLevel(constants.Sampler, texCoord, 0.0).xyz;

    float2 imgCoord = ((texCoord * constants.ViewportInfo.zw) + float2(-0.5, 0.5));
    float2 imgCoordPixel = floor(imgCoord);
    float2 coord = (imgCoordPixel * constants.ViewportInfo.xy);
    float2 pl = (imgCoord + (-imgCoordPixel));
    float4 left = GatherOperation(coord);

    float edgeVote = abs(left.z - left.y) + abs(color[OperationMode] - left.y) + abs(color[OperationMode] - left.z);
    if (edgeVote > EdgeThreshold)
    {
        coord.x += constants.ViewportInfo.x;

        float4 right = GatherOperation(coord + float2(constants.ViewportInfo.x, 0.0));
        float4 upDown;
        upDown.xy = GatherOperation(coord + float2(0.0, -constants.ViewportInfo.y)).wz;
        upDown.zw = GatherOperation(coord + float2(0.0, constants.ViewportInfo.y)).yx;

        float mean = (left.y + left.z + right.x + right.w) * 0.25;
        left = left - float4(mean, mean, mean, mean);
        right = right - float4(mean, mean, mean, mean);
        upDown = upDown - float4(mean, mean, mean, mean);
        color.w = color[OperationMode] - mean;

        float sum = (((((abs(left.x) + abs(left.y)) + abs(left.z)) + abs(left.w)) + (((abs(right.x) + abs(right.y)) + abs(right.z)) + abs(right.w))) + (((abs(upDown.x) + abs(upDown.y)) + abs(upDown.z)) + abs(upDown.w)));
        float sumMean = 1.014185e+01 / sum;
        float std = (sumMean * sumMean);

        float3 data = float3(std, EdgeDirection(left, right));
        float2 aWY = WeightY(pl.x, pl.y + 1.0, upDown.x, data);
        aWY += WeightY(pl.x - 1.0, pl.y + 1.0, upDown.y, data);
        aWY += WeightY(pl.x - 1.0, pl.y - 2.0, upDown.z, data);
        aWY += WeightY(pl.x, pl.y - 2.0, upDown.w, data);
        aWY += WeightY(pl.x + 1.0, pl.y - 1.0, left.x, data);
        aWY += WeightY(pl.x, pl.y - 1.0, left.y, data);
        aWY += WeightY(pl.x, pl.y, left.z, data);
        aWY += WeightY(pl.x + 1.0, pl.y, left.w, data);
        aWY += WeightY(pl.x - 1.0, pl.y - 1.0, right.x, data);
        aWY += WeightY(pl.x - 2.0, pl.y - 1.0, right.y, data);
        aWY += WeightY(pl.x - 2.0, pl.y, right.z, data);
        aWY += WeightY(pl.x - 1.0, pl.y, right.w, data);

        float finalY = aWY.y / aWY.x;
        float maxY = max(max(left.y, left.z), max(right.x, right.w));
        float minY = min(min(left.y, left.z), min(right.x, right.w));
        float deltaY = clamp(EdgeSharpness * finalY, minY, maxY) - color.w;
        deltaY = clamp(deltaY, -23.0 / 255.0, 23.0 / 255.0);

        color.x = clamp((color.x + deltaY), 0.0, 1.0);
        color.y = clamp((color.y + deltaY), 0.0, 1.0);
        color.z = clamp((color.z + deltaY), 0.0, 1.0);
    }

    color.w = 1.0;
    constants.Output[pixel] = color;
}
""";
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct Constants
{
    [FieldOffset(0)]
    public float ViewportInfoX;

    [FieldOffset(4)]
    public float ViewportInfoY;

    [FieldOffset(8)]
    public float ViewportInfoZ;

    [FieldOffset(12)]
    public float ViewportInfoW;

    [FieldOffset(16)]
    public uint InputWidth;

    [FieldOffset(20)]
    public uint InputHeight;

    [FieldOffset(24)]
    public uint OutputWidth;

    [FieldOffset(28)]
    public uint OutputHeight;

    [FieldOffset(32)]
    public ResourceHandle Input;

    [FieldOffset(40)]
    public ResourceHandle Output;

    [FieldOffset(48)]
    public ResourceHandle Sampler;

    public Constants(SpatialUpscalerDesc desc, SpatialUpscalerArgs args, ResourceHandle sampler)
    {
        ViewportInfoX = 1.0f / args.InputWidth;
        ViewportInfoY = 1.0f / args.InputHeight;
        ViewportInfoZ = args.InputWidth;
        ViewportInfoW = args.InputHeight;
        InputWidth = args.InputWidth;
        InputHeight = args.InputHeight;
        OutputWidth = desc.OutputWidth;
        OutputHeight = desc.OutputHeight;
        Input = args.Input;
        Output = args.Output;
        Sampler = sampler;
    }
}
