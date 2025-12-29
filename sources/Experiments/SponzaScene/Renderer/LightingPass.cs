using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class LightingPass : FullscreenPass
{
    private readonly Buffer constantBuffer;
    private readonly Buffer pointLightsBuffer;

    private ResourceSet? resourceSet;

    public LightingPass() : base("LightingPass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(LightingConstants),
            StrideInBytes = (uint)sizeof(LightingConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        pointLightsBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(PointLight) * App.Sponza.PointLights.Length),
            StrideInBytes = (uint)sizeof(PointLight),
            Flags = BufferUsageFlags.ShaderResource
        });
        pointLightsBuffer.Upload(App.Sponza.PointLights, 0);
    }

    protected override string ShaderName => "Lighting";

    protected override Output Output => RenderContext.LightingOutput;

    public override void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;
    }

    protected override ResourceLayout? CreateResourceLayout()
    {
        return App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            )
        });
    }

    protected override ResourceSet EnsureResourceSet(ResourceLayout resourceLayout, RenderContext context)
    {
        return resourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                pointLightsBuffer,
                context.Albedo!,
                context.Normal!,
                context.Position!,
                context.MetallicRoughness!,
                context.Emissive!,
                context.SSAOBlurred!,
                App.PointSampler
            ]
        });
    }

    protected override (FrameBuffer? FrameBuffer, ClearValue ClearValue) GetTarget(RenderContext context)
    {
        return (context.LightingFrameBuffer, ClearValues.Default);
    }

    protected override void UpdateResources(RenderContext context)
    {
        // 更新常量
        constantBuffer.Upload([new LightingConstants
        {
            CameraPosition = new(context.CameraPosition, 1.0f),
            DirectionalLight = App.Sponza.DirectionalLight
        }], 0);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        Vector2 size = new(ImGui.GetContentRegionAvail().X);
        size = size with { Y = size.X * context.Height / context.Width };

        ImGui.Image(App.Binding(context.LitColor!), size);
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();
        pointLightsBuffer.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private struct LightingConstants
    {
        public Vector4 CameraPosition;

        public DirectionalLight DirectionalLight;
    }
}