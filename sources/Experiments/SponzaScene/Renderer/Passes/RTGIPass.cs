using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using SponzaScene.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class RTGIPass : RenderPass
{
    private readonly Buffer constantBuffer;
    private readonly Buffer pointLightsBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly RayTracingPipeline pipeline;

    private ResourceSet? resourceSet;

    private RTGIConstants constants;

    private float intensity = 1.0f;

    public RTGIPass() : base("RTGI Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(RTGIConstants),
            StrideInBytes = (uint)sizeof(RTGIConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        pointLightsBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(PointLight) * App.Sponza.PointLights.Length),
            StrideInBytes = (uint)sizeof(PointLight),
            Flags = BufferUsageFlags.ShaderResource
        });
        pointLightsBuffer.Upload(App.Sponza.PointLights, 0);

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.RayGeneration | ShaderStageFlags.Miss | ShaderStageFlags.ClosestHit },
                new() { Type = ResourceType.AccelerationStructure, Count = 1, StageFlags = ShaderStageFlags.RayGeneration },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.ClosestHit },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.RayGeneration | ShaderStageFlags.ClosestHit },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.RayGeneration | ShaderStageFlags.ClosestHit },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.RayGeneration | ShaderStageFlags.ClosestHit },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.RayGeneration | ShaderStageFlags.ClosestHit },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.RayGeneration },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.RayGeneration | ShaderStageFlags.ClosestHit }
            )
        });

        string shaderPath = GetShaderPath("RTGI");

        using Shader rayGen = App.Context.LoadShaderFromFile(shaderPath, "RayGen", ShaderStageFlags.RayGeneration);
        using Shader miss = App.Context.LoadShaderFromFile(shaderPath, "Miss", ShaderStageFlags.Miss);
        using Shader closestHit = App.Context.LoadShaderFromFile(shaderPath, "ClosestHit", ShaderStageFlags.ClosestHit);

        pipeline = App.Context.CreateRayTracingPipeline(new()
        {
            RayGeneration = rayGen,
            Miss = [miss],
            AnyHit = [],
            Intersection = [],
            ClosestHit = [closestHit],
            HitGroups =
            [
                new()
                {
                    Name = "HitGroup",
                    Type = HitGroupType.Triangles,
                    ClosestHit = "ClosestHit"
                }
            ],
            ResourceLayouts = [resourceLayout],
            MaxTraceRecursionDepth = 1,
            MaxPayloadSizeInBytes = 32,
            MaxAttributeSizeInBytes = 8
        });
    }

    public override void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        constants.Update(context.Width,
                         context.Height,
                         intensity,
                         context.View * context.Projection,
                         App.Sponza.DirectionalLight);

        constantBuffer.Upload([constants], 0);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceSet(EnsureResourceSet(context), 0);
        commandBuffer.DispatchRays(context.Width, context.Height, 1);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderFloat("Intensity", ref intensity, 0.0f, 3.0f);

        ImGuiHelpers.Image(context.RTGI!);
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();
        pipeline.Dispose();
        resourceLayout.Dispose();
        pointLightsBuffer.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private ResourceSet EnsureResourceSet(RenderContext context)
    {
        return resourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                App.Sponza.TLAS!,
                pointLightsBuffer,
                context.Albedo!,
                context.Normal!,
                context.Position!,
                context.NormalizedDepth!,
                context.RTGI!,
                App.LinearSampler
            ]
        });
    }

    private struct RTGIConstants
    {
        public uint Width;

        public uint Height;

        public uint FrameIndex;

        public float Intensity;

        public Matrix4x4 ViewProjection;

        public DirectionalLight DirectionalLight;

        public void Update(uint width,
                           uint height,
                           float intensity,
                           Matrix4x4 viewProjection,
                           DirectionalLight directionalLight)
        {
            int hashCode = HashCode();

            Width = width;
            Height = height;
            Intensity = intensity;
            ViewProjection = viewProjection;
            DirectionalLight = directionalLight;

            if (hashCode != HashCode())
            {
                FrameIndex = 0;
            }
            else
            {
                FrameIndex++;
            }
        }

        private readonly int HashCode()
        {
            HashCode hash = new();

            hash.Add(Width);
            hash.Add(Height);
            hash.Add(Intensity);
            hash.Add(ViewProjection);
            hash.Add(DirectionalLight);

            return hash.ToHashCode();
        }
    }
}
