using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Handlers;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class WaterPass : DisposableObject
{
    private readonly Buffer compositeConstants;
    private readonly Sampler sampler;
    private readonly GraphicsPipeline compositePipeline;
    private readonly Buffer? reflectionConstants;
    private readonly ComputePipeline? reflectionPipeline;

    private Texture? reflection;

    public WaterPass(uint width, uint height)
    {
        compositeConstants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CompositeConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "FluidComposite.slang");
        using Shader compositeVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FullscreenVS"));
        using Shader compositeFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, App.Context.Capabilities.RayTracingSupported ? "CompositeFS" : "EnvironmentFS"));

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
            reflectionConstants = App.Context.CreateBuffer(new()
            {
                SizeInBytes = (uint)sizeof(ReflectionConstants),
                Usages = BufferUsages.Constant,
                Residency = MemoryResidency.CpuWriteOnly
            });

            using Shader reflectionShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "FluidReflection.slang"), "ReflectionCS"));
            reflectionPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = reflectionShader });
        }

        CreateTexture(width, height);
    }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, CameraHandler camera, Texture sceneColor, Texture sceneDepth, Texture fluidDepth, Texture thickness, Texture normal)
    {
        Matrix4x4.Invert(camera.View, out Matrix4x4 inverseView);
        Matrix4x4.Invert(camera.Projection, out Matrix4x4 inverseProjection);

        if (reflectionPipeline is not null)
        {
            uint fluidWidth = fluidDepth.Desc.Width;
            uint fluidHeight = fluidDepth.Desc.Height;

            reflection ??= App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, fluidWidth, fluidHeight, 1, SampleCount.Count1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });

            ReflectionConstants reflectionData = new()
            {
                InvView = inverseView,
                InvProjection = inverseProjection,
                CameraPosition = camera.Position,
                SunDirection = SceneResources.SunDirection,
                LightIntensity = SceneResources.LightIntensity,
                Width = fluidWidth,
                Height = fluidHeight,
                Normal = normal.SampledHandle,
                FluidDepth = fluidDepth.SampledHandle,
                Scene = scene.Scene!.Handle,
                Vertices = scene.Vertices.StorageReadOnlyHandle,
                Indices = scene.Indices.StorageReadOnlyHandle,
                Materials = scene.Materials.StorageReadOnlyHandle,
                OutputTexture = reflection.StorageHandle
            };

            reflectionConstants!.Upload(0, new()
            {
                Pointer = (nint)(&reflectionData),
                SizeInBytes = (uint)sizeof(ReflectionConstants)
            });

            commandBuffer.Transition(reflection, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.SetPipeline(reflectionPipeline);
            commandBuffer.SetConstantBuffer(reflectionConstants, 0);
            ThreadGroupSize group = reflectionPipeline.Desc.ComputeShader.Desc.ThreadGroupSize;
            commandBuffer.Dispatch((fluidWidth + group.X - 1) / group.X, (fluidHeight + group.Y - 1) / group.Y, 1);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.FragmentShading);
            commandBuffer.Transition(reflection, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        CompositeConstants compositeData = new()
        {
            InvView = inverseView,
            InvProjection = inverseProjection,
            CameraPosition = camera.Position,
            LightIntensity = SceneResources.LightIntensity,
            SunDirection = SceneResources.SunDirection,
            Clarity = 0.25f,
            WaterColor = new(0.025f, 0.12f, 0.16f),
            RefractionStrength = 0.45f,
            Absorption = new(0.18f, 0.045f, 0.015f),
            Ior = 1.333f,
            Width = fluidDepth.Desc.Width,
            Height = fluidDepth.Desc.Height,
            SceneColor = sceneColor.SampledHandle,
            SceneDepth = sceneDepth.SampledHandle,
            FluidDepth = fluidDepth.SampledHandle,
            Thickness = thickness.SampledHandle,
            Normal = normal.SampledHandle,
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

    public void Resize(uint width, uint height)
    {
        DestroyTexture();
        CreateTexture(width, height);
    }

    protected override void Destroy()
    {
        DestroyTexture();
        reflectionPipeline?.Dispose();
        reflectionConstants?.Dispose();
        compositePipeline?.Dispose();
        sampler?.Dispose();
        compositeConstants?.Dispose();
    }

    private void CreateTexture(uint width, uint height)
    {
        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with { Usages = TextureUsages.ColorAttachment | TextureUsages.Sampled });
    }

    private void DestroyTexture()
    {
        reflection?.Dispose();
        reflection = null;
        Color?.Dispose();
        Color = null!;
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
