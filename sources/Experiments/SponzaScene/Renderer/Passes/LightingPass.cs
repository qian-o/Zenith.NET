using System.Numerics;
using System.Runtime.InteropServices;
using SponzaScene.Helpers;
using SponzaScene.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class LightingPass : FullscreenPass
{
    private readonly Buffer constantBuffer;
    private readonly Buffer pointLightsBuffer;
    private readonly Buffer csmDatasBuffer;

    private ResourceTable? resourceTable;

    public LightingPass() : base("Lighting Pass")
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
        resourceTable?.Dispose();
        resourceTable = null;
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

    protected override ResourceTable EnsureResourceTable(ResourceLayout resourceLayout, RenderContext context)
    {
        return resourceTable ??= App.Context.CreateResourceTable(new()
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
                context.RTGI!,
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
        ImGuiHelper.Image(context.LitColor!);
    }

    protected override void Destroy()
    {
        resourceTable?.Dispose();
        csmDatasBuffer.Dispose();
        pointLightsBuffer.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 112)]
file struct LightingConstants
{
    [FieldOffset(0)]
    public Vector4 CameraPosition;

    [FieldOffset(16)]
    public Matrix4x4 InverseViewProjection;

    [FieldOffset(80)]
    public DirectionalLight DirectionalLight;
}