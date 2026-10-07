using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class PathTracingPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private const uint ThreadGroupSize = 8;

    private Buffer constantBuffer = null!;
    private ComputePipeline pipeline = null!;
    private Texture[] outputs = [];

    public Texture Color { get; private set; } = null!;

    public Texture Depth { get; private set; } = null!;

    public Texture Normal { get; private set; } = null!;

    public Texture MotionVectors { get; private set; } = null!;

    public Texture DiffuseAlbedo { get; private set; } = null!;

    public Texture SpecularAlbedo { get; private set; } = null!;

    public Texture SpecularHitDistance { get; private set; } = null!;

    protected override void Initialize()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("PathTracing.slang"), "CSMain"));

        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

        CreateTextures();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        Constants constants = new()
        {
            InverseView = args.InverseView,
            InverseProjection = args.InverseProjection,
            ViewProjection = args.ViewProjection,
            PreviousViewProjection = args.PreviousViewProjection,
            PositionFrame = new(args.CameraPosition, BitConverter.UInt32BitsToSingle(args.FrameIndex)),
            RenderSizeJitter = new(RenderWidth, RenderHeight, args.Jitter.X, args.Jitter.Y),
            HitDistance = args.RayReconstruction is not null ? 1u : 0u,
            Scene = args.Scene.AccelerationStructure,
            Vertices = args.Scene.Vertices,
            Indices = args.Scene.Indices,
            Materials = args.Scene.Materials,
            Instances = args.Scene.Instances,
            Color = Color.StorageHandle,
            Depth = Depth.StorageHandle,
            Normal = Normal.StorageHandle,
            MotionVectors = MotionVectors.StorageHandle,
            DiffuseAlbedo = DiffuseAlbedo.StorageHandle,
            SpecularAlbedo = SpecularAlbedo.StorageHandle,
            SpecularHitDistance = SpecularHitDistance.StorageHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        foreach (Texture output in outputs)
        {
            commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Dispatch((RenderWidth + ThreadGroupSize - 1) / ThreadGroupSize, (RenderHeight + ThreadGroupSize - 1) / ThreadGroupSize, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        foreach (Texture output in outputs)
        {
            commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);
        }
    }

    protected override void ResizeImpl()
    {
        DestroyTextures();
        CreateTextures();
    }

    protected override void Destroy()
    {
        DestroyTextures();

        pipeline.Dispose();
        constantBuffer.Dispose();
    }

    private void CreateTextures()
    {
        Color = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);
        Depth = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R32Float, TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferSrc);
        Normal = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);
        MotionVectors = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16Float, TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferSrc);
        DiffuseAlbedo = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);
        SpecularAlbedo = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);
        SpecularHitDistance = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R32Float);

        outputs = [Color, Depth, Normal, MotionVectors, DiffuseAlbedo, SpecularAlbedo, SpecularHitDistance];
    }

    private void DestroyTextures()
    {
        foreach (Texture output in outputs)
        {
            output.Dispose();
        }
    }
}

[StructLayout(LayoutKind.Explicit, Size = 400)]
file struct Constants
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
    public Vector4 PositionFrame;

    [FieldOffset(272)]
    public Vector4 RenderSizeJitter;

    [FieldOffset(288)]
    public uint HitDistance;

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
    public ResourceHandle Color;

    [FieldOffset(352)]
    public ResourceHandle Depth;

    [FieldOffset(360)]
    public ResourceHandle Normal;

    [FieldOffset(368)]
    public ResourceHandle MotionVectors;

    [FieldOffset(376)]
    public ResourceHandle DiffuseAlbedo;

    [FieldOffset(384)]
    public ResourceHandle SpecularAlbedo;

    [FieldOffset(392)]
    public ResourceHandle SpecularHitDistance;
}
