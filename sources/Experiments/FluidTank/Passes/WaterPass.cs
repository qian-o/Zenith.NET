using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Helpers;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class WaterPass : IDisposable
{
    private readonly GraphicsContext context;

    private readonly Buffer compositeConstants;

    private readonly Buffer reflectionConstants;

    private readonly Sampler sampler;

    private readonly GraphicsPipeline compositePipeline;

    private readonly ComputePipeline? reflectionPipeline;

    private Texture? reflection;

    public WaterPass(GraphicsContext context)
    {
        this.context = context;

        compositeConstants = GraphicsHelper.CreateConstantBuffer<CompositeConstants>(context);
        reflectionConstants = GraphicsHelper.CreateConstantBuffer<ReflectionConstants>(context);
        sampler = context.CreateSampler(SamplerDesc.LinearClamp());

        compositePipeline = GraphicsHelper.CreateGraphicsPipeline(context, "FluidComposite.slang", "FullscreenVS", "CompositeFS", [], new()
        {
            ColorFormats = [PixelFormat.R16G16B16A16Float],
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthNone(), BlendState.Opaque());

        if (context.Capabilities.RayTracingSupported)
        {
            reflectionPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidReflection.slang", "ReflectionCS");
        }
    }

    public Texture Color { get; private set; } = null!;

    public void Resize(uint width, uint height, uint surfaceWidth, uint surfaceHeight)
    {
        if (Color is null || Color.Desc.Width != width || Color.Desc.Height != height)
        {
            Color?.Dispose();
            Color = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, width, height, TextureUsages.ColorAttachment | TextureUsages.Sampled);
        }

        if (reflectionPipeline is not null && (reflection is null || reflection.Desc.Width != surfaceWidth || reflection.Desc.Height != surfaceHeight))
        {
            reflection?.Dispose();
            reflection = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, surfaceWidth, surfaceHeight, TextureUsages.Storage | TextureUsages.Sampled);
        }
    }

    public void Render(CommandBuffer commandBuffer, FrameData frame, RenderSettings settings, SceneResources scene, Texture sceneColor, Texture sceneDepth, SurfaceData surface)
    {
        bool rayTracing = settings.RayTracingEnabled && reflectionPipeline is not null && settings.ViewMode is FluidViewMode.Water;

        if (rayTracing)
        {
            ReflectionConstants constants = new()
            {
                InvView = frame.InvView,
                InvProjection = frame.InvProjection,
                CameraPosition = frame.Position,
                Time = frame.Time,
                SunDirection = frame.SunDirection,
                LightIntensity = frame.LightIntensity,
                Width = surface.Depth.Desc.Width,
                Height = surface.Depth.Desc.Height,
                FluidDepth = surface.Depth.SampledHandle,
                Normal = surface.Normal.SampledHandle,
                Scene = scene.Scene!.Handle,
                Vertices = scene.Vertices.StorageReadOnlyHandle,
                Indices = scene.Indices.StorageReadOnlyHandle,
                Materials = scene.Materials.StorageReadOnlyHandle,
                OutputTexture = reflection!.StorageHandle
            };
            GraphicsHelper.Upload(reflectionConstants, 0, &constants, (uint)sizeof(ReflectionConstants));

            commandBuffer.Transition(reflection, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.SetPipeline(reflectionPipeline!);
            commandBuffer.SetConstantBuffer(reflectionConstants, 0);
            GraphicsHelper.Dispatch(commandBuffer, reflectionPipeline!, reflection.Desc.Width, reflection.Desc.Height);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.FragmentShading);
            commandBuffer.Transition(reflection, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        CompositeConstants composite = new()
        {
            InvView = frame.InvView,
            InvProjection = frame.InvProjection,
            CameraPosition = frame.Position,
            LightIntensity = frame.LightIntensity,
            SunDirection = frame.SunDirection,
            Clarity = settings.Clarity,
            WaterColor = new(0.025f, 0.12f, 0.16f),
            RefractionStrength = settings.RefractionStrength,
            Absorption = new(0.18f, 0.045f, 0.015f),
            Ior = 1.333f,
            Width = surface.Depth.Desc.Width,
            Height = surface.Depth.Desc.Height,
            RenderMode = (uint)settings.ViewMode,
            RayTracingEnabled = rayTracing ? 1u : 0u,
            SceneColor = sceneColor.SampledHandle,
            SceneDepth = sceneDepth.SampledHandle,
            FluidDepth = surface.Depth.SampledHandle,
            Thickness = surface.Thickness.SampledHandle,
            Normal = surface.Normal.SampledHandle,
            Reflection = reflection?.SampledHandle ?? default,
            Sampler = sampler.Handle
        };
        GraphicsHelper.Upload(compositeConstants, 0, &composite, (uint)sizeof(CompositeConstants));

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(compositePipeline);
        commandBuffer.SetConstantBuffer(compositeConstants, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Dispose()
    {
        reflection?.Dispose();
        Color?.Dispose();
        reflectionPipeline?.Dispose();
        compositePipeline.Dispose();
        sampler.Dispose();
        reflectionConstants.Dispose();
        compositeConstants.Dispose();
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
