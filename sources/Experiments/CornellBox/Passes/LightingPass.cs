using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class LightingPass : DisposableObject
{
    public const PixelFormat ColorFormat = PixelFormat.R16G16B16A16Float;

    private const uint FlagReset = 1u << 0;
    private const uint FlagHasReactiveMask = 1u << 1;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline pipeline;

    public LightingPass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(LightingConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Lighting.slang"), "LightingCS"));
        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture Color { get; private set; } = null!;

    public Texture? ReactiveMask { get; private set; }

    public void Record(CommandBuffer commandBuffer, SceneResources scene, GBufferPass gBuffer, GiProbePass giProbe, ShadowPass shadow, in FrameContext frame)
    {
        uint flags = (frame.ResetLighting ? FlagReset : 0u) | (ReactiveMask is not null ? FlagHasReactiveMask : 0u);

        LightingConstants constants = new()
        {
            InverseJitteredViewProjection = frame.InverseJitteredViewProjection,
            InverseJitteredProjection = frame.InverseJitteredProjection,
            InverseView = frame.InverseView,
            ViewProjection = frame.ViewProjection,
            PreviousViewProjection = frame.PreviousViewProjection,
            CameraPosition = frame.CameraPosition,
            Flags = flags,
            RenderSize = new(Width, Height),
            Environment = frame.Environment,
            Scene = scene.AccelerationStructure,
            Vertices = scene.Vertices,
            Indices = scene.Indices,
            Materials = scene.Materials,
            Instances = scene.Instances,
            AlbedoAndMaterial = gBuffer.AlbedoAndMaterial.SampledHandle,
            NormalAndRoughness = gBuffer.NormalAndRoughness.SampledHandle,
            Depth = gBuffer.Depth.SampledHandle,
            MotionVectors = gBuffer.MotionVectors.SampledHandle,
            ShadowAo = shadow.ShadowAo.SampledHandle,
            IrradianceAtlas = giProbe.IrradianceAtlas.SampledHandle,
            DistanceAtlas = giProbe.DistanceAtlas.SampledHandle,
            LinearSampler = giProbe.LinearSampler.Handle,
            PrevLinearDepth = shadow.PreviousLinearDepth.SampledHandle,
            Color = Color.StorageHandle,
            ReactiveMask = ReactiveMask?.StorageHandle ?? default
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(LightingConstants)
        });

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        if (ReactiveMask is not null)
        {
            commandBuffer.Transition(ReactiveMask, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        ThreadGroupSize group = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);

        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (ReactiveMask is not null)
        {
            commandBuffer.Transition(ReactiveMask, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
    }

    public void Resize(uint width, uint height, bool hasReactiveMask)
    {
        if (Width == width && Height == height && (ReactiveMask is not null) == hasReactiveMask)
        {
            return;
        }

        DestroyResources();
        CreateResources(width, height, hasReactiveMask);
    }

    protected override void Destroy()
    {
        DestroyResources();

        pipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void CreateResources(uint width, uint height, bool hasReactiveMask)
    {
        Width = width;
        Height = height;

        Color = CreateUpscalingTexture(ColorFormat, width, height);

        if (hasReactiveMask)
        {
            ReactiveMask = CreateUpscalingTexture(PixelFormat.R16Float, width, height);
        }
    }

    private void DestroyResources()
    {
        ReactiveMask?.Dispose();
        ReactiveMask = null;
        Color?.Dispose();
        Color = null!;

        Width = 0;
        Height = 0;
    }

    private static Texture CreateUpscalingTexture(PixelFormat format, uint width, uint height)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Storage | TextureUsages.UpscalingInput
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 768)]
file struct LightingConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InverseJitteredViewProjection;

    [FieldOffset(64)]
    public Matrix4x4 InverseJitteredProjection;

    [FieldOffset(128)]
    public Matrix4x4 InverseView;

    [FieldOffset(192)]
    public Matrix4x4 ViewProjection;

    [FieldOffset(256)]
    public Matrix4x4 PreviousViewProjection;

    [FieldOffset(320)]
    public Vector3 CameraPosition;

    [FieldOffset(332)]
    public uint Flags;

    [FieldOffset(336)]
    public Vector2 RenderSize;

    [FieldOffset(352)]
    public SceneEnvironment Environment;

    [FieldOffset(448)]
    public ResourceHandle Scene;

    [FieldOffset(456)]
    public ResourceHandle Vertices;

    [FieldOffset(464)]
    public ResourceHandle Indices;

    [FieldOffset(472)]
    public ResourceHandle Materials;

    [FieldOffset(480)]
    public ResourceHandle Instances;

    [FieldOffset(488)]
    public ResourceHandle AlbedoAndMaterial;

    [FieldOffset(496)]
    public ResourceHandle NormalAndRoughness;

    [FieldOffset(504)]
    public ResourceHandle Depth;

    [FieldOffset(512)]
    public ResourceHandle MotionVectors;

    [FieldOffset(520)]
    public ResourceHandle ShadowAo;

    [FieldOffset(528)]
    public ResourceHandle IrradianceAtlas;

    [FieldOffset(536)]
    public ResourceHandle DistanceAtlas;

    [FieldOffset(544)]
    public ResourceHandle LinearSampler;

    [FieldOffset(552)]
    public ResourceHandle PrevLinearDepth;

    [FieldOffset(560)]
    public ResourceHandle Color;

    [FieldOffset(568)]
    public ResourceHandle ReactiveMask;
}
