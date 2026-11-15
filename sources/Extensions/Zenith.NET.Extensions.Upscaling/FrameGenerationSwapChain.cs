using System.Collections.Concurrent;
using System.Diagnostics;

namespace Zenith.NET.Extensions.Upscaling;

public class FrameGenerationSwapChain : DisposableObject
{
    private const int FrameCount = 3;

    private readonly GraphicsContext context;
    private readonly SwapChain swapChain;
    private readonly Frame[] frames = new Frame[FrameCount];
    private readonly Stopwatch submitStopwatch = new();
    private readonly Stopwatch presentStopwatch = new();

    private FrameGenerationSwapChainDesc desc;
    private FrameGeneration? frameGeneration;
    private CancellationTokenSource tokenSource = null!;
    private BlockingCollection<Submission> submissions = null!;
    private Thread presenterThread = null!;
    private int frameIndex;
    private double frameInterval;

    internal FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc)
    {
        this.context = context;
        this.desc = desc;

        swapChain = context.CreateSwapChain(new()
        {
            Surface = desc.Surface,
            Format = desc.Format,
            IsVerticalSyncEnabled = desc.IsVerticalSyncEnabled,
            IsPresentQueueEnabled = true
        });

        CreateResources();
    }

    public FrameGenerationSwapChainDesc Desc => desc;

    public Texture Drawable => frames[frameIndex].Color;

    public void Present(FrameGenerationSwapChainArgs args)
    {
        Frame frame = frames[frameIndex];

        if (Ensure(args.Depth.Desc.Width, args.Depth.Desc.Height))
        {
            args.Reset = true;
        }

        CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.Sampled);

        frameGeneration!.Dispatch(commandBuffer, new()
        {
            Color = frame.Color,
            Depth = args.Depth,
            MotionVectors = args.MotionVectors,
            Hudless = args.Hudless,
            JitterOffset = args.JitterOffset,
            View = args.View,
            Projection = args.Projection,
            DeltaTimeInSeconds = args.DeltaTimeInSeconds,
            Reset = args.Reset
        });

        if (!args.Reset)
        {
            commandBuffer.Transition(frameGeneration.Output, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(frame.Interpolated, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.CopyTexture(frameGeneration.Output, default, default, frame.Interpolated, default, default, new()
            {
                Width = desc.Surface.Width,
                Height = desc.Surface.Height,
                Depth = 1
            });
            commandBuffer.Transition(frame.Interpolated, default, TextureLayout.CopyDst, TextureLayout.CopySrc);
            commandBuffer.Transition(frameGeneration.Output, default, TextureLayout.CopySrc, TextureLayout.Sampled);
        }

        commandBuffer.Transition(frame.Color, default, TextureLayout.Sampled, TextureLayout.CopySrc);

        TimelineValue rendered = commandBuffer.Submit();

        frame.Ready.Reset();
        submissions.Add(new(frame, rendered, NextStep(args.Reset), !args.Reset));

        frameIndex = (frameIndex + 1) % frames.Length;
        frames[frameIndex].Ready.Wait();
    }

    public void Resize(uint width, uint height)
    {
        desc.Surface.Width = width;
        desc.Surface.Height = height;

        DestroyResources();
        swapChain.Resize(width, height);
        CreateResources();
    }

    public void Refresh(Surface surface)
    {
        desc.Surface = surface;

        DestroyResources();
        swapChain.Refresh(surface);
        CreateResources();
    }

    protected override void Destroy()
    {
        DestroyResources();

        swapChain.Dispose();
    }

    private void CreateResources()
    {
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = new(context, desc);
        }

        frameIndex = 0;
        frameInterval = 0.0;
        submitStopwatch.Reset();
        presentStopwatch.Reset();

        tokenSource = new();
        submissions = [with(FrameCount)];
        presenterThread = new(PresentLoop)
        {
            Name = nameof(FrameGenerationSwapChain),
            IsBackground = true
        };
        presenterThread.Start();
    }

    private void DestroyResources()
    {
        tokenSource.Cancel();
        submissions.CompleteAdding();
        presenterThread.Join();

        submissions.Dispose();
        tokenSource.Dispose();

        context.GraphicsQueue.Timeline.Signal().Wait();
        swapChain.Queue.Timeline.Signal().Wait();

        frameGeneration?.Dispose();
        frameGeneration = null;

        for (int i = 0; i < frames.Length; i++)
        {
            frames[i].Dispose();
        }
    }

    private bool Ensure(uint renderWidth, uint renderHeight)
    {
        if (frameGeneration is not null && frameGeneration.Desc.RenderWidth == renderWidth && frameGeneration.Desc.RenderHeight == renderHeight)
        {
            return false;
        }

        frameGeneration?.Dispose();
        frameGeneration = context.CreateFrameGeneration(new()
        {
            Format = desc.Format,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = desc.Surface.Width,
            OutputHeight = desc.Surface.Height
        });

        return true;
    }

    private double NextStep(bool reset)
    {
        const double MinInterval = 0.001;
        const double MaxInterval = 0.1;
        const double VSyncMargin = 0.003;

        if (reset)
        {
            submitStopwatch.Reset();
            frameInterval = 0.0;

            return 0.0;
        }

        if (!submitStopwatch.IsRunning)
        {
            submitStopwatch.Restart();

            return 0.0;
        }

        double interval = Math.Clamp(submitStopwatch.Elapsed.TotalSeconds, MinInterval, MaxInterval);
        submitStopwatch.Restart();

        frameInterval = frameInterval is 0.0 ? interval : double.Lerp(frameInterval, interval, 0.2);

        return Math.Max((frameInterval * 0.5) - (desc.IsVerticalSyncEnabled ? VSyncMargin : 0.0), 0.0);
    }

    private void PresentLoop()
    {
        CancellationToken cancellationToken = tokenSource.Token;

        try
        {
            foreach (Submission submission in submissions.GetConsumingEnumerable(cancellationToken))
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

        if (submission.Interpolated)
        {
            CopyToDrawable(frame.Interpolated, [submission.Rendered]);

            if (presentStopwatch.IsRunning && submissions.Count <= 1)
            {
                WaitUntil(presentStopwatch, submission.StepSeconds, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                frame.Ready.Set();

                return;
            }

            swapChain.Present();
            presentStopwatch.Restart();
        }

        CopyToDrawable(frame.Color, submission.Interpolated ? [] : [submission.Rendered]);
        frame.Ready.Set();

        if (submission.Interpolated && submissions.Count <= 1)
        {
            WaitUntil(presentStopwatch, submission.StepSeconds, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        swapChain.Present();
        presentStopwatch.Restart();
    }

    private void CopyToDrawable(Texture source, ReadOnlySpan<TimelineValue> waits)
    {
        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();
        Texture drawable = swapChain.Drawable;

        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(source, default, default, drawable, default, default, new()
        {
            Width = desc.Surface.Width,
            Height = desc.Surface.Height,
            Depth = 1
        });
        commandBuffer.Transition(drawable, default, TextureLayout.CopyDst, TextureLayout.Present);
        commandBuffer.Submit(waits).Wait();
    }

    private static void WaitUntil(Stopwatch stopwatch, double targetSeconds, CancellationToken cancellationToken)
    {
        const double SpinMargin = 0.002;

        double remainingSeconds = targetSeconds - stopwatch.Elapsed.TotalSeconds;
        if (remainingSeconds <= 0.0)
        {
            return;
        }

        int sleepMilliseconds = (int)((remainingSeconds - SpinMargin) * 1000.0);
        if (sleepMilliseconds > 0 && cancellationToken.WaitHandle.WaitOne(sleepMilliseconds))
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested && stopwatch.Elapsed.TotalSeconds < targetSeconds)
        {
            Thread.SpinWait(64);
        }
    }

    private class Frame(GraphicsContext context, FrameGenerationSwapChainDesc desc) : DisposableObject
    {
        public readonly ManualResetEventSlim Ready = new(true);

        public readonly Texture Color = context.CreateTexture(TextureDesc.Texture2D(desc.Format, desc.Surface.Width, desc.Surface.Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment | TextureUsages.TransferSrc | TextureUsages.TransferDst
        });

        public readonly Texture Interpolated = context.CreateTexture(TextureDesc.Texture2D(desc.Format, desc.Surface.Width, desc.Surface.Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
        });

        protected override void Destroy()
        {
            Interpolated.Dispose();
            Color.Dispose();
            Ready.Dispose();
        }
    }

    private readonly struct Submission(Frame frame, TimelineValue rendered, double stepSeconds, bool interpolated)
    {
        public readonly Frame Frame = frame;

        public readonly TimelineValue Rendered = rendered;

        public readonly double StepSeconds = stepSeconds;

        public readonly bool Interpolated = interpolated;
    }
}
