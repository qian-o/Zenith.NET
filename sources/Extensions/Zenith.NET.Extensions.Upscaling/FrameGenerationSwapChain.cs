using System.Collections.Concurrent;
using System.Diagnostics;

namespace Zenith.NET.Extensions.Upscaling;

public class FrameGenerationSwapChain : DisposableObject
{
    private readonly SwapChain swapChain;
    private readonly FramePresenter presenter;
    private readonly Stopwatch resizeStopwatch = new();

    private FrameGeneration? frameGeneration;

    internal FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc)
    {
        swapChain = context.CreateSwapChain(new()
        {
            Surface = desc.Surface,
            Format = desc.Format,
            IsVerticalSyncEnabled = desc.IsVerticalSyncEnabled,
            IsPresentQueueEnabled = true
        });

        presenter = new(swapChain);

        Desc = desc;
    }

    public FrameGenerationSwapChainDesc Desc { get; private set; }

    public Texture Drawable => presenter.Color;

    public void Present(FrameGenerationSwapChainArgs args)
    {
        const double ResizeCooldown = 0.1;

        Texture color = Drawable;

        if (resizeStopwatch.IsRunning && resizeStopwatch.Elapsed.TotalSeconds < ResizeCooldown)
        {
            presenter.Submit(swapChain.Context.GraphicsQueue.Timeline.Signal(), TextureLayout.Present, false);

            return;
        }

        resizeStopwatch.Reset();

        if (Ensure(args.Depth.Desc.Width, args.Depth.Desc.Height))
        {
            args.Reset = true;
        }

        CommandBuffer commandBuffer = swapChain.Context.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(color, default, TextureLayout.Present, TextureLayout.Sampled);

        frameGeneration!.Dispatch(commandBuffer, new()
        {
            Color = color,
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
            Texture output = presenter.Interpolated;

            commandBuffer.Transition(frameGeneration.Output, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.CopyTexture(frameGeneration.Output, default, default, output, default, default, new()
            {
                Width = Desc.Surface.Width,
                Height = Desc.Surface.Height,
                Depth = 1
            });
            commandBuffer.Transition(frameGeneration.Output, default, TextureLayout.CopySrc, TextureLayout.Sampled);
        }

        presenter.Submit(commandBuffer.Submit(), TextureLayout.Sampled, !args.Reset);
    }

    public void Resize(uint width, uint height)
    {
        Reset();

        Desc = Desc with
        {
            Surface = Desc.Surface with
            {
                Width = width,
                Height = height
            }
        };

        swapChain.Resize(width, height);
        resizeStopwatch.Restart();
    }

    public void Refresh(Surface surface)
    {
        Reset();

        Desc = Desc with { Surface = surface };

        swapChain.Refresh(surface);
    }

    protected override void Destroy()
    {
        Reset();

        presenter.Dispose();
        swapChain.Dispose();
    }

    private bool Ensure(uint renderWidth, uint renderHeight)
    {
        if (frameGeneration is not null && frameGeneration.Desc.RenderWidth == renderWidth && frameGeneration.Desc.RenderHeight == renderHeight)
        {
            return false;
        }

        frameGeneration?.Dispose();
        frameGeneration = swapChain.Context.CreateFrameGeneration(new()
        {
            Format = Desc.Format,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = Desc.Surface.Width,
            OutputHeight = Desc.Surface.Height
        });

        return true;
    }

    private void Reset()
    {
        presenter.Reset();

        frameGeneration?.Dispose();
        frameGeneration = null;
        resizeStopwatch.Reset();
    }
}

internal class FramePresenter : DisposableObject
{
    private readonly FramePacer pacer;
    private readonly Frame[] frames = [new(), new()];
    private readonly BlockingCollection<Submission> submissions = [];
    private readonly ManualResetEventSlim aborting = new(false);
    private readonly Thread thread;

    private int frameIndex;

    public FramePresenter(SwapChain swapChain)
    {
        SwapChain = swapChain;
        pacer = new(swapChain.Desc.IsVerticalSyncEnabled);

        thread = new(PresentLoop)
        {
            IsBackground = true,
            Name = nameof(FramePresenter)
        };
        thread.Start();
    }

    public SwapChain SwapChain { get; }

    public Texture Color => CurrentFrame().Color ??= CreateOutput();

    public Texture Interpolated => CurrentFrame().Interpolated ??= CreateOutput();

    public void Submit(TimelineValue rendered, TextureLayout colorLayout, bool interpolated)
    {
        Frame frame = CurrentFrame();
        frame.Ready.Reset();

        submissions.Add(new(frame, rendered, colorLayout, interpolated));

        frameIndex = (frameIndex + 1) % frames.Length;
    }

    public void Reset()
    {
        aborting.Set();

        foreach (Frame frame in frames)
        {
            frame.Ready.Wait();
        }

        foreach (Frame frame in frames)
        {
            frame.Reset();
        }

        frameIndex = 0;
        pacer.Reset();
        aborting.Reset();
    }

    protected override void Destroy()
    {
        submissions.CompleteAdding();
        thread.Join();

        aborting.Dispose();
        submissions.Dispose();

        foreach (Frame frame in frames)
        {
            frame.Dispose();
        }
    }

    private Frame CurrentFrame()
    {
        Frame frame = frames[frameIndex];
        frame.Ready.Wait();

        return frame;
    }

    private Texture CreateOutput()
    {
        return UpscalingBridge.CreateOutput(SwapChain.Context, SwapChain.Desc.Format, SwapChain.Desc.Surface.Width, SwapChain.Desc.Surface.Height);
    }

    private void PresentLoop()
    {
        foreach (Submission submission in submissions.GetConsumingEnumerable())
        {
            PresentFrame(submission);
        }
    }

    private void PresentFrame(Submission submission)
    {
        Frame frame = submission.Frame;

        try
        {
            if (aborting.IsSet)
            {
                submission.Rendered.Wait();

                return;
            }

            if (submission.Interpolated)
            {
                CopyToDrawable(frame.Interpolated!, TextureLayout.CopyDst, [submission.Rendered]);

                double stepSeconds = pacer.NextStep(true);
                if (!PresentDrawable(0.0) || aborting.IsSet)
                {
                    return;
                }

                CopyToDrawable(frame.Color!, submission.ColorLayout, []);
                PresentDrawable(stepSeconds);
            }
            else
            {
                CopyToDrawable(frame.Color!, submission.ColorLayout, [submission.Rendered]);
                pacer.NextStep(false);
                PresentDrawable(0.0);
            }
        }
        finally
        {
            frame.Ready.Set();
        }
    }

    private void CopyToDrawable(Texture source, TextureLayout sourceLayout, ReadOnlySpan<TimelineValue> waits)
    {
        Texture drawable = SwapChain.Drawable;
        CommandBuffer commandBuffer = SwapChain.Queue.CommandBuffer();

        commandBuffer.Transition(source, default, sourceLayout, TextureLayout.CopySrc);
        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(source, default, default, drawable, default, default, new()
        {
            Width = drawable.Desc.Width,
            Height = drawable.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(drawable, default, TextureLayout.CopyDst, TextureLayout.Present);

        commandBuffer.Submit(waits).Wait();
    }

    private bool PresentDrawable(double stepSeconds)
    {
        if (!pacer.Wait(stepSeconds, aborting))
        {
            return false;
        }

        SwapChain.Present();

        return true;
    }

    private class Frame : DisposableObject
    {
        public readonly ManualResetEventSlim Ready = new(true);

        public Texture? Color;

        public Texture? Interpolated;

        public void Reset()
        {
            Interpolated?.Dispose();
            Interpolated = null;

            Color?.Dispose();
            Color = null;
        }

        protected override void Destroy()
        {
            Reset();

            Ready.Dispose();
        }
    }

    private readonly struct Submission(Frame frame, TimelineValue rendered, TextureLayout colorLayout, bool interpolated)
    {
        public readonly Frame Frame = frame;

        public readonly TimelineValue Rendered = rendered;

        public readonly TextureLayout ColorLayout = colorLayout;

        public readonly bool Interpolated = interpolated;
    }
}

internal class FramePacer(bool isVerticalSyncEnabled)
{
    private readonly Stopwatch frameStopwatch = new();
    private readonly Stopwatch presentStopwatch = new();

    private double frameInterval;

    public double NextStep(bool interpolated)
    {
        const double MinInterval = 0.001;
        const double MaxInterval = 0.1;
        const double VSyncMargin = 0.0005;

        if (!interpolated || !frameStopwatch.IsRunning)
        {
            frameStopwatch.Restart();
            frameInterval = 0.0;

            return 0.0;
        }

        double interval = Math.Clamp(frameStopwatch.Elapsed.TotalSeconds, MinInterval, MaxInterval);
        frameStopwatch.Restart();

        frameInterval = frameInterval is 0.0 ? interval : double.Lerp(frameInterval, interval, 0.25);

        return Math.Max((frameInterval * 0.5) - (isVerticalSyncEnabled ? VSyncMargin : 0.0), 0.0);
    }

    public bool Wait(double stepSeconds, ManualResetEventSlim aborting)
    {
        const int SleepMarginInMilliseconds = 16;

        if (stepSeconds > 0.0 && presentStopwatch.IsRunning)
        {
            double remainingSeconds = stepSeconds - presentStopwatch.Elapsed.TotalSeconds;
            if (remainingSeconds > 0.0)
            {
                int milliseconds = (int)(remainingSeconds * 1000.0) - SleepMarginInMilliseconds;
                if (milliseconds > 0 && aborting.Wait(milliseconds))
                {
                    return false;
                }

                SpinWait spinWait = new();
                while (!aborting.IsSet && presentStopwatch.Elapsed.TotalSeconds < stepSeconds)
                {
                    spinWait.SpinOnce(-1);
                }
            }
        }

        if (aborting.IsSet)
        {
            return false;
        }

        presentStopwatch.Restart();

        return true;
    }

    public void Reset()
    {
        frameStopwatch.Reset();
        presentStopwatch.Reset();
        frameInterval = 0.0;
    }
}
