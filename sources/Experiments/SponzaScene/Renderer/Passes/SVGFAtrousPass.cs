using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class SVGFAtrousPass : RenderPass
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private ResourceSet? readPingWritePong;
    private ResourceSet? readPongWritePing;

    private int iterations = 6;
    private float phiColor = 2.0f;
    private float phiNormal = 32.0f;
    private float phiDepth = 0.05f;

    public SVGFAtrousPass() : base("SVGF A-Trous Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(AtrousConstants),
            StrideInBytes = (uint)sizeof(AtrousConstants),
            Flags = BufferUsageFlags.Constant
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        using Shader cs = App.Context.LoadShaderFromFile(GetShaderPath("SVGFAtrous"), "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = cs,
            ResourceLayouts = [resourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });
    }

    public override void Resize(uint width, uint height)
    {
        readPingWritePong?.Dispose();
        readPingWritePong = null;

        readPongWritePing?.Dispose();
        readPongWritePing = null;
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        EnsureResourceSets(context);

        uint dispatchX = (context.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint dispatchY = (context.Height + ThreadGroupSize - 1) / ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);

        bool writeToPong = true;

        for (int i = 0; i < iterations; i++)
        {
            AtrousConstants constants = new()
            {
                ViewportSize = new Vector2(context.Width, context.Height),
                StepWidth = 1 << i,
                PhiColor = phiColor,
                PhiNormal = phiNormal,
                PhiDepth = phiDepth
            };

            commandBuffer.Upload(constantBuffer, 0, [constants]);
            commandBuffer.SetResourceSet(writeToPong ? readPingWritePong! : readPongWritePing!, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            writeToPong = !writeToPong;
        }

        if (writeToPong)
        {
            commandBuffer.CopyTexture(context.SVGFPingPong!,
                                      default,
                                      default,
                                      context.RTGI!,
                                      default,
                                      default,
                                      new() { Width = context.Width, Height = context.Height, Depth = 1 });
        }
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderInt("Iterations", ref iterations, 1, 7);
        ImGui.SliderFloat("Phi Color", ref phiColor, 0.1f, 10.0f);
        ImGui.SliderFloat("Phi Normal", ref phiNormal, 4.0f, 128.0f);
        ImGui.SliderFloat("Phi Depth", ref phiDepth, 0.01f, 1.0f);

        ImGuiHelpers.Image(context.RTGI!);
    }

    protected override void Destroy()
    {
        readPongWritePing?.Dispose();
        readPingWritePong?.Dispose();

        pipeline.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private void EnsureResourceSets(RenderContext context)
    {
        readPingWritePong ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.SVGFPingPong!,
                context.Position!,
                context.Normal!,
                context.RTGI!
            ]
        });

        readPongWritePing ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.RTGI!,
                context.Position!,
                context.Normal!,
                context.SVGFPingPong!
            ]
        });
    }

    private struct AtrousConstants
    {
        public Vector2 ViewportSize;

        public int StepWidth;

        public float PhiColor;

        public float PhiNormal;

        public float PhiDepth;
    }
}
