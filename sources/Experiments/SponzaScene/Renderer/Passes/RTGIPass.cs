using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using SponzaScene.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class RTGIPass : FullscreenPass
{
    private readonly Buffer constantBuffer;
    private readonly Buffer pointLightsBuffer;

    private ResourceTable? resourceTable;

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
    }

    protected override string ShaderName => "RTGI";

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
                new() { Type = ResourceType.AccelerationStructure, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
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

    protected override void UpdateResources(RenderContext context)
    {
        constantBuffer.Upload([new RTGIConstants
        {
            Width = context.Width,
            Height = context.Height,
            FrameIndex = context.FrameIndex,
            Intensity = intensity,
            ViewProjection = context.View * context.Projection,
            DirectionalLight = App.Sponza.DirectionalLight
        }], 0);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderFloat("Intensity", ref intensity, 0.0f, 3.0f);

        ImGuiHelper.Image(context.RTGI!);
    }

    protected override void Destroy()
    {
        resourceTable?.Dispose();
        pointLightsBuffer.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }
}

file struct RTGIConstants
{
    public uint Width;

    public uint Height;

    public uint FrameIndex;

    public float Intensity;

    public Matrix4x4 ViewProjection;

    public DirectionalLight DirectionalLight;
}
