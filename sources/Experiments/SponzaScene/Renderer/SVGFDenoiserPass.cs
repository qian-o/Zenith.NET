using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class SVGFDenoiserPass : RenderPass
{
    private const uint ThreadGroupSize = 16;
    private const int AtrousIterations = 4;  // 改回 4 次迭代

    // Temporal accumulation (now also computes variance)
    private readonly Buffer temporalConstantBuffer;
    private readonly ResourceLayout temporalResourceLayout;
    private readonly ComputePipeline temporalPipeline;

    // Atrous filter
    private readonly Buffer atrousConstantBuffer;
    private readonly ResourceLayout atrousResourceLayout;
    private readonly ComputePipeline atrousPipeline;

    // Resource sets
    private ResourceSet? temporalResourceSetA;
    private ResourceSet? temporalResourceSetB;
    private ResourceSet?[] atrousResourceSets = new ResourceSet?[AtrousIterations];

    private bool useSetA = true;

    // Parameters - 优化后的参数
    private float temporalAlpha = 0.06f;
    private float momentsAlpha = 0.15f;
    private float phiNormal = 24.0f;
    private float phiDepth = 0.06f;
    private float sigmaLuminance = 6.0f;

    public SVGFDenoiserPass() : base("SVGF Denoiser Pass")
    {
        // Temporal accumulation pass (now includes variance computation)
        temporalConstantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SVGFTemporalConstants),
            StrideInBytes = (uint)sizeof(SVGFTemporalConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        temporalResourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // colorTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // historyColorTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // historyMomentsTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // positionTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // normalTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // prevPositionTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // prevNormalTexture
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },  // outputColor
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },  // outputMoments (now stores variance directly)
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        using Shader temporalCs = App.Context.LoadShaderFromFile(GetShaderPath("SVGFTemporalAccumulation"), "CSMain", ShaderStageFlags.Compute);
        temporalPipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = temporalCs,
            ResourceLayouts = [temporalResourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });

        // Atrous filter pass
        atrousConstantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SVGFAtrousConstants),
            StrideInBytes = (uint)sizeof(SVGFAtrousConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        atrousResourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // colorTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // varianceTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // positionTexture
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // normalTexture
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },  // outputColor
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        using Shader atrousCs = App.Context.LoadShaderFromFile(GetShaderPath("SVGFAtrous"), "CSMain", ShaderStageFlags.Compute);
        atrousPipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = atrousCs,
            ResourceLayouts = [atrousResourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
    }

    public override void Resize(uint width, uint height)
    {
        DisposeResourceSets();
        useSetA = true;
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        EnsureResourceSets(context);

        uint dispatchX = (context.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint dispatchY = (context.Height + ThreadGroupSize - 1) / ThreadGroupSize;

        // Pass 1: Temporal accumulation (also computes variance inline)
        temporalConstantBuffer.Upload([new SVGFTemporalConstants
        {
            PrevViewProjection = context.PrevViewProjection,
            ViewportSize = new Vector2(context.Width, context.Height),
            TexelSize = new Vector2(1.0f / context.Width, 1.0f / context.Height),
            TemporalAlpha = temporalAlpha,
            MomentsAlpha = momentsAlpha,
            FrameIndex = context.FrameIndex,
            Padding = 0
        }], 0);

        commandBuffer.SetPipeline(temporalPipeline);
        commandBuffer.SetResourceSet(useSetA ? temporalResourceSetA! : temporalResourceSetB!, 0);
        commandBuffer.Dispatch(dispatchX, dispatchY, 1);

        // Pass 2: Atrous wavelet filter (4 iterations: step sizes 1, 2, 4, 8)
        commandBuffer.SetPipeline(atrousPipeline);

        for (int i = 0; i < AtrousIterations; i++)
        {
            int stepSize = 1 << i; // 1, 2, 4, 8

            atrousConstantBuffer.Upload([new SVGFAtrousConstants
            {
                TexelSize = new Vector2(1.0f / context.Width, 1.0f / context.Height),
                StepSize = stepSize,
                PhiColor = 10.0f,
                PhiNormal = phiNormal,
                PhiDepth = phiDepth,
                SigmaLuminance = sigmaLuminance,
                Padding = 0
            }], 0);

            commandBuffer.SetResourceSet(atrousResourceSets[i]!, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);
        }

        useSetA = !useSetA;
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.Text("Temporal Settings:");
        ImGui.SliderFloat("Temporal Alpha", ref temporalAlpha, 0.01f, 0.5f);
        ImGui.SliderFloat("Moments Alpha", ref momentsAlpha, 0.05f, 0.5f);

        ImGui.Separator();
        ImGui.Text("Spatial Filter Settings:");
        ImGui.SliderFloat("Phi Normal", ref phiNormal, 16.0f, 256.0f);
        ImGui.SliderFloat("Phi Depth", ref phiDepth, 0.005f, 0.1f);
        ImGui.SliderFloat("Sigma Luminance", ref sigmaLuminance, 1.0f, 20.0f);

        Vector2 size = new(ImGui.GetContentRegionAvail().X);
        size = size with { Y = size.X * context.Height / context.Width };

        ImGui.Text("Denoised Output:");
        ImGui.Image(App.Binding(context.SSGIDenoised!), size);
    }

    protected override void Destroy()
    {
        DisposeResourceSets();

        atrousPipeline.Dispose();
        atrousResourceLayout.Dispose();
        atrousConstantBuffer.Dispose();

        temporalPipeline.Dispose();
        temporalResourceLayout.Dispose();
        temporalConstantBuffer.Dispose();

        base.Destroy();
    }

    private void EnsureResourceSets(RenderContext context)
    {
        // Temporal accumulation resource sets (ping-pong)
        temporalResourceSetA ??= App.Context.CreateResourceSet(new()
        {
            Layout = temporalResourceLayout,
            Resources =
            [
                temporalConstantBuffer,
                context.SSGICurrent!,                    // Current frame noisy SSGI
                context.SVGFColorHistoryB!,              // Previous accumulated color
                context.SVGFMomentsHistoryB!,            // Previous moments/variance
                context.Position!,
                context.Normal!,
                context.PositionHistory!,                // Previous frame positions
                context.NormalHistory!,                  // Previous frame normals
                context.SVGFColorHistoryA!,              // Output accumulated color
                context.SVGFMomentsHistoryA!,            // Output moments/variance
                App.LinearSampler
            ]
        });

        temporalResourceSetB ??= App.Context.CreateResourceSet(new()
        {
            Layout = temporalResourceLayout,
            Resources =
            [
                temporalConstantBuffer,
                context.SSGICurrent!,
                context.SVGFColorHistoryA!,
                context.SVGFMomentsHistoryA!,
                context.Position!,
                context.Normal!,
                context.PositionHistory!,
                context.NormalHistory!,
                context.SVGFColorHistoryB!,
                context.SVGFMomentsHistoryB!,
                App.LinearSampler
            ]
        });

        // Atrous filter resource sets (ping-pong between temp textures)
        // Use moments texture as variance source (variance is stored in .x component)
        Texture varianceTexture = useSetA ? context.SVGFMomentsHistoryA! : context.SVGFMomentsHistoryB!;

        for (int i = 0; i < AtrousIterations; i++)
        {
            if (atrousResourceSets[i] == null)
            {
                Texture inputColor;
                Texture outputColor;

                if (i == 0)
                {
                    inputColor = useSetA ? context.SVGFColorHistoryA! : context.SVGFColorHistoryB!;
                    outputColor = context.SVGFAtrousTemp0!;
                }
                else if (i == AtrousIterations - 1)
                {
                    inputColor = (i % 2 == 1) ? context.SVGFAtrousTemp0! : context.SVGFAtrousTemp1!;
                    outputColor = context.SSGIDenoised!;
                }
                else
                {
                    inputColor = (i % 2 == 1) ? context.SVGFAtrousTemp0! : context.SVGFAtrousTemp1!;
                    outputColor = (i % 2 == 1) ? context.SVGFAtrousTemp1! : context.SVGFAtrousTemp0!;
                }

                atrousResourceSets[i] = App.Context.CreateResourceSet(new()
                {
                    Layout = atrousResourceLayout,
                    Resources =
                    [
                        atrousConstantBuffer,
                        inputColor,
                        varianceTexture,
                        context.Position!,
                        context.Normal!,
                        outputColor,
                        App.PointSampler
                    ]
                });
            }
        }
    }

    private void DisposeResourceSets()
    {
        for (int i = 0; i < AtrousIterations; i++)
        {
            atrousResourceSets[i]?.Dispose();
            atrousResourceSets[i] = null;
        }

        temporalResourceSetB?.Dispose();
        temporalResourceSetB = null;

        temporalResourceSetA?.Dispose();
        temporalResourceSetA = null;
    }

    private struct SVGFTemporalConstants
    {
        public Matrix4x4 PrevViewProjection;
        public Vector2 ViewportSize;
        public Vector2 TexelSize;
        public float TemporalAlpha;
        public float MomentsAlpha;
        public uint FrameIndex;
        public float Padding;
    }

    private struct SVGFAtrousConstants
    {
        public Vector2 TexelSize;
        public int StepSize;
        public float PhiColor;
        public float PhiNormal;
        public float PhiDepth;
        public float SigmaLuminance;
        public float Padding;
    }
}
