using System.Diagnostics;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Helpers;

internal class FramePresenter : DisposableObject
{
    public const int FrameCount = 3;

    private const int IntervalCount = 8;

    private readonly SwapChain swapChain;
    private readonly Func<CommandBuffer, Texture, Texture?, (Texture Frame, Texture? GeneratedFrame)>? compose;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Queue<long> timestampsInTicks = new(IntervalCount + 1);
    private readonly Stopwatch framerateWatch = new();
    private readonly Batch[] batches = new Batch[FrameCount];
    private readonly ManualResetEventSlim[] completions = new ManualResetEventSlim[FrameCount];
    private readonly SemaphoreSlim pendingFrames = new(0, FrameCount);
    private readonly Thread thread;

    private DLSSFrameGeneration? frameGeneration;
    private DLSSFrameGenerationDesc frameGenerationDesc;
    private Texture[] color = [];
    private Texture[] generatedColor = [];
    private Texture[] depth = [];
    private Texture[] motionVectors = [];
    private Buffer[] disableInterpolation = [];
    private Buffer[] interpolationStatus = [];
    private bool hasHistory;
    private int frameIndex;
    private int framerateCount;
    private long intervalTicks;

    public FramePresenter(SwapChain swapChain, Func<CommandBuffer, Texture, Texture?, (Texture Frame, Texture? GeneratedFrame)>? compose = null)
    {
        this.swapChain = swapChain;
        this.compose = compose;

        for (int i = 0; i < FrameCount; i++)
        {
            completions[i] = new(initialState: true);
        }

        thread = new(Run)
        {
            Name = "Frame Presenter",
            IsBackground = true
        };

        thread.Start();
    }

    public double Framerate { get; private set; }

    public bool IsFrameGenerationEnabled { get; set; }

    public int BeginFrame()
    {
        completions[frameIndex].Wait();

        return frameIndex;
    }

    public void EndFrame(CommandBuffer commandBuffer, Texture frame)
    {
        if (generatedColor.Length is not 0)
        {
            WaitIdle();
            DestroyFrameGenerationResources();

            if (compose is not null)
            {
                DestroyColorResources();
            }
        }

        if (compose is null)
        {
            EnsureColorResources(frame);
            Snapshot(commandBuffer, frame, color[frameIndex]);
            frame = color[frameIndex];
        }

        (Texture output, Texture? generatedFrame) = Compose(commandBuffer, frame, null);
        TimelineValue ready = commandBuffer.Submit();
        ready.Wait();

        hasHistory = false;
        QueueFrame(ready, output, generatedFrame, null);
    }

    public void EndFrame(CommandBuffer commandBuffer, DLSSFrameGenerationArgs args)
    {
        if (!IsFrameGenerationEnabled)
        {
            EndFrame(commandBuffer, args.Color);

            return;
        }

        EnsureColorResources(args.Color);
        EnsureFrameGenerationResources(args.Color, args.Depth, args.MotionVectors);

        frameGeneration ??= swapChain.Context.CreateDLSSFrameGeneration(frameGenerationDesc);

        Snapshot(commandBuffer, args.Color, color[frameIndex]);
        Snapshot(commandBuffer, args.Depth, depth[frameIndex]);
        Snapshot(commandBuffer, args.MotionVectors, motionVectors[frameIndex]);

        args.Color = color[frameIndex];
        args.Depth = depth[frameIndex];
        args.MotionVectors = motionVectors[frameIndex];
        args.Output = generatedColor[frameIndex];
        args.OutputDisableInterpolation = disableInterpolation[frameIndex];
        args.Reset |= !hasHistory;

        TimelineValue rendered = commandBuffer.Submit();
        commandBuffer = swapChain.Context.ComputeQueue.CommandBuffer();

        commandBuffer.Transition(args.Output, default, TextureLayout.Undefined, TextureLayout.Storage);
        frameGeneration.Dispatch(commandBuffer, args);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.Copy);
        commandBuffer.CopyBuffer(args.OutputDisableInterpolation, 0, interpolationStatus[frameIndex], 0, sizeof(uint));
        commandBuffer.Transition(args.Output, default, TextureLayout.Storage, TextureLayout.Sampled);

        (Texture frame, Texture? generatedFrame) = Compose(commandBuffer, args.Color, args.Output);
        TimelineValue ready = commandBuffer.Submit(rendered);
        rendered.Wait();

