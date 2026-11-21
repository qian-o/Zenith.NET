using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class PathTracingPass : DisposableObject
{
    public const PixelFormat ColorFormat = PixelFormat.R16G16B16A16Float;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline primaryRayPipeline;
    private readonly ComputePipeline pathTracePipeline;
    private readonly ComputePipeline reactivePipeline;
    private readonly ComputePipeline rayReconstructionPipeline;

    private Texture visibility = null!;
    private Texture reflectionGuide = null!;
    private Texture previousLinearDepth = null!;
    private Texture currentLinearDepth = null!;
    private Matrix4x4 previousViewProjection = Matrix4x4.Identity;

    public PathTracingPass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(PathTracingConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        primaryRayPipeline = CreatePipeline("PrimaryRayCS");
        pathTracePipeline = CreatePipeline("PathTraceCS");
        reactivePipeline = CreatePipeline("ReactiveMaskCS");
        rayReconstructionPipeline = CreatePipeline("RayReconstructionGuidesCS");
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture Color { get; private set; } = null!;

    public Texture Depth { get; private set; } = null!;

    public Texture MotionVectors { get; private set; } = null!;

    public Texture? ReactiveMask { get; private set; }

    public Texture? DiffuseAlbedo { get; private set; }

    public Texture? SpecularAlbedo { get; private set; }

    public Texture? Normals { get; private set; }

    public Texture? Roughness { get; private set; }

    public Texture? SpecularHitDistance { get; private set; }

    public void Record(CommandBuffer commandBuffer,
                       SceneResources scene,
                       CameraHandler camera,
                       Matrix4x4 view,
                       Matrix4x4 projection,
                       Vector2 jitterOffset,
                       uint frameIndex,
                       bool reset)
    {
        Matrix4x4 viewProjection = view * projection;
        Matrix4x4 effectivePreviousViewProjection = reset ? viewProjection : previousViewProjection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        uint packedFlags = (scene.LightCount & 0xFFFFu) | (reset ? 0x10000u : 0u);

        PathTracingConstants constants = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = viewProjection,
            PreviousViewProjection = effectivePreviousViewProjection,
            CameraPositionAndFrame = new(camera.Position, Unsafe.BitCast<uint, float>(frameIndex)),
            RenderSizeAndJitter = new(Width, Height, jitterOffset.X, jitterOffset.Y),
            SunDirectionAndCosRadius = SceneResources.SunDirectionAndCosRadius,
            SunIrradianceAndWeight = SceneResources.SunIrradianceAndWeight,
            SkyZenithAndIntensity = SceneResources.SkyZenithAndIntensity,
            SkyHorizonAndFlags = new(SceneResources.SkyHorizon, Unsafe.BitCast<uint, float>(packedFlags)),
            Scene = scene.AccelerationStructure,
            Vertices = scene.Vertices,
            Indices = scene.Indices,
            Materials = scene.Materials,
            Instances = scene.Instances,
            Lights = scene.Lights,
            VisibilityStorage = visibility.StorageHandle,
            VisibilitySampled = visibility.SampledHandle,
            ReflectionGuideStorage = reflectionGuide.StorageHandle,
            ReflectionGuideSampled = reflectionGuide.SampledHandle,
            PreviousLinearDepth = previousLinearDepth.SampledHandle,
            CurrentLinearDepthStorage = currentLinearDepth.StorageHandle,
            CurrentLinearDepthSampled = currentLinearDepth.SampledHandle,
            Color = Color.StorageHandle,
            Depth = Depth.StorageHandle,
            MotionVectors = MotionVectors.StorageHandle,
            ReactiveMask = ReactiveMask?.StorageHandle ?? default,
            DiffuseAlbedo = DiffuseAlbedo?.StorageHandle ?? default,
            SpecularAlbedo = SpecularAlbedo?.StorageHandle ?? default,
            Normals = Normals?.StorageHandle ?? default,
            Roughness = Roughness?.StorageHandle ?? default,
            SpecularHitDistance = SpecularHitDistance?.StorageHandle ?? default
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(PathTracingConstants)
        });

        if (reset)
        {
            commandBuffer.Transition(previousLinearDepth, default, TextureLayout.Undefined, TextureLayout.Sampled);
        }

        commandBuffer.Transition(visibility, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(Depth, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(MotionVectors, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(currentLinearDepth, default, TextureLayout.Undefined, TextureLayout.Storage);

        Dispatch(commandBuffer, primaryRayPipeline);

        commandBuffer.Transition(visibility, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(Depth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(currentLinearDepth, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(reflectionGuide, default, TextureLayout.Undefined, TextureLayout.Storage);

        Dispatch(commandBuffer, pathTracePipeline);

        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(reflectionGuide, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (ReactiveMask is not null)
        {
            commandBuffer.Transition(ReactiveMask, default, TextureLayout.Undefined, TextureLayout.Storage);

            if (DiffuseAlbedo is not null)
            {
                commandBuffer.Transition(DiffuseAlbedo, default, TextureLayout.Undefined, TextureLayout.Storage);
                commandBuffer.Transition(SpecularAlbedo!, default, TextureLayout.Undefined, TextureLayout.Storage);
                commandBuffer.Transition(Normals!, default, TextureLayout.Undefined, TextureLayout.Storage);
                commandBuffer.Transition(Roughness!, default, TextureLayout.Undefined, TextureLayout.Storage);
                commandBuffer.Transition(SpecularHitDistance!, default, TextureLayout.Undefined, TextureLayout.Storage);
            }

            Dispatch(commandBuffer, DiffuseAlbedo is not null ? rayReconstructionPipeline : reactivePipeline);

            commandBuffer.Transition(ReactiveMask, default, TextureLayout.Storage, TextureLayout.Sampled);

            if (DiffuseAlbedo is not null)
            {
                commandBuffer.Transition(DiffuseAlbedo, default, TextureLayout.Storage, TextureLayout.Sampled);
                commandBuffer.Transition(SpecularAlbedo!, default, TextureLayout.Storage, TextureLayout.Sampled);
                commandBuffer.Transition(Normals!, default, TextureLayout.Storage, TextureLayout.Sampled);
                commandBuffer.Transition(Roughness!, default, TextureLayout.Storage, TextureLayout.Sampled);
                commandBuffer.Transition(SpecularHitDistance!, default, TextureLayout.Storage, TextureLayout.Sampled);
            }
        }

        commandBuffer.Transition(MotionVectors, default, TextureLayout.Storage, TextureLayout.Sampled);

        (previousLinearDepth, currentLinearDepth) = (currentLinearDepth, previousLinearDepth);
        previousViewProjection = viewProjection;
    }

    public void Resize(uint width, uint height, bool hasReactiveMask, bool hasRayReconstructionGuides)
    {
        if (Width == width && Height == height && (ReactiveMask is not null) == hasReactiveMask && (DiffuseAlbedo is not null) == hasRayReconstructionGuides)
        {
            return;
        }

        DestroyTextures();
        CreateTextures(width, height, hasReactiveMask, hasRayReconstructionGuides);
    }

    protected override void Destroy()
    {
        DestroyTextures();

        rayReconstructionPipeline?.Dispose();
        reactivePipeline?.Dispose();
        pathTracePipeline?.Dispose();
        primaryRayPipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline)
    {
        ThreadGroupSize group = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);
    }

    private void CreateTextures(uint width, uint height, bool hasReactiveMask, bool hasRayReconstructionGuides)
    {
        Width = width;
        Height = height;

        Color = CreateTexture(ColorFormat, width, height);
        Depth = CreateTexture(PixelFormat.R32Float, width, height);
        MotionVectors = CreateTexture(PixelFormat.R32G32Float, width, height);

        if (hasReactiveMask || hasRayReconstructionGuides)
        {
            ReactiveMask = CreateTexture(PixelFormat.R16Float, width, height);
        }

        if (hasRayReconstructionGuides)
        {
            DiffuseAlbedo = CreateTexture(PixelFormat.R16G16B16A16Float, width, height);
            SpecularAlbedo = CreateTexture(PixelFormat.R16G16B16A16Float, width, height);
            Normals = CreateTexture(PixelFormat.R16G16B16A16Float, width, height);
            Roughness = CreateTexture(PixelFormat.R16Float, width, height);
            SpecularHitDistance = CreateTexture(PixelFormat.R32Float, width, height);
        }

        visibility = CreateTexture(PixelFormat.R32G32B32A32UInt, width, height);
        reflectionGuide = CreateTexture(PixelFormat.R16G16B16A16Float, width, height);
        previousLinearDepth = CreateTexture(PixelFormat.R32Float, width, height);
        currentLinearDepth = CreateTexture(PixelFormat.R32Float, width, height);
    }

    private void DestroyTextures()
    {
        currentLinearDepth?.Dispose();
        currentLinearDepth = null!;
        previousLinearDepth?.Dispose();
        previousLinearDepth = null!;
        reflectionGuide?.Dispose();
        reflectionGuide = null!;
        visibility?.Dispose();
        visibility = null!;

        SpecularHitDistance?.Dispose();
        SpecularHitDistance = null;
        Roughness?.Dispose();
        Roughness = null;
        Normals?.Dispose();
        Normals = null;
        SpecularAlbedo?.Dispose();
        SpecularAlbedo = null;
        DiffuseAlbedo?.Dispose();
        DiffuseAlbedo = null;
        ReactiveMask?.Dispose();
        ReactiveMask = null;

        MotionVectors?.Dispose();
        MotionVectors = null!;
        Depth?.Dispose();
        Depth = null!;
        Color?.Dispose();
        Color = null!;

        Width = 0;
        Height = 0;
    }

    private static ComputePipeline CreatePipeline(string entryPoint)
    {
        string shaderDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders");
        string shaderPath = Path.Combine(shaderDirectory, "PathTracing.slang");

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, entryPoint, [shaderDirectory]));

        return App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    private static Texture CreateTexture(PixelFormat format, uint width, uint height)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Storage | TextureUsages.UpscalingInput
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 528)]
file struct PathTracingConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InverseView;

    [FieldOffset(64)]
    public Matrix4x4 InverseProjection;

    [FieldOffset(128)]
    public Matrix4x4 ViewProjection;

    [FieldOffset(192)]
    public Matrix4x4 PreviousViewProjection;

    [FieldOffset(256)]
    public Vector4 CameraPositionAndFrame;

    [FieldOffset(272)]
    public Vector4 RenderSizeAndJitter;

    [FieldOffset(288)]
    public Vector4 SunDirectionAndCosRadius;

    [FieldOffset(304)]
    public Vector4 SunIrradianceAndWeight;

    [FieldOffset(320)]
    public Vector4 SkyZenithAndIntensity;

    [FieldOffset(336)]
    public Vector4 SkyHorizonAndFlags;

    [FieldOffset(352)]
    public ResourceHandle Scene;

    [FieldOffset(360)]
    public ResourceHandle Vertices;

    [FieldOffset(368)]
    public ResourceHandle Indices;

    [FieldOffset(376)]
    public ResourceHandle Materials;

    [FieldOffset(384)]
    public ResourceHandle Instances;

    [FieldOffset(392)]
    public ResourceHandle Lights;

    [FieldOffset(400)]
    public ResourceHandle VisibilityStorage;

    [FieldOffset(408)]
    public ResourceHandle VisibilitySampled;

    [FieldOffset(416)]
    public ResourceHandle ReflectionGuideStorage;

    [FieldOffset(424)]
    public ResourceHandle ReflectionGuideSampled;

    [FieldOffset(432)]
    public ResourceHandle PreviousLinearDepth;

    [FieldOffset(440)]
    public ResourceHandle CurrentLinearDepthStorage;

    [FieldOffset(448)]
    public ResourceHandle CurrentLinearDepthSampled;

    [FieldOffset(456)]
    public ResourceHandle Color;

    [FieldOffset(464)]
    public ResourceHandle Depth;

    [FieldOffset(472)]
    public ResourceHandle MotionVectors;

    [FieldOffset(480)]
    public ResourceHandle ReactiveMask;

    [FieldOffset(488)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(496)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(504)]
    public ResourceHandle Normals;

    [FieldOffset(512)]
    public ResourceHandle Roughness;

    [FieldOffset(520)]
    public ResourceHandle SpecularHitDistance;
}
