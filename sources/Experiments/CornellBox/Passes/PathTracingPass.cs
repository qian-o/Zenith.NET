using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class PathTracingPass : DisposableObject
{
    private readonly Buffer constantBuffer;
    private readonly ComputePipeline pipeline;

    private Matrix4x4 previousViewProjection;

    public PathTracingPass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(PathTracingConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "PathTracing.slang"), "PathTracingCS"));

        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public PathTracingOutput Output { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, CameraHandler camera, Vector2 jitterOffsetInPixels, uint frameIndex)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4 projection = camera.Projection;
        Matrix4x4 viewProjection = view * projection;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(projection, out Matrix4x4 inverseProjection);

        PathTracingConstants pathTracingConstants = new()
        {
            InverseView = inverseView,
            InverseProjection = inverseProjection,
            ViewProjection = viewProjection,
            PreviousViewProjection = frameIndex is 0 ? viewProjection : previousViewProjection,
            Position = new(camera.Position, 1.0f),
            RenderSize = new(Output.Width, Output.Height),
            JitterOffset = jitterOffsetInPixels,
            FrameIndex = frameIndex,
            IsRayReconstructionEnabled = Output.LinearDepthDelta is not null ? 1u : 0u,
            Scene = scene.AccelerationStructure,
            Vertices = scene.Vertices,
            Indices = scene.Indices,
            Materials = scene.Materials,
            Instances = scene.Instances,
            Color = Output.Color.StorageHandle,
            Depth = Output.Depth.StorageHandle,
            MotionVectors = Output.MotionVectors.StorageHandle,
            DiffuseAlbedo = Output.DiffuseAlbedo.StorageHandle,
            SpecularAlbedo = Output.SpecularAlbedo.StorageHandle,
            Normals = Output.Normals.StorageHandle,
            Roughness = Output.Roughness.StorageHandle,
            SpecularHitDistance = Output.SpecularHitDistance.StorageHandle,
            LinearDepthDelta = Output.LinearDepthDelta?.StorageHandle ?? default,
            DirectDiffuseRadiance = Output.DirectDiffuseRadiance?.StorageHandle ?? default,
            DirectSpecularRadiance = Output.DirectSpecularRadiance?.StorageHandle ?? default,
            IndirectDiffuseRadiance = Output.IndirectDiffuseRadiance?.StorageHandle ?? default,
            IndirectSpecularRadiance = Output.IndirectSpecularRadiance?.StorageHandle ?? default
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&pathTracingConstants),
            SizeInBytes = (uint)sizeof(PathTracingConstants)
        });

        foreach (Texture texture in Output.Textures)
        {
            commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        ThreadGroupSize group = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Dispatch((Output.Width + group.X - 1) / group.X, (Output.Height + group.Y - 1) / group.Y, 1);

        foreach (Texture texture in Output.Textures)
        {
            commandBuffer.Transition(texture, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        previousViewProjection = viewProjection;
    }

    public void Resize(uint width, uint height, bool isRayReconstructionEnabled)
    {
        DestroyOutput();
        CreateOutput(width, height, isRayReconstructionEnabled);
    }

    protected override void Destroy()
    {
        DestroyOutput();
        pipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void CreateOutput(uint width, uint height, bool isRayReconstructionEnabled)
    {
        Output = new(width, height, isRayReconstructionEnabled);
    }

    private void DestroyOutput()
    {
        Output?.Dispose();
        Output = null!;
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
    public Vector4 Position;

    [FieldOffset(272)]
    public Vector2 RenderSize;

    [FieldOffset(280)]
    public Vector2 JitterOffset;

    [FieldOffset(288)]
    public uint FrameIndex;

    [FieldOffset(292)]
    public uint IsRayReconstructionEnabled;

    [FieldOffset(296)]
    public ResourceHandle Scene;

    [FieldOffset(304)]
    public ResourceHandle Vertices;

    [FieldOffset(312)]
    public ResourceHandle Indices;

    [FieldOffset(320)]
    public ResourceHandle Materials;

    [FieldOffset(328)]
    public ResourceHandle Instances;

    [FieldOffset(336)]
    public ResourceHandle Color;

    [FieldOffset(344)]
    public ResourceHandle Depth;

    [FieldOffset(352)]
    public ResourceHandle MotionVectors;

    [FieldOffset(360)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(368)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(376)]
    public ResourceHandle Normals;

    [FieldOffset(384)]
    public ResourceHandle Roughness;

    [FieldOffset(392)]
    public ResourceHandle SpecularHitDistance;

    [FieldOffset(400)]
    public ResourceHandle LinearDepthDelta;

    [FieldOffset(408)]
    public ResourceHandle DirectDiffuseRadiance;

    [FieldOffset(416)]
    public ResourceHandle DirectSpecularRadiance;

    [FieldOffset(424)]
    public ResourceHandle IndirectDiffuseRadiance;

    [FieldOffset(432)]
    public ResourceHandle IndirectSpecularRadiance;
}