        hasHistory = true;
        QueueFrame(ready, frame, generatedFrame, interpolationStatus[frameIndex]);
    }

    public void WaitIdle()
    {
        foreach (ManualResetEventSlim completion in completions)
        {
            completion.Wait();
        }
    }

    protected override void Destroy()
    {
        if (thread?.IsAlive is true)
        {
            WaitIdle();

            cancellation.Cancel();
            pendingFrames.Release();
            thread.Join();
        }

        DestroyFrameGenerationResources();
        DestroyColorResources();

        for (int i = completions.Length - 1; i >= 0; i--)
        {
            completions[i]?.Dispose();
        }

        pendingFrames?.Dispose();
        cancellation.Dispose();
    }

    private void QueueFrame(TimelineValue ready, Texture frame, Texture? generatedFrame, Buffer? status)
    {
        completions[frameIndex].Reset();
        batches[frameIndex] = new(ready, frame, generatedFrame, status);
        frameIndex = (frameIndex + 1) % FrameCount;
        pendingFrames.Release();
    }

    private (Texture Frame, Texture? GeneratedFrame) Compose(CommandBuffer commandBuffer, Texture frame, Texture? generatedFrame)
    {
        if (compose is not null)
        {
            return compose(commandBuffer, frame, generatedFrame);
        }

        commandBuffer.Transition(frame, default, TextureLayout.Sampled, TextureLayout.CopySrc);

        if (generatedFrame is not null)
        {
            commandBuffer.Transition(generatedFrame, default, TextureLayout.Sampled, TextureLayout.CopySrc);
        }

        return (frame, generatedFrame);
    }

    private void Run()
    {
        int index = 0;

        while (true)
        {
            pendingFrames.Wait();

            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            try
            {
                Present(batches[index]);
            }
            finally
            {
                completions[index].Set();
            }

            index = (index + 1) % batches.Length;
        }
    }

    private void Present(Batch batch)
    {
        bool canInterpolate = true;
        if (batch.InterpolationStatus is Buffer status)
        {
            batch.Ready.Wait();

            unsafe
            {
                canInterpolate = *(byte*)status.Map() is 0;
            }

            status.Unmap();
        }

        if (batch.GeneratedFrame is not null && canInterpolate)
        {
            Present(batch.Ready, batch.GeneratedFrame);

            long deadlineTicks = Stopwatch.GetTimestamp() + (intervalTicks / 2);

            while (Stopwatch.GetTimestamp() < deadlineTicks)
            {
                Thread.Yield();
            }
        }

        Present(batch.Ready, batch.Frame);

        long nowTicks = Stopwatch.GetTimestamp();

        if (timestampsInTicks.Count > IntervalCount)
        {
            timestampsInTicks.Dequeue();
        }

        timestampsInTicks.Enqueue(nowTicks);

        intervalTicks = Math.Min((nowTicks - timestampsInTicks.Peek()) / Math.Max(timestampsInTicks.Count - 1, 1), Stopwatch.Frequency / 10);
    }

    private void Present(TimelineValue ready, Texture frame)
    {
        Texture drawable = swapChain.Drawable;

        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(frame, default, default, drawable, default, default, new()
        {
            Width = frame.Desc.Width,
            Height = frame.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(drawable, default, TextureLayout.CopyDst, TextureLayout.Present);
        commandBuffer.Submit(ready);

        swapChain.Present();

        if (!framerateWatch.IsRunning)
        {
            framerateWatch.Start();
        }

        framerateCount++;

        if (framerateWatch.Elapsed.TotalSeconds >= 1.0)
        {
            Framerate = framerateCount / framerateWatch.Elapsed.TotalSeconds;

            framerateCount = 0;
            framerateWatch.Restart();
        }
    }

    private void EnsureColorResources(Texture frame)
    {
        if (color.Length is not 0 && color[0].Desc.Format == frame.Desc.Format && color[0].Desc.Width == frame.Desc.Width && color[0].Desc.Height == frame.Desc.Height)
        {
            return;
        }

        WaitIdle();
        DestroyFrameGenerationResources();
        DestroyColorResources();
        CreateColorResources(frame);

        hasHistory = false;
    }

    private void CreateColorResources(Texture frame)
    {
        color = new Texture[FrameCount];

        for (int i = 0; i < FrameCount; i++)
        {
            color[i] = CreateTexture(frame.Desc.Width, frame.Desc.Height, frame.Desc.Format, TextureUsages.Sampled | TextureUsages.TransferSrc | TextureUsages.TransferDst);
        }
    }

    private void DestroyColorResources()
    {
        for (int i = color.Length - 1; i >= 0; i--)
        {
            color[i].Dispose();
        }
        color = [];
    }

    private void EnsureFrameGenerationResources(Texture frame, Texture frameDepth, Texture frameMotionVectors)
    {
        DLSSFrameGenerationDesc desc = new()
        {
            Format = frame.Desc.Format,
            InputWidth = frameDepth.Desc.Width,
            InputHeight = frameDepth.Desc.Height,
            OutputWidth = frame.Desc.Width,
            OutputHeight = frame.Desc.Height
        };

        if (generatedColor.Length is not 0 && frameGenerationDesc.Format == desc.Format && frameGenerationDesc.InputWidth == desc.InputWidth && frameGenerationDesc.InputHeight == desc.InputHeight && frameGenerationDesc.OutputWidth == desc.OutputWidth && frameGenerationDesc.OutputHeight == desc.OutputHeight && depth[0].Desc.Format == frameDepth.Desc.Format && motionVectors[0].Desc.Format == frameMotionVectors.Desc.Format)
        {
            return;
        }

        WaitIdle();
        DestroyFrameGenerationResources();

        frameGenerationDesc = desc;
        CreateFrameGenerationResources(frameDepth.Desc.Format, frameMotionVectors.Desc.Format);
        hasHistory = false;
    }

    private void CreateFrameGenerationResources(PixelFormat depthFormat, PixelFormat motionVectorFormat)
    {
        generatedColor = new Texture[FrameCount];
        depth = new Texture[FrameCount];
        motionVectors = new Texture[FrameCount];
        disableInterpolation = new Buffer[FrameCount];
        interpolationStatus = new Buffer[FrameCount];

        for (int i = 0; i < FrameCount; i++)
        {
            generatedColor[i] = CreateTexture(frameGenerationDesc.OutputWidth, frameGenerationDesc.OutputHeight, frameGenerationDesc.Format, TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferSrc);
            depth[i] = CreateTexture(frameGenerationDesc.InputWidth, frameGenerationDesc.InputHeight, depthFormat, TextureUsages.Sampled | TextureUsages.TransferDst);
            motionVectors[i] = CreateTexture(frameGenerationDesc.InputWidth, frameGenerationDesc.InputHeight, motionVectorFormat, TextureUsages.Sampled | TextureUsages.TransferDst);
            disableInterpolation[i] = swapChain.Context.CreateBuffer(new()
            {
                SizeInBytes = sizeof(uint),
                Usages = BufferUsages.StorageReadWrite | BufferUsages.TransferSrc,
                Residency = MemoryResidency.GpuOnly
            });
            interpolationStatus[i] = swapChain.Context.CreateBuffer(new()
            {
                SizeInBytes = sizeof(uint),
                Usages = BufferUsages.TransferDst,
                Residency = MemoryResidency.CpuReadOnly
            });
        }
    }

    private void DestroyFrameGenerationResources()
    {
        frameGeneration?.Dispose();
        frameGeneration = null;

        for (int i = generatedColor.Length - 1; i >= 0; i--)
        {
            interpolationStatus[i].Dispose();
            disableInterpolation[i].Dispose();
            motionVectors[i].Dispose();
            depth[i].Dispose();
            generatedColor[i].Dispose();
        }
        interpolationStatus = [];
        disableInterpolation = [];
        motionVectors = [];
        depth = [];
        generatedColor = [];
    }

    private Texture CreateTexture(uint width, uint height, PixelFormat format, TextureUsages usages)
    {
        return swapChain.Context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = usages
        });
    }

    private static void Snapshot(CommandBuffer commandBuffer, Texture src, Texture dst)
    {
        commandBuffer.Transition(src, default, TextureLayout.Sampled, TextureLayout.CopySrc);
        commandBuffer.Transition(dst, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(src, default, default, dst, default, default, new()
        {
            Width = src.Desc.Width,
            Height = src.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(dst, default, TextureLayout.CopyDst, TextureLayout.Sampled);
        commandBuffer.Transition(src, default, TextureLayout.CopySrc, TextureLayout.Sampled);
    }

    private readonly struct Batch(TimelineValue ready, Texture frame, Texture? generatedFrame, Buffer? interpolationStatus)
    {
        public readonly TimelineValue Ready = ready;

        public readonly Texture Frame = frame;

        public readonly Texture? GeneratedFrame = generatedFrame;

        public readonly Buffer? InterpolationStatus = interpolationStatus;
    }
}
