using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using SponzaScene.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class VolumetricLightPass : FullscreenPass
{
    private readonly Buffer constantBuffer;
    private readonly Buffer csmDatasBuffer;

    private ResourceTable? resourceTable;

    private int sampleCount = 64;
    private float intensity = 1.0f;
    private float scattering = 0.7f;
    private float maxDistance = 100.0f;

    public VolumetricLightPass() : base("Volumetric Light Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(VolumetricLightConstants),
            StrideInBytes = (uint)sizeof(VolumetricLightConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        csmDatasBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(CSMData) * RenderContext.CSMSplits.Length),
            StrideInBytes = (uint)sizeof(CSMData),
            Flags = BufferUsageFlags.ShaderResource
        });
    }

    protected override string ShaderName => "VolumetricLight";

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
                csmDatasBuffer,
                context.Position!,
                context.CSMDepths!,
                context.VolumetricLight!,
                App.PointSampler,
                App.ShadowSampler
            ]
        });
    }

    protected override void UpdateResources(RenderContext context)
    {
        Matrix4x4.Invert(context.View * context.Projection, out Matrix4x4 inverseViewProjection);

        constantBuffer.Upload([new VolumetricLightConstants
        {
            CameraPosition = new(context.CameraPosition, 1.0f),
            LightDirection = new(App.Sponza.DirectionalLight.Direction, 0.0f),
            LightColor = new(App.Sponza.DirectionalLight.Color, App.Sponza.DirectionalLight.Intensity),
            InverseViewProjection = inverseViewProjection,
            ScreenSize = new(context.Width, context.Height),
            SampleCount = sampleCount,
            Intensity = intensity,
            Scattering = scattering,
            MaxDistance = maxDistance
        }], 0);

        csmDatasBuffer.Upload(context.CSMDatas, 0);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderInt("Sample Count", ref sampleCount, 16, 128);
        ImGui.SliderFloat("Intensity", ref intensity, 0.0f, 5.0f);
        ImGui.SliderFloat("Scattering", ref scattering, 0.0f, 1.0f);
        ImGui.SliderFloat("Max Distance", ref maxDistance, 10.0f, 500.0f);

        ImGuiHelper.Image(context.VolumetricLight!);
    }

    protected override void Destroy()
    {
        resourceTable?.Dispose();
        csmDatasBuffer.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }
}

file struct VolumetricLightConstants
{
    public Vector4 CameraPosition;

    public Vector4 LightDirection;

    public Vector4 LightColor;

    public Matrix4x4 InverseViewProjection;

    public Vector2 ScreenSize;

    public int SampleCount;

    public float Intensity;

    public float Scattering;

    public float MaxDistance;

    private Vector2 padding0;
}