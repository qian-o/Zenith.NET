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
    private const int MaxIterations = 5;

    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private ResourceSet? resourceSetAB;
    private ResourceSet? resourceSetBA;

    private int iterations = 5;
    private float phiColor = 10.0f;
    private float phiNormal = 128.0f;
    private float phiDepth = 0.5f;

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
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
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
        DisposeResourceSets();
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        EnsureResourceSets(context);

        uint dispatchX = (context.Width + ThreadGroupSize - 1) / ThreadGroupSize;
        uint dispatchY = (context.Height + ThreadGroupSize - 1) / ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);

        bool pingPong = true;

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
            commandBuffer.SetResourceSet(pingPong ? resourceSetAB! : resourceSetBA!, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            pingPong = !pingPong;
        }

        if (pingPong)
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
        if (ImGui.SliderInt("Iterations", ref iterations, 1, MaxIterations))
        {
            DisposeResourceSets();
        }

        ImGui.SliderFloat("Phi Color", ref phiColor, 1.0f, 50.0f);
        ImGui.SliderFloat("Phi Normal", ref phiNormal, 16.0f, 256.0f);
        ImGui.SliderFloat("Phi Depth", ref phiDepth, 0.1f, 5.0f);

        ImGuiHelpers.Image(context.RTGI!);
    }

    protected override void Destroy()
    {
        DisposeResourceSets();
        pipeline.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private void EnsureResourceSets(RenderContext context)
    {
        resourceSetAB ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources = [constantBuffer, context.SVGFPingPong!, context.Position!, context.Normal!, context.RTGI!, App.PointSampler]
        });

        resourceSetBA ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources = [constantBuffer, context.RTGI!, context.Position!, context.Normal!, context.SVGFPingPong!, App.PointSampler]
        });
    }

    private void DisposeResourceSets()
    {
        resourceSetAB?.Dispose();
        resourceSetAB = null;

        resourceSetBA?.Dispose();
        resourceSetBA = null;
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
