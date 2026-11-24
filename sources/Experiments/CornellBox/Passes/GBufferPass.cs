using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Upscaling;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class GBufferPass : DisposableObject
{
    private const int MaxBatchCount = 64;

    private readonly Buffer constantBuffer;
    private readonly GraphicsPipeline skyMotionPipeline;
    private readonly GraphicsPipeline skyMotionDepthPipeline;
    private readonly GraphicsPipeline meshPipeline;
    private readonly GraphicsPipeline meshMotionDepthPipeline;
    private readonly GraphicsPipeline staticDepthPipeline;
    private readonly GraphicsPipeline dynamicMotionDepthPipeline;

    private uint outputWidth;
    private uint outputHeight;
    private bool hasValidStaticDepth;
    private Texture depthStencil = null!;
    private Texture? staticOutputDepth;
    private Texture? outputDepth;
    private Texture? outputMotionVectors;
    private Texture? outputDepthStencil;

    public GBufferPass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(GBufferConstants) * MaxBatchCount),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "GBuffer.slang");

        using Shader skyVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "SkyVS"));
        using Shader skyFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "SkyMotionFS"));
        using Shader skyMotionDepthFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "SkyMotionDepthFS"));

        AttachmentFormats attachmentFormats = new()
        {
            ColorFormats = [PixelFormat.R8G8B8A8UNorm, PixelFormat.R16G16B16A16Float, PixelFormat.R32Float, PixelFormat.R16G16Float],
            DepthStencilFormat = PixelFormat.D32Float,
            SampleCount = SampleCount.Count1
        };

        AttachmentFormats motionDepthAttachmentFormats = new()
        {
            ColorFormats = [PixelFormat.R32Float, PixelFormat.R16G16Float],
            DepthStencilFormat = PixelFormat.D32Float,
            SampleCount = SampleCount.Count1
        };

        AttachmentFormats staticDepthAttachmentFormats = new()
        {
            ColorFormats = [PixelFormat.R32Float],
            DepthStencilFormat = PixelFormat.D32Float,
            SampleCount = SampleCount.Count1
        };

        DepthStencilState skyDepthState = DepthStencilState.DepthRead() with { DepthCompareOp = CompareOp.Equal };

        skyMotionPipeline = CreatePassPipeline(skyVertexShader, skyFragmentShader, [], attachmentFormats, skyDepthState);
        skyMotionDepthPipeline = CreatePassPipeline(skyVertexShader, skyMotionDepthFragmentShader, [], motionDepthAttachmentFormats, skyDepthState);

        InputLayout positionInputLayout = new();
        positionInputLayout.Add(new()
        {
            Format = ElementFormat.Float4,
            Semantic = ElementSemantic.Position
        });
        positionInputLayout.StrideInBytes = (uint)sizeof(SceneVertex);

        InputLayout inputLayout = new();
        inputLayout.Add(new()
        {
            Format = ElementFormat.Float4,
            Semantic = ElementSemantic.Position
        });
        inputLayout.Add(new()
        {
            Format = ElementFormat.Float4,
            Semantic = ElementSemantic.Normal
        });

        using Shader meshVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "VSMain"));
        using Shader meshFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FSMain"));
        using Shader meshMotionDepthVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "MotionDepthVS"));
        using Shader meshMotionDepthFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "MotionDepthFS"));
        using Shader staticDepthFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "StaticDepthFS"));
        using Shader dynamicMotionDepthFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "DynamicMotionDepthFS"));

        meshPipeline = CreatePassPipeline(meshVertexShader, meshFragmentShader, [inputLayout], attachmentFormats, DepthStencilState.DepthReadWrite());
        meshMotionDepthPipeline = CreatePassPipeline(meshMotionDepthVertexShader, meshMotionDepthFragmentShader, [positionInputLayout], motionDepthAttachmentFormats, DepthStencilState.DepthReadWrite());
        staticDepthPipeline = CreatePassPipeline(meshMotionDepthVertexShader, staticDepthFragmentShader, [positionInputLayout], staticDepthAttachmentFormats, DepthStencilState.DepthReadWrite());
        dynamicMotionDepthPipeline = CreatePassPipeline(meshMotionDepthVertexShader, dynamicMotionDepthFragmentShader, [positionInputLayout], motionDepthAttachmentFormats, DepthStencilState.DepthReadWrite());
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public bool HasOutputMotionDepth => outputDepth is not null;

    public Texture AlbedoAndMaterial { get; private set; } = null!;

    public Texture NormalAndRoughness { get; private set; } = null!;

    public Texture Depth { get; private set; } = null!;

    public Texture MotionVectors { get; private set; } = null!;

    public Texture FrameGenerationDepth => outputDepth ?? Depth;

    public Texture FrameGenerationMotionVectors => outputMotionVectors ?? MotionVectors;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, in FrameContext frame)
    {
        ReadOnlySpan<DrawBatch> batches = scene.DrawBatches;
        uint strideInBytes = (uint)sizeof(GBufferConstants);
        GBufferConstants* slots = stackalloc GBufferConstants[batches.Length];

        GBufferConstants baseConstants = new()
        {
            JitteredViewProjection = frame.JitteredViewProjection,
            ViewProjection = frame.ViewProjection,
            PreviousViewProjection = frame.PreviousViewProjection,
            InverseView = frame.InverseView,
            InverseJitteredProjection = frame.InverseJitteredProjection,
            InverseProjection = frame.InverseProjection,
            CameraPosition = frame.CameraPosition,
            ElapsedSeconds = frame.Environment.ElapsedSeconds,
            Materials = scene.Materials,
            Instances = scene.Instances,
            StaticDepth = staticOutputDepth?.SampledHandle ?? default
        };

        for (int i = 0; i < batches.Length; i++)
        {
            slots[i] = baseConstants with { BaseInstance = batches[i].BaseInstance };
        }

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)slots,
            SizeInBytes = (uint)(batches.Length * strideInBytes)
        });

        commandBuffer.Transition(AlbedoAndMaterial, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.Transition(NormalAndRoughness, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.Transition(Depth, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.Transition(MotionVectors, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.Transition(depthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);

        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(AlbedoAndMaterial), ColorAttachment.DontCare(NormalAndRoughness), ColorAttachment.DontCare(Depth), ColorAttachment.DontCare(MotionVectors)], DepthStencilAttachment.Clear(depthStencil, 1.0f, 0));

        DrawMeshBatches(commandBuffer, scene, batches, meshPipeline, isStatic: null);
        commandBuffer.SetPipeline(skyMotionPipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);

        commandBuffer.EndRenderPass();

        commandBuffer.Transition(AlbedoAndMaterial, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(NormalAndRoughness, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(Depth, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(MotionVectors, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);

        if (staticOutputDepth is not null && outputDepth is not null && outputMotionVectors is not null && outputDepthStencil is not null)
        {
            if (frame.Reset || frame.ViewProjection != frame.PreviousViewProjection)
            {
                hasValidStaticDepth = false;
            }

            if (frame.ViewProjection == frame.PreviousViewProjection)
            {
                bool hasRenderedStaticDepth = false;

                if (!hasValidStaticDepth)
                {
                    commandBuffer.Transition(staticOutputDepth, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
                    commandBuffer.Transition(outputDepthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);

                    commandBuffer.BeginRenderPass([ColorAttachment.Clear(staticOutputDepth, Vector4.One)], DepthStencilAttachment.Clear(outputDepthStencil, 1.0f, 0));
                    DrawMeshBatches(commandBuffer, scene, batches, staticDepthPipeline, isStatic: true);
                    commandBuffer.EndRenderPass();

                    commandBuffer.Transition(staticOutputDepth, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
                    hasValidStaticDepth = true;
                    hasRenderedStaticDepth = true;
                }

                commandBuffer.Transition(staticOutputDepth, default, TextureLayout.Sampled, TextureLayout.CopySrc);
                commandBuffer.Transition(outputDepth, default, TextureLayout.Undefined, TextureLayout.CopyDst);
                commandBuffer.CopyTexture(staticOutputDepth, default, default, outputDepth, default, default, new()
                {
                    Width = outputWidth,
                    Height = outputHeight,
                    Depth = 1
                });
                commandBuffer.Transition(staticOutputDepth, default, TextureLayout.CopySrc, TextureLayout.Sampled);
                commandBuffer.Transition(outputDepth, default, TextureLayout.CopyDst, TextureLayout.ColorAttachment);
                commandBuffer.Transition(outputMotionVectors, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

                if (hasRenderedStaticDepth)
                {
                    commandBuffer.Barrier(BarrierStages.DepthStencilAttachment, BarrierStages.DepthStencilAttachment);
                }
                else
                {
                    commandBuffer.Transition(outputDepthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);
                }

                commandBuffer.BeginRenderPass([ColorAttachment.Load(outputDepth), ColorAttachment.Clear(outputMotionVectors, Vector4.Zero)], DepthStencilAttachment.Clear(outputDepthStencil, 1.0f, 0));

                DrawMeshBatches(commandBuffer, scene, batches, dynamicMotionDepthPipeline, isStatic: false);

                commandBuffer.EndRenderPass();
            }
            else
            {
                commandBuffer.Transition(outputDepth, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
                commandBuffer.Transition(outputMotionVectors, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
                commandBuffer.Transition(outputDepthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);

                commandBuffer.BeginRenderPass([ColorAttachment.Clear(outputDepth, Vector4.One), ColorAttachment.Clear(outputMotionVectors, Vector4.Zero)], DepthStencilAttachment.Clear(outputDepthStencil, 1.0f, 0));

                DrawMeshBatches(commandBuffer, scene, batches, meshMotionDepthPipeline, isStatic: null);

                commandBuffer.SetPipeline(skyMotionDepthPipeline);
                commandBuffer.SetConstantBuffer(constantBuffer, 0);
                commandBuffer.Draw(3, 1, 0, 0);

                commandBuffer.EndRenderPass();
            }

            commandBuffer.Transition(outputDepth, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
            commandBuffer.Transition(outputMotionVectors, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        }
        else
        {
            hasValidStaticDepth = false;
        }
    }

    public void Resize(uint width, uint height, uint targetOutputWidth, uint targetOutputHeight, bool hasOutputMotionDepth)
    {
        bool useOutputMotionDepth = hasOutputMotionDepth && (width != targetOutputWidth || height != targetOutputHeight) && App.Context.UpscalingCapabilities.FrameGenerationSupported;
        if (Width == width && Height == height && outputWidth == targetOutputWidth && outputHeight == targetOutputHeight && HasOutputMotionDepth == useOutputMotionDepth)
        {
            return;
        }

        DestroyResources();
        CreateResources(width, height, targetOutputWidth, targetOutputHeight, useOutputMotionDepth);
    }

    protected override void Destroy()
    {
        DestroyResources();

        dynamicMotionDepthPipeline?.Dispose();
        staticDepthPipeline?.Dispose();
        meshMotionDepthPipeline?.Dispose();
        meshPipeline?.Dispose();
        skyMotionDepthPipeline?.Dispose();
        skyMotionPipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void DrawMeshBatches(CommandBuffer commandBuffer,
                                 SceneResources scene,
                                 ReadOnlySpan<DrawBatch> batches,
                                 GraphicsPipeline pipeline,
                                 bool? isStatic)
    {
        uint firstDynamic = scene.FirstDynamicInstance;
        uint strideInBytes = (uint)sizeof(GBufferConstants);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(scene.VertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(scene.IndexBuffer, 0, IndexFormat.UInt32);

        for (int i = 0; i < batches.Length; i++)
        {
            DrawBatch batch = batches[i];
            if (isStatic is bool targetStatic && (batch.BaseInstance < firstDynamic) != targetStatic)
            {
                continue;
            }

            commandBuffer.SetConstantBuffer(constantBuffer, (uint)(i * strideInBytes));
            commandBuffer.DrawIndexed(batch.IndexCount, batch.InstanceCount, batch.FirstIndex, 0, 0);
        }
    }

    private void CreateResources(uint width, uint height, uint targetOutputWidth, uint targetOutputHeight, bool useOutputMotionDepth)
    {
        Width = width;
        Height = height;
        outputWidth = targetOutputWidth;
        outputHeight = targetOutputHeight;
        hasValidStaticDepth = false;

        AlbedoAndMaterial = CreateTarget(PixelFormat.R8G8B8A8UNorm, width, height);
        NormalAndRoughness = CreateTarget(PixelFormat.R16G16B16A16Float, width, height);
        Depth = CreateTarget(PixelFormat.R32Float, width, height, TextureUsages.UpscalingInput);
        MotionVectors = CreateTarget(PixelFormat.R16G16Float, width, height, TextureUsages.UpscalingInput);
        depthStencil = CreateDepthStencil(width, height);

        if (useOutputMotionDepth)
        {
            staticOutputDepth = CreateTarget(PixelFormat.R32Float, targetOutputWidth, targetOutputHeight, TextureUsages.TransferSrc);
            outputDepth = CreateTarget(PixelFormat.R32Float, targetOutputWidth, targetOutputHeight, TextureUsages.TransferDst | TextureUsages.UpscalingInput);
            outputMotionVectors = CreateTarget(PixelFormat.R16G16Float, targetOutputWidth, targetOutputHeight, TextureUsages.UpscalingInput);
            outputDepthStencil = CreateDepthStencil(targetOutputWidth, targetOutputHeight);
        }
    }

    private void DestroyResources()
    {
        outputDepthStencil?.Dispose();
        outputDepthStencil = null;
        outputMotionVectors?.Dispose();
        outputMotionVectors = null;
        outputDepth?.Dispose();
        outputDepth = null;
        staticOutputDepth?.Dispose();
        staticOutputDepth = null;
        hasValidStaticDepth = false;

        depthStencil?.Dispose();
        depthStencil = null!;

        MotionVectors?.Dispose();
        MotionVectors = null!;
        Depth?.Dispose();
        Depth = null!;
        NormalAndRoughness?.Dispose();
        NormalAndRoughness = null!;
        AlbedoAndMaterial?.Dispose();
        AlbedoAndMaterial = null!;

        Width = 0;
        Height = 0;
        outputWidth = 0;
        outputHeight = 0;
    }

    private static GraphicsPipeline CreatePassPipeline(Shader vertexShader,
                                                       Shader fragmentShader,
                                                       InputLayout[] inputLayouts,
                                                       AttachmentFormats attachmentFormats,
                                                       DepthStencilState depthStencil)
    {
        return App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = inputLayouts,
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = attachmentFormats,
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = depthStencil,
                Blend = BlendState.Opaque()
            }
        });
    }

    private static Texture CreateTarget(PixelFormat format, uint width, uint height, TextureUsages extraUsages = TextureUsages.None)
    {
        TextureDesc desc = TextureDesc.ColorAttachment(format, width, height, 1, SampleCount.Count1);

        if (extraUsages is not TextureUsages.None)
        {
            desc = desc with { Usages = desc.Usages | extraUsages };
        }

        return App.Context.CreateTexture(desc);
    }

    private static Texture CreateDepthStencil(uint width, uint height)
    {
        return App.Context.CreateTexture(TextureDesc.DepthStencilAttachment(PixelFormat.D32Float, width, height, SampleCount.Count1));
    }
}

[StructLayout(LayoutKind.Explicit, Size = 512)]
file struct GBufferConstants
{
    [FieldOffset(0)]
    public Matrix4x4 JitteredViewProjection;

    [FieldOffset(64)]
    public Matrix4x4 ViewProjection;

    [FieldOffset(128)]
    public Matrix4x4 PreviousViewProjection;

    [FieldOffset(192)]
    public Matrix4x4 InverseView;

    [FieldOffset(256)]
    public Matrix4x4 InverseJitteredProjection;

    [FieldOffset(320)]
    public Matrix4x4 InverseProjection;

    [FieldOffset(384)]
    public Vector3 CameraPosition;

    [FieldOffset(396)]
    public uint BaseInstance;

    [FieldOffset(400)]
    public float ElapsedSeconds;

    [FieldOffset(416)]
    public ResourceHandle Materials;

    [FieldOffset(424)]
    public ResourceHandle Instances;

    [FieldOffset(432)]
    public ResourceHandle StaticDepth;
}
