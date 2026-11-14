using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling;

public unsafe partial class FrameGenerationSwapChain : DisposableObject
{
    private const int FrameCount = 3;
    private const double MinimumIntervalSeconds = 0.001;
    private const double MaximumIntervalSeconds = 0.1;
    private const double VerticalSyncMarginSeconds = 0.003;

    private readonly GraphicsContext context;
    private readonly ComputePipeline inputsPipeline;
    private readonly Shader vertexShader;
    private readonly Shader fragmentShader;
    private readonly Dictionary<PixelFormat, GraphicsPipeline> colorPipelines = [];
    private readonly SwapChain swapChain;
    private readonly Frame[] frames = new Frame[FrameCount];

    private FrameGeneration? frameGeneration;
    private CancellationTokenSource? tokenSource;
    private BlockingCollection<Submission>? submissions;
    private Thread? presenterThread;
    private int frameIndex;
    private bool hasHistory;
    private long lastSubmitTimestamp;
    private long lastPresentationTimestamp;
    private double realIntervalSeconds;

    internal FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc)
    {
        this.context = context;
        Desc = desc;

        using Shader computeShader = context.CreateShader(context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => DirectX12CaptureInputs,
            GraphicsApi.Metal => MetalCaptureInputs,
            GraphicsApi.Vulkan => VulkanCaptureInputs,
            _ => default
        });

        inputsPipeline = context.CreateComputePipeline(new() { ComputeShader = computeShader });

        vertexShader = context.CreateShader(context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => DirectX12FullscreenVS,
            GraphicsApi.Metal => MetalFullscreenVS,
            GraphicsApi.Vulkan => VulkanFullscreenVS,
            _ => default
        });

        fragmentShader = context.CreateShader(context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => DirectX12CaptureColorFS,
            GraphicsApi.Metal => MetalCaptureColorFS,
            GraphicsApi.Vulkan => VulkanCaptureColorFS,
            _ => default
        });

        swapChain = context.CreateSwapChain(new()
        {
            Surface = desc.Surface,
            Format = desc.Format,
            IsVerticalSyncEnabled = desc.IsVerticalSyncEnabled,
            IsPresentQueueEnabled = true
        });

        CreateResources();
    }

    public FrameGenerationSwapChainDesc Desc { get; private set; }

    public Texture Drawable => frames[frameIndex].Color;

    public void Present(FrameGenerationSwapChainArgs args)
    {
        Frame frame = frames[frameIndex];

        CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();
        commandBuffer.Barrier(BarrierStages.All, args.Hudless is not null ? BarrierStages.ComputeShading | BarrierStages.FragmentShading : BarrierStages.ComputeShading);
        Capture(frame, commandBuffer, args);

        TimelineValue rendered = commandBuffer.Submit();

        long now = Stopwatch.GetTimestamp();
        double intervalSeconds = lastSubmitTimestamp is 0 ? args.DeltaTimeInSeconds : Stopwatch.GetElapsedTime(lastSubmitTimestamp, now).TotalSeconds;
        lastSubmitTimestamp = now;
        intervalSeconds = Math.Clamp(intervalSeconds, MinimumIntervalSeconds, MaximumIntervalSeconds);
        realIntervalSeconds = realIntervalSeconds is 0.0 || args.Reset ? intervalSeconds : (realIntervalSeconds * 0.8) + (intervalSeconds * 0.2);

        frame.Ready.Reset();
        submissions!.Add(new(frame, args, rendered, realIntervalSeconds));

        frameIndex = (frameIndex + 1) % frames.Length;
        frames[frameIndex].Ready.Wait();
    }

    public void Resize(uint width, uint height)
    {
        Desc = Desc with
        {
            Surface = Desc.Surface with
            {
                Width = width,
                Height = height
            }
        };

        DestroyResources();
        swapChain.Resize(width, height);
        CreateResources();
    }

    public void Refresh(Surface surface)
    {
        Desc = Desc with { Surface = surface };

        DestroyResources();
        swapChain.Refresh(surface);
        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();

        swapChain.Dispose();

        foreach (GraphicsPipeline pipeline in colorPipelines.Values)
        {
            pipeline.Dispose();
        }
        colorPipelines.Clear();

        fragmentShader.Dispose();
        vertexShader.Dispose();
        inputsPipeline.Dispose();
    }

    private void CreateResources()
    {
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = new(context, Desc);
        }

        frameIndex = 0;
        hasHistory = false;
        lastSubmitTimestamp = 0;
        lastPresentationTimestamp = 0;
        realIntervalSeconds = 0.0;

        tokenSource = new();
        submissions = new(FrameCount);
        presenterThread = new(PresentLoop)
        {
            Name = nameof(FrameGenerationSwapChain),
            IsBackground = true
        };
        presenterThread.Start();
    }

    private void DestroyResources()
    {
        tokenSource?.Cancel();
        submissions?.CompleteAdding();
        presenterThread?.Join();

        submissions?.Dispose();
        submissions = null;
        tokenSource?.Dispose();
        tokenSource = null;
        presenterThread = null;

        context.GraphicsQueue.Timeline.Signal().Wait();
        swapChain.Queue.Timeline.Signal().Wait();

        frameGeneration?.Dispose();
        frameGeneration = null;

        for (int i = frames.Length - 1; i >= 0; i--)
        {
            frames[i].Dispose();
            frames[i] = null!;
        }
    }

    private void Capture(Frame frame, CommandBuffer commandBuffer, FrameGenerationSwapChainArgs args)
    {
        uint width = args.Depth.Desc.Width;
        uint height = args.Depth.Desc.Height;

        frame.Depth = EnsureTexture(context, frame.Depth, PixelFormat.R32Float, width, height, TextureUsages.Sampled | TextureUsages.Storage);
        frame.MotionVectors = EnsureTexture(context, frame.MotionVectors, PixelFormat.R32G32Float, width, height, TextureUsages.Sampled | TextureUsages.Storage);

        if (args.Hudless is Texture hudless)
        {
            frame.Hudless = EnsureTexture(context, frame.Hudless, hudless.Desc.Format, hudless.Desc.Width, hudless.Desc.Height, TextureUsages.Sampled | TextureUsages.ColorAttachment);
            frame.HasHudless = true;
        }
        else
        {
            frame.HasHudless = false;
        }

        CaptureConstants constants = new()
        {
            Width = width,
            Height = height,
            SrcColor = args.Hudless?.SampledHandle ?? default,
            SrcDepth = args.Depth.SampledHandle,
            SrcMotionVectors = args.MotionVectors.SampledHandle,
            DstDepth = frame.Depth.StorageHandle,
            DstMotionVectors = frame.MotionVectors.StorageHandle
        };

        commandBuffer.BeginDebugEvent("Frame Generation Capture");

        commandBuffer.Upload(frame.ConstantBuffer, 0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(CaptureConstants)
        });
        commandBuffer.Barrier(BarrierStages.Copy, frame.HasHudless ? BarrierStages.ComputeShading | BarrierStages.FragmentShading : BarrierStages.ComputeShading);

        commandBuffer.Transition(frame.Depth, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(frame.MotionVectors, default, TextureLayout.Undefined, TextureLayout.Storage);

        ThreadGroupSize group = inputsPipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(inputsPipeline);
        commandBuffer.SetConstantBuffer(frame.ConstantBuffer, 0);
        commandBuffer.Dispatch((width + group.X - 1) / group.X, (height + group.Y - 1) / group.Y, 1);

        commandBuffer.Transition(frame.Depth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(frame.MotionVectors, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (frame.HasHudless)
        {
            commandBuffer.Transition(frame.Hudless!, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            commandBuffer.BeginRenderPass([ColorAttachment.DontCare(frame.Hudless!)], null);
            commandBuffer.SetPipeline(GetColorPipeline(frame.Hudless!.Desc.Format));
            commandBuffer.SetConstantBuffer(frame.ConstantBuffer, 0);
            commandBuffer.Draw(3, 1, 0, 0);
            commandBuffer.EndRenderPass();
            commandBuffer.Transition(frame.Hudless!, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        }

        commandBuffer.EndDebugEvent();
    }

    private GraphicsPipeline GetColorPipeline(PixelFormat format)
    {
        if (!colorPipelines.TryGetValue(format, out GraphicsPipeline? pipeline))
        {
            colorPipelines[format] = pipeline = context.CreateGraphicsPipeline(new()
            {
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                InputLayouts = [],
                PrimitiveTopology = PrimitiveTopology.TriangleList,
                AttachmentFormats = new()
                {
                    ColorFormats = [format],
                    SampleCount = SampleCount.Count1
                },
                RenderState = new()
                {
                    Rasterizer = RasterizerState.CullNone(),
                    DepthStencil = DepthStencilState.DepthNone(),
                    Blend = BlendState.Opaque()
                }
            });
        }

        return pipeline;
    }

    private void PresentLoop()
    {
        CancellationToken cancellationToken = tokenSource!.Token;

        try
        {
            foreach (Submission submission in submissions!.GetConsumingEnumerable(cancellationToken))
            {
                PresentFrame(submission, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PresentFrame(Submission submission, CancellationToken cancellationToken)
    {
        Frame frame = submission.Frame;
        FrameGenerationSwapChainArgs args = submission.Args;

        FrameGeneration interpolator = EnsureFrameGeneration(frame.Color.Desc.Format, frame.Depth!.Desc.Width, frame.Depth.Desc.Height, frame.Color.Desc.Width, frame.Color.Desc.Height, out bool recreated);

        bool reset = !hasHistory || recreated || args.Reset;
        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

        commandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.Sampled);

        interpolator.Dispatch(commandBuffer, new()
        {
            Color = frame.Color,
            Depth = frame.Depth,
            MotionVectors = frame.MotionVectors!,
            Hudless = frame.HasHudless ? frame.Hudless : null,
            JitterOffset = args.JitterOffset,
            View = args.View,
            Projection = args.Projection,
            DeltaTimeInSeconds = args.DeltaTimeInSeconds,
            Reset = reset
        });

        if (!reset)
        {
            commandBuffer.Transition(frame.Color, default, TextureLayout.Sampled, TextureLayout.Present);
            commandBuffer.Transition(interpolator.Output, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            CopyToDrawable(commandBuffer, interpolator.Output);
            commandBuffer.Transition(interpolator.Output, default, TextureLayout.CopySrc, TextureLayout.Sampled);
            commandBuffer.Submit(submission.Rendered).Wait();

            double stepSeconds = (submission.RealIntervalSeconds * 0.5) - (swapChain.Desc.IsVerticalSyncEnabled ? VerticalSyncMarginSeconds : 0.0);

            if (lastPresentationTimestamp is not 0)
            {
                WaitUntil(lastPresentationTimestamp, stepSeconds, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                frame.Ready.Set();

                return;
            }

            swapChain.Present();
            lastPresentationTimestamp = Stopwatch.GetTimestamp();

            CommandBuffer realCommandBuffer = swapChain.Queue.CommandBuffer();
            realCommandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.CopySrc);
            CopyToDrawable(realCommandBuffer, frame.Color);
            realCommandBuffer.Transition(frame.Color, default, TextureLayout.CopySrc, TextureLayout.Present);
            realCommandBuffer.Submit().Wait();
            frame.Ready.Set();

            WaitUntil(lastPresentationTimestamp, stepSeconds, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            swapChain.Present();
            lastPresentationTimestamp = Stopwatch.GetTimestamp();
        }
        else
        {
            commandBuffer.Transition(frame.Color, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            CopyToDrawable(commandBuffer, frame.Color);
            commandBuffer.Transition(frame.Color, default, TextureLayout.CopySrc, TextureLayout.Present);
            commandBuffer.Submit(submission.Rendered).Wait();
            frame.Ready.Set();

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            swapChain.Present();
            lastPresentationTimestamp = Stopwatch.GetTimestamp();
        }

        hasHistory = true;
    }

    private FrameGeneration EnsureFrameGeneration(PixelFormat format, uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight, out bool recreated)
    {
        if (frameGeneration is not null && frameGeneration.Desc.Format == format && frameGeneration.Desc.RenderWidth == renderWidth && frameGeneration.Desc.RenderHeight == renderHeight && frameGeneration.Desc.OutputWidth == outputWidth && frameGeneration.Desc.OutputHeight == outputHeight)
        {
            recreated = false;

            return frameGeneration;
        }

        frameGeneration?.Dispose();
        frameGeneration = context.CreateFrameGeneration(new()
        {
            Format = format,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        });

        recreated = true;

        return frameGeneration;
    }

    private void CopyToDrawable(CommandBuffer commandBuffer, Texture texture)
    {
        Texture drawable = swapChain.Drawable;

        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(texture, default, default, drawable, default, default, new()
        {
            Width = texture.Desc.Width,
            Height = texture.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(drawable, default, TextureLayout.CopyDst, TextureLayout.Present);
    }

    private static Texture EnsureTexture(GraphicsContext context, Texture? target, PixelFormat format, uint width, uint height, TextureUsages usages)
    {
        if (target is not null && target.Desc.Format == format && target.Desc.Width == width && target.Desc.Height == height)
        {
            return target;
        }

        target?.Dispose();

        return context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with { Usages = usages });
    }

    private static void WaitUntil(long startTimestamp, double targetSeconds, CancellationToken cancellationToken)
    {
        double remainingSeconds = targetSeconds - Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        if (remainingSeconds <= 0.0)
        {
            return;
        }

        int sleepMilliseconds = (int)((remainingSeconds - 0.002) * 1000.0);
        if (sleepMilliseconds > 0 && cancellationToken.WaitHandle.WaitOne(sleepMilliseconds))
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested && Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds < targetSeconds)
        {
            Thread.SpinWait(64);
        }
    }

    private class Frame(GraphicsContext context, FrameGenerationSwapChainDesc desc) : DisposableObject
    {
        public readonly Buffer ConstantBuffer = context.CreateBuffer(BufferDesc.Constant(256));

        public readonly ManualResetEventSlim Ready = new(true);

        public readonly Texture Color = context.CreateTexture(TextureDesc.Texture2D(desc.Format, desc.Surface.Width, desc.Surface.Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment | TextureUsages.TransferSrc | TextureUsages.TransferDst
        });

        public Texture? Depth;

        public Texture? MotionVectors;

        public Texture? Hudless;

        public bool HasHudless;

        protected override void Destroy()
        {
            Hudless?.Dispose();
            MotionVectors?.Dispose();
            Depth?.Dispose();
            Color.Dispose();
            Ready.Dispose();
            ConstantBuffer.Dispose();
        }
    }

    private readonly struct Submission(Frame frame, FrameGenerationSwapChainArgs args, TimelineValue rendered, double realIntervalSeconds)
    {
        public readonly Frame Frame = frame;

        public readonly FrameGenerationSwapChainArgs Args = args;

        public readonly TimelineValue Rendered = rendered;

        public readonly double RealIntervalSeconds = realIntervalSeconds;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct CaptureConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(16)]
    public ResourceHandle SrcColor;

    [FieldOffset(24)]
    public ResourceHandle SrcDepth;

    [FieldOffset(32)]
    public ResourceHandle SrcMotionVectors;

    [FieldOffset(40)]
    public ResourceHandle DstDepth;

    [FieldOffset(48)]
    public ResourceHandle DstMotionVectors;
}
