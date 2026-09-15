using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class WaterPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private Buffer compositeConstants = null!;
    private Buffer reflectionConstants = null!;
    private Sampler sampler = null!;
    private GraphicsPipeline compositePipeline = null!;
    private ComputePipeline? reflectionPipeline;
    private Texture? reflection;

    public Texture Color { get; private set; } = null!;

    protected override void Initialize()
    {
        compositeConstants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CompositeConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        reflectionConstants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(ReflectionConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        using Shader compositeVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("FluidComposite.slang"), "FullscreenVS"));
        using Shader compositeFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("FluidComposite.slang"), "CompositeFS"));

        compositePipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = compositeVertexShader,
            FragmentShader = compositeFragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R16G16B16A16Float],
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        if (App.Context.Capabilities.RayTracingSupported)
        {
            using Shader reflectionShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("FluidReflection.slang"), "ReflectionCS"));
            reflectionPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = reflectionShader });
        }

        ResizeImpl();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        bool rayTracing = args.RayTracingEnabled && reflectionPipeline is not null && args.ViewMode is FluidViewMode.Water;

        if (rayTracing)
        {
            RecordReflection(commandBuffer, in args);
        }

        RecordComposite(commandBuffer, in args, rayTracing);
    }

    protected override void ResizeImpl()
    {
        if (Color is null || Color.Desc.Width != DisplayWidth || Color.Desc.Height != DisplayHeight)
        {
            Color?.Dispose();
            Color = CreateTexture(DisplayWidth, DisplayHeight, PixelFormat.R16G16B16A16Float, TextureUsages.ColorAttachment | TextureUsages.Sampled);
        }

        if (reflectionPipeline is not null && (reflection is null || reflection.Desc.Width != RenderWidth || reflection.Desc.Height != RenderHeight))
        {
            reflection?.Dispose();
            reflection = CreateTexture(RenderWidth, RenderHeight, PixelFormat.R16G16B16A16Float);
        }
    }

    protected override void Destroy()
    {
        reflection?.Dispose();
        Color?.Dispose();
        reflectionPipeline?.Dispose();
        compositePipeline.Dispose();
        sampler.Dispose();
        reflectionConstants.Dispose();
        compositeConstants.Dispose();
    }

    private void RecordReflection(CommandBuffer commandBuffer, in PassArgs args)
    {
        ReflectionConstants reflectionData = new()
        {
            InvView = args.InverseView,
            InvProjection = args.InverseProjection,
            CameraPosition = args.CameraPosition,
            Time = args.Time,
            SunDirection = args.SunDirection,
            LightIntensity = args.LightIntensity,
            Width = RenderWidth,
            Height = RenderHeight,
            FluidDepth = args.FluidDepth.SampledHandle,
            Normal = args.Normal.SampledHandle,
            Scene = args.Scene.Scene!.Handle,
            Vertices = args.Scene.Vertices.StorageReadOnlyHandle,
            Indices = args.Scene.Indices.StorageReadOnlyHandle,
            Materials = args.Scene.Materials.StorageReadOnlyHandle,
            OutputTexture = reflection!.StorageHandle
        };

        reflectionConstants.Upload(0, new()
        {
            Pointer = (nint)(&reflectionData),
            SizeInBytes = (uint)sizeof(ReflectionConstants)
        });

        commandBuffer.Transition(reflection, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(reflectionPipeline!);
        commandBuffer.SetConstantBuffer(reflectionConstants, 0);
        ThreadGroupSize group = reflectionPipeline!.Desc.ComputeShader.Desc.ThreadGroupSize;
        commandBuffer.Dispatch((RenderWidth + group.X - 1) / group.X, (RenderHeight + group.Y - 1) / group.Y, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.FragmentShading);
        commandBuffer.Transition(reflection, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    private void RecordComposite(CommandBuffer commandBuffer, in PassArgs args, bool rayTracing)
    {
        CompositeConstants compositeData = new()
        {
            InvView = args.InverseView,
            InvProjection = args.InverseProjection,
            CameraPosition = args.CameraPosition,
            LightIntensity = args.LightIntensity,
            SunDirection = args.SunDirection,
            Clarity = 0.25f,
            WaterColor = new(0.025f, 0.12f, 0.16f),
            RefractionStrength = 0.45f,
            Absorption = new(0.18f, 0.045f, 0.015f),
            Ior = 1.333f,
            Width = RenderWidth,
            Height = RenderHeight,
            RenderMode = (uint)args.ViewMode,
            RayTracingEnabled = rayTracing ? 1u : 0u,
            SceneColor = args.Color.SampledHandle,
            SceneDepth = args.SceneDepth.SampledHandle,
            FluidDepth = args.FluidDepth.SampledHandle,
            Thickness = args.Thickness.SampledHandle,
            Normal = args.Normal.SampledHandle,
            Reflection = reflection?.SampledHandle ?? default,
            Sampler = sampler.Handle
        };

        compositeConstants.Upload(0, new()
        {
            Pointer = (nint)(&compositeData),
            SizeInBytes = (uint)sizeof(CompositeConstants)
        });

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(compositePipeline);
        commandBuffer.SetConstantBuffer(compositeConstants, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 272)]
file struct CompositeConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InvView;

    [FieldOffset(64)]
    public Matrix4x4 InvProjection;

    [FieldOffset(128)]
    public Vector3 CameraPosition;

    [FieldOffset(140)]
    public float LightIntensity;

    [FieldOffset(144)]
    public Vector3 SunDirection;

    [FieldOffset(156)]
    public float Clarity;

    [FieldOffset(160)]
    public Vector3 WaterColor;

    [FieldOffset(172)]
    public float RefractionStrength;

    [FieldOffset(176)]
    public Vector3 Absorption;

    [FieldOffset(188)]
    public float Ior;

    [FieldOffset(192)]
    public uint Width;

    [FieldOffset(196)]
    public uint Height;

    [FieldOffset(200)]
    public uint RenderMode;

    [FieldOffset(204)]
    public uint RayTracingEnabled;

    [FieldOffset(208)]
    public ResourceHandle SceneColor;

    [FieldOffset(216)]
    public ResourceHandle SceneDepth;

    [FieldOffset(224)]
    public ResourceHandle FluidDepth;

    [FieldOffset(232)]
    public ResourceHandle Thickness;

    [FieldOffset(240)]
    public ResourceHandle Normal;

    [FieldOffset(248)]
    public ResourceHandle Reflection;

    [FieldOffset(256)]
    public ResourceHandle Sampler;
}

[StructLayout(LayoutKind.Explicit, Size = 224)]
file struct ReflectionConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InvView;

    [FieldOffset(64)]
    public Matrix4x4 InvProjection;

    [FieldOffset(128)]
    public Vector3 CameraPosition;

    [FieldOffset(140)]
    public float Time;

    [FieldOffset(144)]
    public Vector3 SunDirection;

    [FieldOffset(156)]
    public float LightIntensity;

    [FieldOffset(160)]
    public uint Width;

    [FieldOffset(164)]
    public uint Height;

    [FieldOffset(168)]
    public ResourceHandle Normal;

    [FieldOffset(176)]
    public ResourceHandle FluidDepth;

    [FieldOffset(184)]
    public ResourceHandle Scene;

    [FieldOffset(192)]
    public ResourceHandle Vertices;

    [FieldOffset(200)]
    public ResourceHandle Indices;

    [FieldOffset(208)]
    public ResourceHandle Materials;

    [FieldOffset(216)]
    public ResourceHandle OutputTexture;
}
