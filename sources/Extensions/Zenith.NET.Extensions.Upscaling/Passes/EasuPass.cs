using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe class EasuPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline pipeline;

    public EasuPass(GraphicsContext context)
    {
        using Shader shader = context.CreateShader(ZenithCompiler.CompileFromSource(context.GraphicsApi, Source, "Main"));

        buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = context.CreateSampler(SamplerDesc.LinearClamp());
        pipeline = context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public void Record(CommandBuffer commandBuffer, UpscalerDesc desc, ResourceHandle input, ResourceHandle output)
    {
        Constants constants = new(desc, input, output, sampler.Handle);

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((constants.OutputWidth + 7) / 8, (constants.OutputHeight + 7) / 8, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        sampler.Dispose();
        buffer.Dispose();
    }

    private const string Source = """
struct Constants
{
    uint4 Con0;

    uint4 Con1;

    uint4 Con2;

    uint4 Con3;

    uint4 Dimensions;

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<SamplerState> Sampler;
};

ConstantBuffer<Constants> constants;

float ApproximateReciprocal(float value)
{
    return asfloat(0x7ef07ebb - asuint(value));
}

float ApproximateReciprocalSquareRoot(float value)
{
    return asfloat(0x5f347d74 - (asuint(value) >> 1));
}

void EasuTap(inout float3 accumulatedColor, inout float accumulatedWeight, float2 pixelOffset, float2 gradientDirection, float2 length, float negativeLobeStrength, float clippingPoint, float3 color)
{
    float2 rotatedOffset;

    rotatedOffset.x = pixelOffset.x * gradientDirection.x + pixelOffset.y * gradientDirection.y;
    rotatedOffset.y = pixelOffset.x * -gradientDirection.y + pixelOffset.y * gradientDirection.x;
    rotatedOffset *= length;

    float distanceSquared = rotatedOffset.x * rotatedOffset.x + rotatedOffset.y * rotatedOffset.y;
    distanceSquared = min(distanceSquared, clippingPoint);

    float weightB = 2.0 / 5.0 * distanceSquared - 1.0;

    float weightA = negativeLobeStrength * distanceSquared - 1.0;
    weightB *= weightB;
    weightA *= weightA;
    weightB = 25.0 / 16.0 * weightB - (25.0 / 16.0 - 1.0);
    float weight = weightB * weightA;

    accumulatedColor += color * weight;
    accumulatedWeight += weight;
}

void EasuSet(inout float2 direction, inout float length, float2 pp, bool biS, bool biT, bool biU, bool biV, float lA, float lB, float lC, float lD, float lE)
{
    float weight = 0.0;

    if (biS)
    {
        weight = (1.0 - pp.x) * (1.0 - pp.y);
    }

    if (biT)
    {
        weight = pp.x * (1.0 - pp.y);
    }

    if (biU)
    {
        weight = (1.0 - pp.x) * pp.y;
    }

    if (biV)
    {
        weight = pp.x * pp.y;
    }

    float dc = lD - lC;

    float cb = lC - lB;

    float lengthX = max(abs(dc), abs(cb));

    lengthX = ApproximateReciprocal(lengthX);

    float directionX = lD - lB;

    direction.x += directionX * weight;
    lengthX = saturate(abs(directionX) * lengthX);
    lengthX *= lengthX;
    length += lengthX * weight;

    float ec = lE - lC;

    float ca = lC - lA;

    float lengthY = max(abs(ec), abs(ca));

    lengthY = ApproximateReciprocal(lengthY);

    float directionY = lE - lA;

    direction.y += directionY * weight;
    lengthY = saturate(abs(directionY) * lengthY);
    lengthY *= lengthY;
    length += lengthY * weight;
}

float3 Easu(uint2 ip)
{
    float2 pp = float2(ip) * asfloat(constants.Con0.xy) + asfloat(constants.Con0.zw);

    float2 fp = floor(pp);

    pp -= fp;

    float2 p0 = fp * asfloat(constants.Con1.xy) + asfloat(constants.Con1.zw);

    float2 p1 = p0 + asfloat(constants.Con2.xy);

    float2 p2 = p0 + asfloat(constants.Con2.zw);

    float2 p3 = p0 + asfloat(constants.Con3.xy);

    float4 bczzR = constants.Input.GatherRed(constants.Sampler, p0, int2(0, 0));

    float4 bczzG = constants.Input.GatherGreen(constants.Sampler, p0, int2(0, 0));

    float4 bczzB = constants.Input.GatherBlue(constants.Sampler, p0, int2(0, 0));

    float4 ijfeR = constants.Input.GatherRed(constants.Sampler, p1, int2(0, 0));

    float4 ijfeG = constants.Input.GatherGreen(constants.Sampler, p1, int2(0, 0));

    float4 ijfeB = constants.Input.GatherBlue(constants.Sampler, p1, int2(0, 0));

    float4 klhgR = constants.Input.GatherRed(constants.Sampler, p2, int2(0, 0));

    float4 klhgG = constants.Input.GatherGreen(constants.Sampler, p2, int2(0, 0));

    float4 klhgB = constants.Input.GatherBlue(constants.Sampler, p2, int2(0, 0));

    float4 zzonR = constants.Input.GatherRed(constants.Sampler, p3, int2(0, 0));

    float4 zzonG = constants.Input.GatherGreen(constants.Sampler, p3, int2(0, 0));

    float4 zzonB = constants.Input.GatherBlue(constants.Sampler, p3, int2(0, 0));

    float4 bczzL = bczzB * 0.5 + (bczzR * 0.5 + bczzG);

    float4 ijfeL = ijfeB * 0.5 + (ijfeR * 0.5 + ijfeG);

    float4 klhgL = klhgB * 0.5 + (klhgR * 0.5 + klhgG);

    float4 zzonL = zzonB * 0.5 + (zzonR * 0.5 + zzonG);

    float bL = bczzL.x;

    float cL = bczzL.y;

    float iL = ijfeL.x;

    float jL = ijfeL.y;

    float fL = ijfeL.z;

    float eL = ijfeL.w;

    float kL = klhgL.x;

    float lL = klhgL.y;

    float hL = klhgL.z;

    float gL = klhgL.w;

    float oL = zzonL.z;

    float nL = zzonL.w;

    float2 direction = 0.0;

    float length = 0.0;
    EasuSet(direction, length, pp, true, false, false, false, bL, eL, fL, gL, jL);
    EasuSet(direction, length, pp, false, true, false, false, cL, fL, gL, hL, kL);
    EasuSet(direction, length, pp, false, false, true, false, fL, iL, jL, kL, nL);
    EasuSet(direction, length, pp, false, false, false, true, gL, jL, kL, lL, oL);

    float2 directionSquared = direction * direction;

    float directionReciprocal = directionSquared.x + directionSquared.y;

    bool zeroDirection = directionReciprocal < 1.0 / 32768.0;
    directionReciprocal = ApproximateReciprocalSquareRoot(directionReciprocal);
    directionReciprocal = zeroDirection ? 1.0 : directionReciprocal;
    direction.x = zeroDirection ? 1.0 : direction.x;
    direction *= directionReciprocal;

    length *= 0.5;
    length *= length;

    float stretch = (direction.x * direction.x + direction.y * direction.y) * ApproximateReciprocal(max(abs(direction.x), abs(direction.y)));

    float2 length2 = float2(1.0 + (stretch - 1.0) * length, 1.0 - 0.5 * length);

    float lobe = 0.5 + (0.25 - 0.04 - 0.5) * length;

    float clippingPoint = ApproximateReciprocal(lobe);

    float3 min4 = min(min(float3(ijfeR.z, ijfeG.z, ijfeB.z), float3(klhgR.w, klhgG.w, klhgB.w)), min(float3(ijfeR.y, ijfeG.y, ijfeB.y), float3(klhgR.x, klhgG.x, klhgB.x)));

    float3 max4 = max(max(float3(ijfeR.z, ijfeG.z, ijfeB.z), float3(klhgR.w, klhgG.w, klhgB.w)), max(float3(ijfeR.y, ijfeG.y, ijfeB.y), float3(klhgR.x, klhgG.x, klhgB.x)));

    float3 accumulatedColor = 0.0;

    float accumulatedWeight = 0.0;
    EasuTap(accumulatedColor, accumulatedWeight, float2(0.0, -1.0) - pp, direction, length2, lobe, clippingPoint, float3(bczzR.x, bczzG.x, bczzB.x));
    EasuTap(accumulatedColor, accumulatedWeight, float2(1.0, -1.0) - pp, direction, length2, lobe, clippingPoint, float3(bczzR.y, bczzG.y, bczzB.y));
    EasuTap(accumulatedColor, accumulatedWeight, float2(-1.0, 1.0) - pp, direction, length2, lobe, clippingPoint, float3(ijfeR.x, ijfeG.x, ijfeB.x));
    EasuTap(accumulatedColor, accumulatedWeight, float2(0.0, 1.0) - pp, direction, length2, lobe, clippingPoint, float3(ijfeR.y, ijfeG.y, ijfeB.y));
    EasuTap(accumulatedColor, accumulatedWeight, float2(0.0, 0.0) - pp, direction, length2, lobe, clippingPoint, float3(ijfeR.z, ijfeG.z, ijfeB.z));
    EasuTap(accumulatedColor, accumulatedWeight, float2(-1.0, 0.0) - pp, direction, length2, lobe, clippingPoint, float3(ijfeR.w, ijfeG.w, ijfeB.w));
    EasuTap(accumulatedColor, accumulatedWeight, float2(1.0, 1.0) - pp, direction, length2, lobe, clippingPoint, float3(klhgR.x, klhgG.x, klhgB.x));
    EasuTap(accumulatedColor, accumulatedWeight, float2(2.0, 1.0) - pp, direction, length2, lobe, clippingPoint, float3(klhgR.y, klhgG.y, klhgB.y));
    EasuTap(accumulatedColor, accumulatedWeight, float2(2.0, 0.0) - pp, direction, length2, lobe, clippingPoint, float3(klhgR.z, klhgG.z, klhgB.z));
    EasuTap(accumulatedColor, accumulatedWeight, float2(1.0, 0.0) - pp, direction, length2, lobe, clippingPoint, float3(klhgR.w, klhgG.w, klhgB.w));
    EasuTap(accumulatedColor, accumulatedWeight, float2(1.0, 2.0) - pp, direction, length2, lobe, clippingPoint, float3(zzonR.z, zzonG.z, zzonB.z));
    EasuTap(accumulatedColor, accumulatedWeight, float2(0.0, 2.0) - pp, direction, length2, lobe, clippingPoint, float3(zzonR.w, zzonG.w, zzonB.w));

    return min(max4, max(min4, accumulatedColor * rcp(accumulatedWeight)));
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

    float3 color = Easu(pixel);
    constants.Output[pixel] = float4(color, 1.0);
}
""";
}

[StructLayout(LayoutKind.Explicit)]
file struct Constants
{
    [FieldOffset(0)]
    public uint Con0X;

    [FieldOffset(4)]
    public uint Con0Y;

    [FieldOffset(8)]
    public uint Con0Z;

    [FieldOffset(12)]
    public uint Con0W;

    [FieldOffset(16)]
    public uint Con1X;

    [FieldOffset(20)]
    public uint Con1Y;

    [FieldOffset(24)]
    public uint Con1Z;

    [FieldOffset(28)]
    public uint Con1W;

    [FieldOffset(32)]
    public uint Con2X;

    [FieldOffset(36)]
    public uint Con2Y;

    [FieldOffset(40)]
    public uint Con2Z;

    [FieldOffset(44)]
    public uint Con2W;

    [FieldOffset(48)]
    public uint Con3X;

    [FieldOffset(52)]
    public uint Con3Y;

    [FieldOffset(56)]
    public uint Con3Z;

    [FieldOffset(60)]
    public uint Con3W;

    [FieldOffset(64)]
    public uint InputWidth;

    [FieldOffset(68)]
    public uint InputHeight;

    [FieldOffset(72)]
    public uint OutputWidth;

    [FieldOffset(76)]
    public uint OutputHeight;

    [FieldOffset(80)]
    public ResourceHandle Input;

    [FieldOffset(88)]
    public ResourceHandle Output;

    [FieldOffset(96)]
    public ResourceHandle Sampler;

    public Constants(UpscalerDesc desc, ResourceHandle input, ResourceHandle output, ResourceHandle sampler)
    {
        float inputWidth = desc.InputWidth;
        float inputHeight = desc.InputHeight;
        float outputWidth = desc.OutputWidth;
        float outputHeight = desc.OutputHeight;
        float inverseOutputWidth = 1.0f / outputWidth;
        float inverseOutputHeight = 1.0f / outputHeight;
        float inverseInputWidth = 1.0f / inputWidth;
        float inverseInputHeight = 1.0f / inputHeight;

        Con0X = FloatBits(inputWidth * inverseOutputWidth);
        Con0Y = FloatBits(inputHeight * inverseOutputHeight);
        Con0Z = FloatBits((0.5f * inputWidth * inverseOutputWidth) - 0.5f);
        Con0W = FloatBits((0.5f * inputHeight * inverseOutputHeight) - 0.5f);
        Con1X = FloatBits(inverseInputWidth);
        Con1Y = FloatBits(inverseInputHeight);
        Con1Z = FloatBits(inverseInputWidth);
        Con1W = FloatBits(-inverseInputHeight);
        Con2X = FloatBits(-inverseInputWidth);
        Con2Y = FloatBits(2.0f * inverseInputHeight);
        Con2Z = FloatBits(inverseInputWidth);
        Con2W = FloatBits(2.0f * inverseInputHeight);
        Con3X = FloatBits(0.0f);
        Con3Y = FloatBits(4.0f * inverseInputHeight);
        Con3Z = 0;
        Con3W = 0;
        InputWidth = desc.InputWidth;
        InputHeight = desc.InputHeight;
        OutputWidth = desc.OutputWidth;
        OutputHeight = desc.OutputHeight;
        Input = input;
        Output = output;
        Sampler = sampler;
    }

    private static uint FloatBits(float value)
    {
        return BitConverter.SingleToUInt32Bits(value);
    }
}
