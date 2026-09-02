using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe class RcasPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly ComputePipeline pipeline;

    public RcasPass(GraphicsContext context)
    {
        using Shader shader = context.CreateShader(ZenithCompiler.CompileFromSource(context.GraphicsApi, Source, "Main"));

        buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        pipeline = context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public void Record(CommandBuffer commandBuffer, UpscalerDesc desc, ResourceHandle input, ResourceHandle output)
    {
        Constants constants = new(desc, input, output);

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
        buffer.Dispose();
    }

    private const string Source = """
struct Constants
{
    uint4 Con;

    uint4 Dimensions;

    DescriptorHandle<RWTexture2D<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;
};

ConstantBuffer<Constants> constants;

float ApproximateReciprocalMedium(float value)
{
    float estimate = asfloat(0x7ef19fff - asuint(value));

    return estimate * (-estimate * value + 2.0);
}

float3 Rcas(float3 b, float3 d, float3 e, float3 f, float3 h)
{
    float bL = b.b * 0.5 + (b.r * 0.5 + b.g);

    float dL = d.b * 0.5 + (d.r * 0.5 + d.g);

    float eL = e.b * 0.5 + (e.r * 0.5 + e.g);

    float fL = f.b * 0.5 + (f.r * 0.5 + f.g);

    float hL = h.b * 0.5 + (h.r * 0.5 + h.g);

    float noise = 0.25 * bL + 0.25 * dL + 0.25 * fL + 0.25 * hL - eL;

    float maximumLuma = max(max(bL, dL), max(max(eL, fL), hL));

    float minimumLuma = min(min(bL, dL), min(min(eL, fL), hL));

    noise = saturate(abs(noise) * ApproximateReciprocalMedium(maximumLuma - minimumLuma));
    noise = -0.5 * noise + 1.0;

    float3 minimum = min(min(b, d), min(f, h));

    float3 maximum = max(max(b, d), max(f, h));

    float2 peak = float2(1.0, -4.0);

    float lowerLimiterMultiplier = saturate(eL / min(min(bL, dL), min(fL, hL)));

    float hitMinR = minimum.r * rcp(4.0 * maximum.r) * lowerLimiterMultiplier;

    float hitMinG = minimum.g * rcp(4.0 * maximum.g) * lowerLimiterMultiplier;

    float hitMinB = minimum.b * rcp(4.0 * maximum.b) * lowerLimiterMultiplier;

    float hitMaxR = (peak.x - maximum.r) * rcp(4.0 * minimum.r + peak.y);

    float hitMaxG = (peak.x - maximum.g) * rcp(4.0 * minimum.g + peak.y);

    float hitMaxB = (peak.x - maximum.b) * rcp(4.0 * minimum.b + peak.y);

    float lobeR = max(-hitMinR, hitMaxR);

    float lobeG = max(-hitMinG, hitMaxG);

    float lobeB = max(-hitMinB, hitMaxB);

    float lobe = max(-(0.25 - 1.0 / 16.0), min(max(lobeR, max(lobeG, lobeB)), 0.0)) * asfloat(constants.Con.x);

    lobe *= noise;

    float reciprocalLobe = ApproximateReciprocalMedium(4.0 * lobe + 1.0);

    return (lobe * b + lobe * d + lobe * h + lobe * f + e) * reciprocalLobe;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;

    if (pixel.x >= constants.Dimensions.x || pixel.y >= constants.Dimensions.y)
    {
        return;
    }

    int2 position = int2(pixel);

    float3 b = constants.Input[position + int2(0, -1)].rgb;

    float3 d = constants.Input[position + int2(-1, 0)].rgb;

    float3 e = constants.Input[position].rgb;

    float3 f = constants.Input[position + int2(1, 0)].rgb;

    float3 h = constants.Input[position + int2(0, 1)].rgb;

    constants.Output[pixel] = float4(Rcas(b, d, e, f, h), 1.0);
}
""";
}

[StructLayout(LayoutKind.Explicit)]
file struct Constants
{
    [FieldOffset(0)]
    public uint Sharpness;

    [FieldOffset(4)]
    public uint SharpnessHalf;

    [FieldOffset(8)]
    public uint ConZ;

    [FieldOffset(12)]
    public uint ConW;

    [FieldOffset(16)]
    public uint OutputWidth;

    [FieldOffset(20)]
    public uint OutputHeight;

    [FieldOffset(24)]
    public uint DimensionsZ;

    [FieldOffset(28)]
    public uint DimensionsW;

    [FieldOffset(32)]
    public ResourceHandle Input;

    [FieldOffset(40)]
    public ResourceHandle Output;

    public Constants(UpscalerDesc desc, ResourceHandle input, ResourceHandle output)
    {
        float sharpness = MathF.Pow(2.0f, -0.2f);
        ushort sharpnessHalf = BitConverter.HalfToUInt16Bits((Half)sharpness);

        Sharpness = BitConverter.SingleToUInt32Bits(sharpness);
        SharpnessHalf = sharpnessHalf | ((uint)sharpnessHalf << 16);
        ConZ = 0;
        ConW = 0;
        OutputWidth = desc.OutputWidth;
        OutputHeight = desc.OutputHeight;
        DimensionsZ = 0;
        DimensionsW = 0;
        Input = input;
        Output = output;
    }
}
