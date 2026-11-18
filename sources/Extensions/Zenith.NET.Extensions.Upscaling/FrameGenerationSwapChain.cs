using System.Collections.Concurrent;
using System.Diagnostics;

namespace Zenith.NET.Extensions.Upscaling;

public class FrameGenerationSwapChain : GraphicsResource
{
    private readonly Stopwatch resizeStopwatch = new();
    private readonly FramePresenter presenter;

    private FrameGeneration? frameGeneration;

    internal FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc) : base(context)
    {
        presenter = new(context, desc);

        Desc = desc;
    }

    public FrameGenerationSwapChainDesc Desc { get; private set; }

    public Texture Drawable => presenter.Drawable;

    public void Present(FrameGenerationSwapChainArgs args)
    {
        const double ResizeCooldown = 0.1;

        Texture color = Drawable;

        if (resizeStopwatch.IsRunning && resizeStopwatch.Elapsed.TotalSeconds < ResizeCooldown)
        {
            presenter.Submit(Context.GraphicsQueue.Timeline.Signal(), TextureLayout.Present, false);

            return;
        }

        resizeStopwatch.Reset();

        if (Ensure(args.Depth.Desc.Width, args.Depth.Desc.Height))
        {
            args.Reset = true;
        }

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

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
            Texture interpolated = presenter.Interpolated;

            commandBuffer.Transition(frameGeneration.Output, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(interpolated, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.CopyTexture(frameGeneration.Output, default, default, interpolated, default, default, new()
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
        Desc = Desc with
        {
            Surface = Desc.Surface with
            {
                Width = width,
                Height = height
            }
        };

        presenter.Resize(width, height);

        frameGeneration?.Dispose();
        frameGeneration = null;
        resizeStopwatch.Restart();
    }

    public void Refresh(Surface surface)
    {
        Desc = Desc with { Surface = surface };

        presenter.Refresh(surface);

        frameGeneration?.Dispose();
        frameGeneration = null;
        resizeStopwatch.Reset();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        presenter.SetResourceName(name);
    }

    protected override void Destroy()
    {
        presenter.Dispose();
        frameGeneration?.Dispose();
    }

    private bool Ensure(uint renderWidth, uint renderHeight)
    {
        if (frameGeneration is not null && frameGeneration.Desc.RenderWidth == renderWidth && frameGeneration.Desc.RenderHeight == renderHeight)
        {
            return false;
        }

        frameGeneration?.Dispose();
        frameGeneration = Context.CreateFrameGeneration(new()
        {
            Format = Desc.Format,
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = Desc.Surface.Width,
            OutputHeight = Desc.Surface.Height
        });

        return true;
    }

    private class FramePresenter : DisposableObject
    {
        private readonly GraphicsContext context;
        private readonly SwapChain swapChain;
        private readonly FramePacer pacer;
        private readonly Frame[] frames = [new(), new()];
        private readonly BlockingCollection<Submission> submissions = [];
        private readonly ManualResetEventSlim aborting = new(false);
        private readonly Thread thread;

        private int frameIndex;

        public FramePresenter(GraphicsContext context, FrameGenerationSwapChainDesc desc)
        {
            this.context = context;

            swapChain = context.CreateSwapChain(new()
            {
                Surface = desc.Surface,
                Format = desc.Format,
                IsVerticalSyncEnabled = desc.IsVerticalSyncEnabled,
                IsPresentQueueEnabled = true
            });

            pacer = new(desc.IsVerticalSyncEnabled);

            thread = new(PresentLoop)
            {
                IsBackground = true,
                Name = nameof(FramePresenter)
            };
            thread.Start();
        }

        public Texture Drawable => CurrentFrame().Color ??= CreateOutput();

        public Texture Interpolated => CurrentFrame().Interpolated ??= CreateOutput();

        public void Submit(TimelineValue rendered, TextureLayout colorLayout, bool interpolated)
        {
            Frame frame = CurrentFrame();
            frame.Ready.Reset();

            submissions.Add(new(frame, rendered, colorLayout, interpolated));

            frameIndex = (frameIndex + 1) % frames.Length;
        }

        public void Resize(uint width, uint height)
        {
            Reset();

            swapChain.Resize(width, height);
        }

        public void Refresh(Surface surface)
        {
            Reset();

            swapChain.Refresh(surface);
        }

        public void SetResourceName(string name)
        {
            swapChain.Name = name;
        }

        protected override void Destroy()
        {
            aborting.Set();
            submissions.CompleteAdding();
            thread.Join();

            aborting.Dispose();
            submissions.Dispose();

            foreach (Frame frame in frames)
            {
                frame.Dispose();
            }

            swapChain.Dispose();
        }

        private void Reset()
        {
            aborting.Set();

            foreach (Frame frame in frames)
            {
                frame.Ready.Wait();
                frame.Reset();
            }

            frameIndex = 0;
            pacer.Reset();
            aborting.Reset();
        }

        private Frame CurrentFrame()
        {
            Frame frame = frames[frameIndex];
            frame.Ready.Wait();

            return frame;
        }

        private Texture CreateOutput()
        {
            return UpscalingBridge.CreateOutput(context, swapChain.Desc.Format, swapChain.Desc.Surface.Width, swapChain.Desc.Surface.Height);
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
                    if (!PresentDrawable(0.0))
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
            Texture drawable = swapChain.Drawable;
            CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

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

            swapChain.Present();

            return !aborting.IsSet;
        }
    }

    private class FramePacer(bool isVerticalSyncEnabled)
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

            if (stepSeconds > 0.0)
            {
                int milliseconds = (int)((stepSeconds - presentStopwatch.Elapsed.TotalSeconds) * 1000.0) - SleepMarginInMilliseconds;
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
