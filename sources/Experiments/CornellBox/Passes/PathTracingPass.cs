using System.Numerics;
using System.Runtime.InteropServices;
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

    private Texture visibility = null!;
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
                       Vector3 cameraPosition,
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

        uint flags = (reset ? 1u : 0u)
                   | (ReactiveMask is not null ? 2u : 0u)
                   | (DiffuseAlbedo is not null ? 4u : 0u);

        PathTracingConstants constants = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = viewProjection,
            PreviousViewProjection = effectivePreviousViewProjection,
            CameraPosition = cameraPosition,
            FrameIndex = frameIndex,
            RenderSize = new(Width, Height),
            JitterOffset = jitterOffset,
            Flags = flags,
            Scene = scene.AccelerationStructure,
            Vertices = scene.Vertices,
            Indices = scene.Indices,
            Materials = scene.Materials,
            Instances = scene.Instances,
            VisibilityStorage = visibility.StorageHandle,
            VisibilitySampled = visibility.SampledHandle,
            PreviousLinearDepth = previousLinearDepth.SampledHandle,
            CurrentLinearDepth = currentLinearDepth.StorageHandle,
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

        TransitionPrimaryOutputs(commandBuffer, TextureLayout.Undefined, TextureLayout.Storage);

        Dispatch(commandBuffer, primaryRayPipeline);

        TransitionPrimaryOutputs(commandBuffer, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        if (ReactiveMask is not null)
        {
            commandBuffer.Transition(ReactiveMask, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        if (SpecularHitDistance is not null)
        {
            commandBuffer.Transition(SpecularHitDistance, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        Dispatch(commandBuffer, pathTracePipeline);

        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (ReactiveMask is not null)
        {
            commandBuffer.Transition(ReactiveMask, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        if (SpecularHitDistance is not null)
        {
            commandBuffer.Transition(SpecularHitDistance, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

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

    private void TransitionPrimaryOutputs(CommandBuffer commandBuffer, TextureLayout oldLayout, TextureLayout newLayout)
    {
        commandBuffer.Transition(visibility, default, oldLayout, newLayout);
        commandBuffer.Transition(Depth, default, oldLayout, newLayout);
        commandBuffer.Transition(MotionVectors, default, oldLayout, newLayout);
        commandBuffer.Transition(currentLinearDepth, default, oldLayout, newLayout);

        if (DiffuseAlbedo is not null)
        {
            commandBuffer.Transition(DiffuseAlbedo, default, oldLayout, newLayout);
            commandBuffer.Transition(SpecularAlbedo!, default, oldLayout, newLayout);
            commandBuffer.Transition(Normals!, default, oldLayout, newLayout);
            commandBuffer.Transition(Roughness!, default, oldLayout, newLayout);
        }
    }

    private void CreateTextures(uint width, uint height, bool hasReactiveMask, bool hasRayReconstructionGuides)
    {
        Width = width;
        Height = height;

        Color = CreateTexture(ColorFormat, width, height);
        Depth = CreateTexture(PixelFormat.R32Float, width, height);
        MotionVectors = CreateTexture(PixelFormat.R32G32Float, width, height);

        if (hasReactiveMask)
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
        previousLinearDepth = CreateTexture(PixelFormat.R32Float, width, height);
        currentLinearDepth = CreateTexture(PixelFormat.R32Float, width, height);
    }

    private void DestroyTextures()
    {
        currentLinearDepth?.Dispose();
        currentLinearDepth = null!;
        previousLinearDepth?.Dispose();
        previousLinearDepth = null!;
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

[StructLayout(LayoutKind.Explicit, Size = 448)]
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
    public Vector3 CameraPosition;

    [FieldOffset(268)]
    public uint FrameIndex;

    [FieldOffset(272)]
    public Vector2 RenderSize;

    [FieldOffset(280)]
    public Vector2 JitterOffset;

    [FieldOffset(288)]
    public uint Flags;

    [FieldOffset(304)]
    public ResourceHandle Scene;

    [FieldOffset(312)]
    public ResourceHandle Vertices;

    [FieldOffset(320)]
    public ResourceHandle Indices;

    [FieldOffset(328)]
    public ResourceHandle Materials;

    [FieldOffset(336)]
    public ResourceHandle Instances;

    [FieldOffset(344)]
    public ResourceHandle VisibilityStorage;

    [FieldOffset(352)]
    public ResourceHandle VisibilitySampled;

    [FieldOffset(360)]
    public ResourceHandle PreviousLinearDepth;

    [FieldOffset(368)]
    public ResourceHandle CurrentLinearDepth;

    [FieldOffset(376)]
    public ResourceHandle Color;

    [FieldOffset(384)]
    public ResourceHandle Depth;

    [FieldOffset(392)]
    public ResourceHandle MotionVectors;

    [FieldOffset(400)]
    public ResourceHandle ReactiveMask;

    [FieldOffset(408)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(416)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(424)]
    public ResourceHandle Normals;

    [FieldOffset(432)]
    public ResourceHandle Roughness;

    [FieldOffset(440)]
    public ResourceHandle SpecularHitDistance;
}
