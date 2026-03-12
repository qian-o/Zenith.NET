using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class GTAOPass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceTable? resourceTable;

    private float effectRadius = 1.5f;
    private float effectFalloffRange = 2.0f;
    private float radiusMultiplier = 1.2f;
    private float finalValuePower = 1.5f;
    private float sampleDistributionPower = 2.0f;
    private int sliceCount = 3;
    private int stepsPerSlice = 4;

    public GTAOPass() : base("GTAO Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(GTAOConstants),
            StrideInBytes = (uint)sizeof(GTAOConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });
    }

    protected override string ShaderName => "GTAO";

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
                context.Position!,
                context.Normal!,
                context.GTAO!,
                App.PointSampler
            ]
        });
    }

    protected override void UpdateResources(RenderContext context)
    {
        constantBuffer.Upload([new GTAOConstants
        {
            View = context.View,
            Projection = context.Projection,
            ViewportSize = new(context.Width, context.Height),
            EffectRadius = effectRadius,
            EffectFalloffRange = effectFalloffRange,
            RadiusMultiplier = radiusMultiplier,
            FinalValuePower = finalValuePower,
            SampleDistributionPower = sampleDistributionPower,
            SliceCount = sliceCount,
            StepsPerSlice = stepsPerSlice
        }], 0);
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderFloat("Effect Radius", ref effectRadius, 0.5f, 10.0f);
        ImGui.SliderFloat("Falloff Range", ref effectFalloffRange, 0.5f, 4.0f);
        ImGui.SliderFloat("Radius Multiplier", ref radiusMultiplier, 0.5f, 5.0f);
        ImGui.SliderFloat("Final Value Power", ref finalValuePower, 0.5f, 3.0f);
        ImGui.SliderFloat("Sample Distribution Power", ref sampleDistributionPower, 1.0f, 3.0f);
        ImGui.SliderInt("Slice Count", ref sliceCount, 2, 12);
        ImGui.SliderInt("Steps Per Slice", ref stepsPerSlice, 2, 16);

        ImGuiHelper.Image(context.GTAO!);
    }

    protected override void Destroy()
    {
        resourceTable?.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 176)]
file struct GTAOConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector2 ViewportSize;

    [FieldOffset(136)]
    public float EffectRadius;

    [FieldOffset(140)]
    public float EffectFalloffRange;

    [FieldOffset(144)]
    public float RadiusMultiplier;

    [FieldOffset(148)]
    public float FinalValuePower;

    [FieldOffset(152)]
    public float SampleDistributionPower;

    [FieldOffset(156)]
    public int SliceCount;

    [FieldOffset(160)]
    public int StepsPerSlice;
}