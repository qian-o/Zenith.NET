using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class GTAOPass : FullscreenPass
{
    private readonly Buffer constantBuffer;

    private ResourceSet? resourceSet;

    // 调整后的默认参数
    private float effectRadius = 2.0f;          // 增大效果半径
    private float effectFalloffRange = 1.5f;    // 减小衰减，让 AO 更柔和
    private float radiusMultiplier = 1.5f;      // 增大半径倍数
    private float finalValuePower = 1.0f;       // 减小，避免过暗
    private float sampleDistributionPower = 1.5f; // 减小，采样更均匀
    private float thinOccluderCompensation = 0.0f;
    private int sliceCount = 4;                 // 增加方向数
    private int stepsPerSlice = 6;              // 增加采样步数

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
        ImGui.SliderFloat("Thin Occluder Compensation", ref thinOccluderCompensation, 0.0f, 0.7f);
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

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct GTAOConstants
    {
        [FieldOffset(0)]
        public Matrix4x4 View;

        [FieldOffset(64)]
        public Matrix4x4 Projection;

        [FieldOffset(128)]
        public Vector2 ViewportSize;

        [FieldOffset(136)]
        public Vector2 ViewportPixelSize;

        [FieldOffset(144)]
        public float EffectRadius;

        [FieldOffset(148)]
        public float EffectFalloffRange;

        [FieldOffset(152)]
        public float RadiusMultiplier;

        [FieldOffset(156)]
        public float FinalValuePower;

        [FieldOffset(160)]
        public float SampleDistributionPower;

        [FieldOffset(164)]
        public float ThinOccluderCompensation;

        [FieldOffset(168)]
        public int SliceCount;

        [FieldOffset(172)]
        public int StepsPerSlice;
    }
}