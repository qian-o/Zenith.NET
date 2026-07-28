using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Handlers;
using FluidTank.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank;

internal enum FluidViewMode
{
    Water,
    Particles
}

internal unsafe class FluidTankRenderer : IDisposable
{
    private const double FixedSimulationStep = 1.0 / 30.0;

    private readonly FluidSimulation simulation;
    private readonly Buffer sceneVertexBuffer;
    private readonly Buffer sceneIndexBuffer;
    private readonly Buffer glassVertexBuffer;
    private readonly Buffer glassIndexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer sceneConstantBuffer;
    private readonly Buffer surfaceConstantBuffer;
    private readonly Buffer blurConstantBuffer;
    private readonly Buffer compositeConstantBuffer;
    private readonly Buffer reflectionConstantBuffer;
    private readonly Buffer glassConstantBuffer;
    private readonly Sampler linearSampler;

    private readonly GraphicsPipeline scenePipeline;
    private readonly GraphicsPipeline fluidDepthPipeline;
    private readonly GraphicsPipeline fluidThicknessPipeline;
    private readonly GraphicsPipeline particlePipeline;
    private readonly ComputePipeline blurPipeline;
    private readonly ComputePipeline blurThicknessPipeline;
    private readonly GraphicsPipeline compositePipeline;
    private readonly GraphicsPipeline glassPipeline;
    private readonly ComputePipeline? reflectionPipeline;

    private readonly BottomLevelAccelerationStructure? sceneBlas;
    private readonly TopLevelAccelerationStructure? sceneTlas;
    private readonly uint sceneIndexCount;
    private readonly uint glassIndexCount;

    private Texture sceneColor = null!;
    private Texture sceneLinearDepth = null!;
    private Texture fluidAttributes = null!;
    private Texture reconstructionDepth = null!;
    private Texture smoothDepthA = null!;
    private Texture smoothDepthB = null!;
    private Texture smoothThicknessA = null!;
    private Texture smoothThicknessB = null!;
    private Texture? reflection;

    private Matrix4x4 view;
    private Matrix4x4 projection;
    private Vector3 cameraPosition;
    private Vector3 cameraRight;
    private Vector3 cameraUp;
    private double totalTime;
    private double simulationTime;
    private double simulationAccumulator = FixedSimulationStep;
    private TimelineValue simulationReady;

