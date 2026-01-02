using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class SSGIPass : RenderPass
{
    private const uint ThreadGroupSize = 16;

    private readonly Buffer constantBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ComputePipeline pipeline;

    private ResourceSet? resourceSetA;
    private ResourceSet? resourceSetB;

    private uint frameCount;
    private bool useSetA = true;

    // Previous frame data for reprojection and motion detection
    private Matrix4x4 prevViewProjection = Matrix4x4.Identity;
    private Vector3 prevCameraPosition = Vector3.Zero;

    private float maxDistance = 20.0f;
    private float thickness = 1.0f;
    private float intensity = 2.5f;
    private int maxSteps = 24;              // 24 步
    private int binarySearchSteps = 0;
    private float roughnessThreshold = 1.0f;
    private int sampleCount = 8;            // 8 样本
    private float temporalBlend = 0.88f;

    public SSGIPass() : base("SSGI Pass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SSGIConstants),
            StrideInBytes = (uint)sizeof(SSGIConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // position
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // normal
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // albedo
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // metallicRoughness
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // litColorHistory
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },  // ssgiHistory
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute },  // output
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        using Shader cs = App.Context.LoadShaderFromFile(GetShaderPath("SSGI"), "CSMain", ShaderStageFlags.Compute);

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
        resourceSetA?.Dispose();
        resourceSetA = null;
        resourceSetB?.Dispose();
        resourceSetB = null;
        frameCount = 0;
        useSetA = true;
        prevViewProjection = Matrix4x4.Identity;
        prevCameraPosition = Vector3.Zero;
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        EnsureResourceSets(context);

        Matrix4x4 currentViewProjection = context.View * context.Projection;

        // Detect camera motion
        float cameraMotion = Vector3.Distance(context.CameraPosition, prevCameraPosition);
        // Normalize motion - small movements don't affect much, large movements reduce history weight significantly
        float motionFactor = MathF.Min(cameraMotion * 10.0f, 1.0f); // 0.1 units of movement = full motion

        constantBuffer.Upload([new SSGIConstants
        {
            View = context.View,
            Projection = context.Projection,
            PrevViewProjection = prevViewProjection,
            CameraPosition = new Vector4(context.CameraPosition, 1.0f),
            ViewportSize = new Vector2(context.Width, context.Height),
            ViewportPixelSize = new Vector2(1.0f / context.Width, 1.0f / context.Height),
            MaxDistance = maxDistance,
            Thickness = thickness,
            Intensity = intensity,
            MaxSteps = maxSteps,
            BinarySearchSteps = binarySearchSteps,
            RoughnessThreshold = roughnessThreshold,
            SampleCount = sampleCount,
            TemporalBlend = temporalBlend,
            FrameIndex = frameCount++,
            CameraMotion = motionFactor
        }], 0);

        // Store for next frame
        prevViewProjection = currentViewProjection;
        prevCameraPosition = context.CameraPosition;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceSet(useSetA ? resourceSetA! : resourceSetB!, 0);
        commandBuffer.Dispatch((context.Width + ThreadGroupSize - 1) / ThreadGroupSize,
                               (context.Height + ThreadGroupSize - 1) / ThreadGroupSize, 1);

        useSetA = !useSetA;
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        ImGui.SliderFloat("Max Distance", ref maxDistance, 1.0f, 20.0f);
        ImGui.SliderFloat("Thickness", ref thickness, 0.1f, 3.0f);
        ImGui.SliderFloat("Intensity", ref intensity, 0.0f, 5.0f);
        ImGui.SliderInt("Max Steps", ref maxSteps, 16, 96);
        ImGui.SliderInt("Binary Search Steps", ref binarySearchSteps, 2, 16);
        ImGui.SliderFloat("Roughness Threshold", ref roughnessThreshold, 0.0f, 1.0f);
        ImGui.SliderInt("Sample Count", ref sampleCount, 1, 16);
        ImGui.SliderFloat("Temporal Blend", ref temporalBlend, 0.0f, 0.99f);

        Vector2 size = new(ImGui.GetContentRegionAvail().X);
        size = size with { Y = size.X * context.Height / context.Width };

        ImGui.Image(App.Binding(useSetA ? context.SSGIHistory! : context.SSGI!), size);
    }

    protected override void Destroy()
    {
        resourceSetB?.Dispose();
        resourceSetA?.Dispose();
        pipeline.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    private void EnsureResourceSets(RenderContext context)
    {
        resourceSetA ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.Position!,
                context.Normal!,
                context.Albedo!,
                context.MetallicRoughness!,
                context.LitColorHistory!,  // Previous frame's lit color
                context.SSGIHistory!,      // Previous SSGI result for temporal accumulation
                context.SSGI!,             // Output
                App.LinearSampler
            ]
        });

        resourceSetB ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                context.Position!,
                context.Normal!,
                context.Albedo!,
                context.MetallicRoughness!,
                context.LitColorHistory!,  // Previous frame's lit color
                context.SSGI!,             // Previous SSGI result for temporal accumulation
                context.SSGIHistory!,      // Output
                App.LinearSampler
            ]
        });
    }

    private struct SSGIConstants
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Matrix4x4 PrevViewProjection;
        public Vector4 CameraPosition;
        public Vector2 ViewportSize;
        public Vector2 ViewportPixelSize;
        public float MaxDistance;
        public float Thickness;
        public float Intensity;
        public int MaxSteps;
        public int BinarySearchSteps;
        public float RoughnessThreshold;
        public int SampleCount;
        public float TemporalBlend;
        public uint FrameIndex;
        public float CameraMotion;
    }
}
