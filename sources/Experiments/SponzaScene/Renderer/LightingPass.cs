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
    private readonly Buffer csmDatasBuffer;

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

        csmDatasBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(CSMData) * RenderContext.CSMSplits.Length),
            StrideInBytes = (uint)sizeof(CSMData),
            Flags = BufferUsageFlags.ShaderResource
        });
    }

    protected override string ShaderName => "Lighting";

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
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
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
                csmDatasBuffer,
                context.Albedo!,
                context.Normal!,
                context.Position!,
                context.MetallicRoughness!,
                context.Emissive!,
                context.CSMDepths!,
                context.GTAOBlurred!,
                context.SSGIDenoised!,  // Use SVGF denoised output instead of SSGIBlurred
                context.LitColor!,
                App.PointSampler,
                App.ShadowSampler
            ]
        });
    }

    protected override void UpdateResources(RenderContext context)
    {
        Matrix4x4.Invert(context.View * context.Projection, out Matrix4x4 inverseViewProjection);

        constantBuffer.Upload([new LightingConstants
        {
            CameraPosition = new(context.CameraPosition, 1.0f),
            InverseViewProjection = inverseViewProjection,
            DirectionalLight = App.Sponza.DirectionalLight
        }], 0);

        csmDatasBuffer.Upload(context.CSMDatas, 0);
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
        csmDatasBuffer.Dispose();
        pointLightsBuffer.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private struct LightingConstants
    {
        public Vector4 CameraPosition;

        public Matrix4x4 InverseViewProjection;

        public DirectionalLight DirectionalLight;
    }
}