    public FluidTankRenderer()
    {
        FluidTankGeometry.CreateScene(out SceneVertex[] sceneVertices, out uint[] sceneIndices, out SceneMaterial[] materials);
        FluidTankGeometry.CreateGlass(out SceneVertex[] glassVertices, out uint[] glassIndices);

        sceneIndexCount = (uint)sceneIndices.Length;
        glassIndexCount = (uint)glassIndices.Length;

        CommandBuffer uploadCommandBuffer = App.Context.TransferQueue.CommandBuffer();
        sceneVertexBuffer = GraphicsHelper.LoadBuffer(uploadCommandBuffer, sceneVertices, BufferUsages.Vertex | BufferUsages.StorageReadOnly);
        sceneIndexBuffer = GraphicsHelper.LoadBuffer(uploadCommandBuffer, sceneIndices, BufferUsages.Index | BufferUsages.StorageReadOnly);
        glassVertexBuffer = GraphicsHelper.LoadBuffer(uploadCommandBuffer, glassVertices, BufferUsages.Vertex);
        glassIndexBuffer = GraphicsHelper.LoadBuffer(uploadCommandBuffer, glassIndices, BufferUsages.Index);
        materialBuffer = GraphicsHelper.LoadBuffer(uploadCommandBuffer, materials, BufferUsages.StorageReadOnly);

        uploadCommandBuffer.Submit().Wait();

        sceneConstantBuffer = GraphicsHelper.CreateConstantBuffer<SceneConstants>();
        surfaceConstantBuffer = GraphicsHelper.CreateConstantBuffer<SurfaceConstants>();
        blurConstantBuffer = GraphicsHelper.CreateConstantBuffer(1024);
        compositeConstantBuffer = GraphicsHelper.CreateConstantBuffer<CompositeConstants>();
        reflectionConstantBuffer = GraphicsHelper.CreateConstantBuffer<ReflectionConstants>();
        glassConstantBuffer = GraphicsHelper.CreateConstantBuffer<GlassConstants>();
        linearSampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        Resize(App.Width, App.Height);

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        scenePipeline = GraphicsHelper.CreateGraphicsPipeline("Scene.slang", "VSMain", "FSMain", [inputLayout], new()
        {
            ColorFormats = [PixelFormat.R16G16B16A16Float, PixelFormat.R32Float],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullBack(), DepthStencilState.DepthReadWrite(), BlendState.Opaque());

        using Shader surfaceVertexShader = GraphicsHelper.LoadShader("FluidSurface.slang", "SurfaceVS");

        fluidDepthPipeline = GraphicsHelper.CreateGraphicsPipeline(surfaceVertexShader, "FluidSurface.slang", "DepthFS", [], new()
        {
            ColorFormats = [PixelFormat.R32Float, PixelFormat.R16G16B16A16Float],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthReadWrite(), BlendState.Opaque(), PrimitiveTopology.TriangleStrip);

        fluidThicknessPipeline = GraphicsHelper.CreateGraphicsPipeline(surfaceVertexShader, "FluidSurface.slang", "ThicknessFS", [], new()
        {
            ColorFormats = [PixelFormat.R16Float],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthRead(), AdditiveBlend(), PrimitiveTopology.TriangleStrip);

        particlePipeline = GraphicsHelper.CreateGraphicsPipeline(surfaceVertexShader, "FluidSurface.slang", "ParticleFS", [], new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthReadWrite(), BlendState.Opaque(), PrimitiveTopology.TriangleStrip);

        blurPipeline = GraphicsHelper.CreateComputePipeline("FluidBlur.slang", "BlurCS");
        blurThicknessPipeline = GraphicsHelper.CreateComputePipeline("FluidBlur.slang", "BlurThicknessCS");

        compositePipeline = GraphicsHelper.CreateGraphicsPipeline("FluidComposite.slang", "FullscreenVS", "CompositeFS", [], new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthNone(), BlendState.Opaque());

        glassPipeline = GraphicsHelper.CreateGraphicsPipeline("Glass.slang", "GlassVS", "GlassFS", [inputLayout], new()
        {
            ColorFormats = [PixelFormat.B8G8R8A8UNorm],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthRead(), BlendState.AlphaBlend());

        simulation = new();

        if (App.Context.Capabilities.RayTracingSupported)
        {
            CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

            sceneBlas = commandBuffer.BuildAccelerationStructure(new BottomLevelAccelerationStructureDesc
            {
                Geometries =
                [
                    new()
                    {
                        Type = RayTracingGeometryType.Triangle,
                        TriangleGeometry = new()
                        {
                            VertexBuffer = sceneVertexBuffer,
                            VertexFormat = PixelFormat.R32G32B32Float,
                            VertexCount = (uint)sceneVertices.Length,
                            VertexStrideInBytes = (uint)sizeof(SceneVertex),
                            IndexBuffer = sceneIndexBuffer,
                            IndexFormat = IndexFormat.UInt32,
                            IndexCount = sceneIndexCount,
                            Transform = Matrix4x4.Identity
                        },
                        IsOpaque = true
                    }
                ],
                BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
            });

            sceneTlas = commandBuffer.BuildAccelerationStructure(new TopLevelAccelerationStructureDesc
            {
                Instances =
                [
                    new()
                    {
                        AccelerationStructure = sceneBlas,
                        InstanceId = 0,
                        VisibilityMask = 0xFF,
                        Transform = Matrix4x4.Identity,
                        Flags = RayTracingInstanceFlags.None
                    }
                ],
                BuildFlags = AccelerationStructureBuildFlags.PreferFastTrace
            });

            commandBuffer.Submit().Wait();

            reflectionPipeline = GraphicsHelper.CreateComputePipeline("FluidReflection.slang", "ReflectionCS");
        }
    }

    public Texture Color { get; private set; } = null!;

    public Texture DepthStencil { get; private set; } = null!;

    public bool RayTracingEnabled => reflectionPipeline is not null;

    public uint ParticleCount => simulation.ParticleCount;

    public bool Paused { get; set; }

    public bool WaveMakerEnabled
    {
        get => simulation.WaveMakerEnabled;
        set => simulation.WaveMakerEnabled = value;
    }

    public float WaveAmplitude
    {
        get => simulation.WaveAmplitude;
        set => simulation.WaveAmplitude = value;
    }

    public float WaveFrequency
    {
        get => simulation.WaveFrequency;
        set => simulation.WaveFrequency = value;
    }

    public float FlipRatio
    {
        get => simulation.FlipRatio;
        set => simulation.FlipRatio = value;
    }

    public float VelocityDamping
    {
        get => simulation.VelocityDamping;
        set => simulation.VelocityDamping = value;
    }

    public int PressureIterations
    {
        get => simulation.PressureIterations;
        set => simulation.PressureIterations = value;
    }

    public float Clarity { get; set; } = 1.05f;

    public float RefractionStrength { get; set; } = 0.82f;

    public FluidViewMode ViewMode { get; set; }

    public void Update(CameraHandler camera, double delta)
    {
        view = camera.View;
        projection = camera.Projection;
        cameraPosition = camera.Position;
        cameraRight = camera.Right;
        cameraUp = camera.Up;

        if (!Paused)
        {
            double frameTime = Math.Min(delta, FixedSimulationStep);
            totalTime += frameTime;
            simulationAccumulator = Math.Min(simulationAccumulator + frameTime, FixedSimulationStep * 2.0);
        }
    }

    public void PushFluid(Vector3 origin, Vector3 direction)
    {
        simulation.Push(origin, direction);
    }

    public void Reset()
    {
        simulation.Reset();
        Paused = false;
        totalTime = 0.0;
        simulationTime = 0.0;
        simulationAccumulator = FixedSimulationStep;
    }

    public TimelineValue Simulate()
    {
        if (Paused)
        {
            simulationReady = simulation.Step(simulationTime, FixedSimulationStep, true);
        }
        else if (simulationAccumulator >= FixedSimulationStep)
        {
            simulationTime += FixedSimulationStep;
            simulationAccumulator -= FixedSimulationStep;
            simulationReady = simulation.Step(simulationTime, FixedSimulationStep, false);
        }

        UploadFrameConstants();

        return simulationReady;
    }

    public void RenderScene(CommandBuffer commandBuffer)
    {
        commandBuffer.Transition(sceneColor, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.Transition(sceneLinearDepth, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.Transition(DepthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);

        commandBuffer.BeginRenderPass(
        [
            ColorAttachment.Clear(sceneColor, new(0.0f, 0.0f, 0.0f, 1.0f)),
            ColorAttachment.Clear(sceneLinearDepth, Vector4.Zero)
        ], DepthStencilAttachment.Clear(DepthStencil, 1.0f, 0));
        commandBuffer.SetPipeline(scenePipeline);
        commandBuffer.SetVertexBuffer(sceneVertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(sceneIndexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(sceneConstantBuffer, 0);
        commandBuffer.DrawIndexed(sceneIndexCount, 1, 0, 0, 0);
        commandBuffer.EndRenderPass();

        commandBuffer.Transition(sceneColor, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(sceneLinearDepth, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void RenderFluid(CommandBuffer commandBuffer)
    {
        if (ViewMode is FluidViewMode.Particles)
        {
            RenderParticles(commandBuffer);

            return;
        }

        commandBuffer.Transition(reconstructionDepth, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);
        commandBuffer.Transition(smoothThicknessA, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Clear(smoothThicknessA, Vector4.Zero)], DepthStencilAttachment.Clear(reconstructionDepth, 1.0f, 0));
        commandBuffer.SetPipeline(fluidThicknessPipeline);
        commandBuffer.SetConstantBuffer(surfaceConstantBuffer, 0);
        commandBuffer.Draw(4, simulation.ParticleCount, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(smoothThicknessA, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);

        commandBuffer.Transition(smoothDepthA, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.Transition(fluidAttributes, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        commandBuffer.BeginRenderPass(
        [
            ColorAttachment.Clear(smoothDepthA, Vector4.Zero),
            ColorAttachment.Clear(fluidAttributes, Vector4.Zero)
        ], DepthStencilAttachment.Clear(reconstructionDepth, 1.0f, 0));
        commandBuffer.SetPipeline(fluidDepthPipeline);
        commandBuffer.SetConstantBuffer(surfaceConstantBuffer, 0);
        commandBuffer.Draw(4, simulation.ParticleCount, 0, 0);
        commandBuffer.EndRenderPass();

        commandBuffer.Transition(smoothDepthA, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(fluidAttributes, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);

        uint reconstructionWidth = smoothDepthA.Desc.Width;
        uint reconstructionHeight = smoothDepthA.Desc.Height;

        commandBuffer.Transition(smoothDepthB, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(blurPipeline);
        commandBuffer.SetConstantBuffer(blurConstantBuffer, 0);
        GraphicsHelper.Dispatch(commandBuffer, blurPipeline, reconstructionWidth, reconstructionHeight);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(smoothDepthB, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(smoothDepthA, default, TextureLayout.Sampled, TextureLayout.Storage);
        commandBuffer.SetConstantBuffer(blurConstantBuffer, 256);
        GraphicsHelper.Dispatch(commandBuffer, blurPipeline, reconstructionWidth, reconstructionHeight);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(smoothDepthA, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(smoothThicknessB, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(blurThicknessPipeline);
        commandBuffer.SetConstantBuffer(blurConstantBuffer, 512);
        GraphicsHelper.Dispatch(commandBuffer, blurThicknessPipeline, reconstructionWidth, reconstructionHeight);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(smoothThicknessB, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(smoothThicknessA, default, TextureLayout.Sampled, TextureLayout.Storage);
        commandBuffer.SetConstantBuffer(blurConstantBuffer, 768);
        GraphicsHelper.Dispatch(commandBuffer, blurThicknessPipeline, reconstructionWidth, reconstructionHeight);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.FragmentShading);
        commandBuffer.Transition(smoothThicknessA, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (reflectionPipeline is not null)
        {
            commandBuffer.Transition(reflection!, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.SetPipeline(reflectionPipeline);
            commandBuffer.SetConstantBuffer(reflectionConstantBuffer, 0);
            GraphicsHelper.Dispatch(commandBuffer, reflectionPipeline, reconstructionWidth, reconstructionHeight);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.FragmentShading);
            commandBuffer.Transition(reflection!, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(compositePipeline);
        commandBuffer.SetConstantBuffer(compositeConstantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();

        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.ColorAttachment);
        commandBuffer.Transition(DepthStencil, default, TextureLayout.DepthStencilAttachment, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Load(Color)], DepthStencilAttachment.Load(DepthStencil));
        DrawGlass(commandBuffer);
        commandBuffer.EndRenderPass();

        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Resize(uint width, uint height)
    {
        DisposeTargets();

        Color = GraphicsHelper.CreateTexture(PixelFormat.B8G8R8A8UNorm, width, height, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        DepthStencil = GraphicsHelper.CreateTexture(PixelFormat.D32FloatS8UInt, width, height, TextureUsages.DepthStencilAttachment);
        sceneColor = GraphicsHelper.CreateTexture(PixelFormat.R16G16B16A16Float, width, height, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        sceneLinearDepth = GraphicsHelper.CreateTexture(PixelFormat.R32Float, width, height, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        uint reconstructionWidth = Math.Max((width + 2) / 3, 1u);
        uint reconstructionHeight = Math.Max((height + 2) / 3, 1u);
        reconstructionDepth = GraphicsHelper.CreateTexture(PixelFormat.D32FloatS8UInt, reconstructionWidth, reconstructionHeight, TextureUsages.DepthStencilAttachment);
        fluidAttributes = GraphicsHelper.CreateTexture(PixelFormat.R16G16B16A16Float, reconstructionWidth, reconstructionHeight, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        smoothDepthA = GraphicsHelper.CreateTexture(PixelFormat.R32Float, reconstructionWidth, reconstructionHeight, TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment);
        smoothDepthB = GraphicsHelper.CreateTexture(PixelFormat.R32Float, reconstructionWidth, reconstructionHeight, TextureUsages.Sampled | TextureUsages.Storage);
        smoothThicknessA = GraphicsHelper.CreateTexture(PixelFormat.R16Float, reconstructionWidth, reconstructionHeight, TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment);
        smoothThicknessB = GraphicsHelper.CreateTexture(PixelFormat.R16Float, reconstructionWidth, reconstructionHeight, TextureUsages.Sampled | TextureUsages.Storage);
        reflection = App.Context.Capabilities.RayTracingSupported
            ? GraphicsHelper.CreateTexture(PixelFormat.R16G16B16A16Float, reconstructionWidth, reconstructionHeight, TextureUsages.Sampled | TextureUsages.Storage)
            : null;
    }

    public void Dispose()
    {
        simulation.Dispose();

        sceneTlas?.Dispose();
        sceneBlas?.Dispose();
        reflectionPipeline?.Dispose();
        glassPipeline.Dispose();
        compositePipeline.Dispose();
        blurThicknessPipeline.Dispose();
        blurPipeline.Dispose();
        particlePipeline.Dispose();
        fluidThicknessPipeline.Dispose();
        fluidDepthPipeline.Dispose();
        scenePipeline.Dispose();

        linearSampler.Dispose();
        glassConstantBuffer.Dispose();
        reflectionConstantBuffer.Dispose();
        compositeConstantBuffer.Dispose();
        blurConstantBuffer.Dispose();
        surfaceConstantBuffer.Dispose();
        sceneConstantBuffer.Dispose();
        materialBuffer.Dispose();
        glassIndexBuffer.Dispose();
        glassVertexBuffer.Dispose();
        sceneIndexBuffer.Dispose();
        sceneVertexBuffer.Dispose();

        DisposeTargets();
    }

    private void UploadFrameConstants()
    {
        Matrix4x4.Invert(view, out Matrix4x4 invView);
        Matrix4x4.Invert(projection, out Matrix4x4 invProjection);
        Vector3 sunDirection = Vector3.Normalize(new(-0.38f, -0.83f, -0.42f));
        float surfaceRadius = FluidSimulation.ParticleRadius * (ViewMode is FluidViewMode.Particles ? 0.78f : 1.28f);
        float interpolationAlpha = Paused ? 1.0f : (float)Math.Clamp(simulationAccumulator / FixedSimulationStep, 0.0, 1.0);

        SceneConstants scene = new()
        {
            View = view,
            Projection = projection,
            CameraPosition = cameraPosition,
            Time = (float)totalTime,
            LightDirection = sunDirection,
            LightIntensity = 1.55f,
            Materials = materialBuffer.StorageReadOnlyHandle
        };
        GraphicsHelper.Upload(sceneConstantBuffer, 0, &scene, (uint)sizeof(SceneConstants));

        SurfaceConstants surface = new()
        {
            View = view,
            Projection = projection,
            CameraRight = cameraRight,
            ParticleRadius = surfaceRadius,
            CameraUp = cameraUp,
            RestDensity = FluidSimulation.RestDensity,
            ParticleCount = simulation.ParticleCount,
            RenderMode = (uint)ViewMode,
            Width = ViewMode is FluidViewMode.Particles ? App.Width : smoothDepthA.Desc.Width,
            Height = ViewMode is FluidViewMode.Particles ? App.Height : smoothDepthA.Desc.Height,
            InterpolationAlpha = interpolationAlpha,
            Particles = simulation.ParticleHandle,
            PreviousPositions = simulation.PreviousPositionHandle,
            SceneDepth = sceneLinearDepth.SampledHandle,
            Sampler = linearSampler.Handle
        };
        GraphicsHelper.Upload(surfaceConstantBuffer, 0, &surface, (uint)sizeof(SurfaceConstants));

        uint reconstructionWidth = smoothDepthA.Desc.Width;
        uint reconstructionHeight = smoothDepthA.Desc.Height;

        CompositeConstants composite = new()
        {
            InvView = invView,
            InvProjection = invProjection,
            CameraPosition = cameraPosition,
            Time = (float)totalTime,
            SunDirection = sunDirection,
            Clarity = Clarity,
            WaterColor = new(0.018f, 0.24f, 0.34f),
            RefractionStrength = RefractionStrength,
            Absorption = new(0.54f, 0.14f, 0.052f),
            Ior = 1.333f,
            Width = reconstructionWidth,
            Height = reconstructionHeight,
            RenderMode = (uint)ViewMode,
            RayTracingEnabled = reflectionPipeline is not null ? 1u : 0u,
            SceneColor = sceneColor.SampledHandle,
            SceneDepth = sceneLinearDepth.SampledHandle,
            FluidDepth = smoothDepthA.SampledHandle,
            Thickness = smoothThicknessA.SampledHandle,
            Attributes = fluidAttributes.SampledHandle,
            Reflection = reflection?.SampledHandle ?? default,
            Sampler = linearSampler.Handle
        };
        GraphicsHelper.Upload(compositeConstantBuffer, 0, &composite, (uint)sizeof(CompositeConstants));

        if (ViewMode is FluidViewMode.Water)
        {
            BlurConstants horizontal = new()
            {
                Width = reconstructionWidth,
                Height = reconstructionHeight,
                Direction = 0,
                Radius = 18,
                SpatialSigma = 4.0f,
                DepthSigma = surfaceRadius * 1.4f,
                ProjectedRadiusScale = surfaceRadius * projection.M22 * reconstructionHeight * 0.5f,
                InputTexture = smoothDepthA.SampledHandle,
                OutputTexture = smoothDepthB.StorageHandle
            };
            GraphicsHelper.Upload(blurConstantBuffer, 0, &horizontal, (uint)sizeof(BlurConstants));

            BlurConstants vertical = horizontal;
            vertical.Direction = 1;
            vertical.InputTexture = smoothDepthB.SampledHandle;
            vertical.OutputTexture = smoothDepthA.StorageHandle;
            GraphicsHelper.Upload(blurConstantBuffer, 256, &vertical, (uint)sizeof(BlurConstants));

            BlurConstants horizontalThickness = horizontal;
            horizontalThickness.Radius = 12;
            horizontalThickness.SpatialSigma = 6.0f;
            horizontalThickness.InputTexture = smoothThicknessA.SampledHandle;
            horizontalThickness.OutputTexture = smoothThicknessB.StorageHandle;
            GraphicsHelper.Upload(blurConstantBuffer, 512, &horizontalThickness, (uint)sizeof(BlurConstants));

            BlurConstants verticalThickness = horizontalThickness;
            verticalThickness.Direction = 1;
            verticalThickness.InputTexture = smoothThicknessB.SampledHandle;
            verticalThickness.OutputTexture = smoothThicknessA.StorageHandle;
            GraphicsHelper.Upload(blurConstantBuffer, 768, &verticalThickness, (uint)sizeof(BlurConstants));

            if (reflectionPipeline is not null)
            {
                ReflectionConstants reflectionParameters = new()
                {
                    InvView = invView,
                    InvProjection = invProjection,
                    CameraPosition = cameraPosition,
                    Time = (float)totalTime,
                    SunDirection = sunDirection,
                    Width = reconstructionWidth,
                    Height = reconstructionHeight,
                    FluidDepth = smoothDepthA.SampledHandle,
                    Scene = sceneTlas!.Handle,
                    Vertices = sceneVertexBuffer.StorageReadOnlyHandle,
                    Indices = sceneIndexBuffer.StorageReadOnlyHandle,
                    Materials = materialBuffer.StorageReadOnlyHandle,
                    OutputTexture = reflection!.StorageHandle
                };
                GraphicsHelper.Upload(reflectionConstantBuffer, 0, &reflectionParameters, (uint)sizeof(ReflectionConstants));
            }
        }

        GlassConstants glass = new()
        {
            View = view,
            Projection = projection,
            CameraPosition = cameraPosition,
            Time = (float)totalTime
        };
        GraphicsHelper.Upload(glassConstantBuffer, 0, &glass, (uint)sizeof(GlassConstants));
    }

    private void RenderParticles(CommandBuffer commandBuffer)
    {
        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(compositePipeline);
        commandBuffer.SetConstantBuffer(compositeConstantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();

        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.ColorAttachment);
        commandBuffer.Transition(DepthStencil, default, TextureLayout.DepthStencilAttachment, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Load(Color)], DepthStencilAttachment.Load(DepthStencil));
        commandBuffer.SetPipeline(particlePipeline);
        commandBuffer.SetConstantBuffer(surfaceConstantBuffer, 0);
        commandBuffer.Draw(4, simulation.ParticleCount, 0, 0);
        DrawGlass(commandBuffer);
        commandBuffer.EndRenderPass();

        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    private void DrawGlass(CommandBuffer commandBuffer)
    {
        commandBuffer.SetPipeline(glassPipeline);
        commandBuffer.SetVertexBuffer(glassVertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(glassIndexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(glassConstantBuffer, 0);
        commandBuffer.DrawIndexed(glassIndexCount, 1, 0, 0, 0);
    }

    private void DisposeTargets()
    {
        reflection?.Dispose();
        reconstructionDepth?.Dispose();
        smoothThicknessB?.Dispose();
        smoothThicknessA?.Dispose();
        smoothDepthB?.Dispose();
        smoothDepthA?.Dispose();
        fluidAttributes?.Dispose();
        sceneLinearDepth?.Dispose();
        sceneColor?.Dispose();
        DepthStencil?.Dispose();
        Color?.Dispose();
    }

    private static BlendState AdditiveBlend()
    {
        return new()
        {
            ColorAttachment0 = new()
            {
                IsBlendingEnabled = true,
                SrcRgbFactor = BlendFactor.One,
                DstRgbFactor = BlendFactor.One,
                RgbOp = BlendOp.Add,
                SrcAlphaFactor = BlendFactor.One,
                DstAlphaFactor = BlendFactor.One,
                AlphaOp = BlendOp.Add,
                ColorWrites = ColorWrites.All
            }
        };
    }
}

[StructLayout(LayoutKind.Explicit, Size = 176)]
file struct SceneConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector3 CameraPosition;

    [FieldOffset(140)]
    public float Time;

    [FieldOffset(144)]
    public Vector3 LightDirection;

    [FieldOffset(156)]
    public float LightIntensity;

    [FieldOffset(160)]
    public ResourceHandle Materials;
}

[StructLayout(LayoutKind.Explicit, Size = 224)]
file struct SurfaceConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector3 CameraRight;

    [FieldOffset(140)]
    public float ParticleRadius;

    [FieldOffset(144)]
    public Vector3 CameraUp;

    [FieldOffset(156)]
    public float RestDensity;

    [FieldOffset(160)]
    public uint ParticleCount;

    [FieldOffset(164)]
    public uint RenderMode;

    [FieldOffset(168)]
    public uint Width;

    [FieldOffset(172)]
    public uint Height;

    [FieldOffset(176)]
    public float InterpolationAlpha;

    [FieldOffset(192)]
    public ResourceHandle Particles;

    [FieldOffset(200)]
    public ResourceHandle PreviousPositions;

    [FieldOffset(208)]
    public ResourceHandle SceneDepth;

    [FieldOffset(216)]
    public ResourceHandle Sampler;
}

[StructLayout(LayoutKind.Explicit, Size = 48)]
file struct BlurConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public uint Direction;

    [FieldOffset(12)]
    public uint Radius;

    [FieldOffset(16)]
    public float SpatialSigma;

    [FieldOffset(20)]
    public float DepthSigma;

    [FieldOffset(24)]
    public float ProjectedRadiusScale;

    [FieldOffset(32)]
    public ResourceHandle InputTexture;

    [FieldOffset(40)]
    public ResourceHandle OutputTexture;
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
    public float Time;

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
    public ResourceHandle Attributes;

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

    [FieldOffset(160)]
    public uint Width;

    [FieldOffset(164)]
    public uint Height;

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

[StructLayout(LayoutKind.Explicit, Size = 144)]
file struct GlassConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector3 CameraPosition;

    [FieldOffset(140)]
    public float Time;
}
