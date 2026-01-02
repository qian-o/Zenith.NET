using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class GTAOPass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceSet? resourceSet;

    private float effectRadius = 1.5f;
    private float effectFalloffRange = 2.0f;
    private float radiusMultiplier = 1.2f;
    private float finalValuePower = 1.5f;
    private float sampleDistributionPower = 2.0f;
    private float thinOccluderCompensation = 0.25f;
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
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },
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
            ViewportSize = new Vector2(context.Width, context.Height),
            ViewportPixelSize = new Vector2(1.0f / context.Width, 1.0f / context.Height),
            EffectRadius = effectRadius,
            EffectFalloffRange = effectFalloffRange,
            RadiusMultiplier = radiusMultiplier,
            FinalValuePower = finalValuePower,
            SampleDistributionPower = sampleDistributionPower,
            ThinOccluderCompensation = thinOccluderCompensation,
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
        ImGui.SliderFloat("Thin Occluder Compensation", ref thinOccluderCompensation, 0.1f, 0.7f);
        ImGui.SliderInt("Slice Count", ref sliceCount, 2, 12);
        ImGui.SliderInt("Steps Per Slice", ref stepsPerSlice, 2, 16);

        Vector2 size = new(ImGui.GetContentRegionAvail().X);
        size = size with { Y = size.X * context.Height / context.Width };

        ImGui.Image(App.Binding(context.GTAO!), size);
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private struct GTAOConstants
    {
        public Matrix4x4 View;

        public Matrix4x4 Projection;

        public Vector2 ViewportSize;

        public Vector2 ViewportPixelSize;

        public float EffectRadius;

        public float EffectFalloffRange;

        public float RadiusMultiplier;

        public float FinalValuePower;

        public float SampleDistributionPower;

        public float ThinOccluderCompensation;

        public int SliceCount;

        public int StepsPerSlice;
    }
}