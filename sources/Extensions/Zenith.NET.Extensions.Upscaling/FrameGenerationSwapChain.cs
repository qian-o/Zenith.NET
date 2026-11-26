using System.Collections.Concurrent;
using System.Diagnostics;

namespace Zenith.NET.Extensions.Upscaling;

public class FrameGenerationSwapChain : GraphicsResource
{
    private readonly Stopwatch cooldown = new();
    private readonly Presenter presenter;

    private FrameGeneration? frameGeneration;

    internal FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc) : base(context)
    {
        Desc = desc;

        presenter = new(context, desc);
    }

    public FrameGenerationSwapChainDesc Desc { get; private set; }

    public Texture Drawable => presenter.Drawable;

    public void Present(FrameGenerationSwapChainArgs args)
    {
        const double ResizeCooldown = 0.1;

        Texture color = Drawable;

        if (cooldown.IsRunning && cooldown.Elapsed.TotalSeconds < ResizeCooldown)
        {
            presenter.Submit(Context.GraphicsQueue.Timeline.Signal(), TextureLayout.Present, false);

            return;
        }

        cooldown.Reset();

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

        DestroyFrameGeneration();

        cooldown.Restart();
    }

    public void Refresh(Surface surface)
    {
        Desc = Desc with { Surface = surface };

        presenter.Refresh(surface);

        DestroyFrameGeneration();

        cooldown.Reset();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        presenter?.Dispose();

        DestroyFrameGeneration();
    }

    private void DestroyFrameGeneration()
    {
        if (frameGeneration is null)
        {
            return;
        }

        Context.GraphicsQueue.Timeline.Signal().Wait();

        frameGeneration.Dispose();
        frameGeneration = null;
    }

    private bool Ensure(uint renderWidth, uint renderHeight)
    {
        if (frameGeneration is not null
            && frameGeneration.Desc.RenderWidth == renderWidth
            && frameGeneration.Desc.RenderHeight == renderHeight
            && frameGeneration.Desc.OutputWidth == Desc.Surface.Width
            && frameGeneration.Desc.OutputHeight == Desc.Surface.Height)
        {
            return false;
        }

        DestroyFrameGeneration();

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

    private class Presenter : DisposableObject
    {
        private readonly Frame[] frames = [new(), new()];
        private readonly BlockingCollection<Submission> submissions = [];
        private readonly ManualResetEventSlim aborting = new(false);
        private readonly GraphicsContext context;
        private readonly SwapChain swapChain;
        private readonly Pacer pacer;
        private readonly Thread thread;

        private int index;

        public Presenter(GraphicsContext context, FrameGenerationSwapChainDesc desc)
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
                Name = nameof(FrameGenerationSwapChain)
            };
            thread.Start();
        }

        public Texture Drawable => CurrentFrame().Color ??= UpscalingBridge.CreateOutput(context, swapChain.Desc.Format, swapChain.Desc.Surface.Width, swapChain.Desc.Surface.Height);

        public Texture Interpolated => CurrentFrame().Interpolated ??= UpscalingBridge.CreateOutput(context, swapChain.Desc.Format, swapChain.Desc.Surface.Width, swapChain.Desc.Surface.Height);

        public void Submit(TimelineValue wait, TextureLayout layout, bool interpolated)
        {
            Frame frame = CurrentFrame();
            frame.Ready.Reset();

            submissions.Add(new(frame, wait, layout, interpolated));

            index = (index + 1) % frames.Length;
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

        protected override void Destroy()
        {
            aborting.Set();
            submissions.CompleteAdding();
            thread?.Join();

            swapChain?.Dispose();
            aborting.Dispose();
            submissions.Dispose();

            foreach (Frame frame in frames)
            {
                frame.Dispose();
            }
        }

        private void Reset()
        {
            aborting.Set();

            foreach (Frame frame in frames)
            {
                frame.Ready.Wait();
                frame.Reset();
            }

            index = 0;
            pacer.Reset();
            aborting.Reset();
        }

        private Frame CurrentFrame()
        {
            Frame frame = frames[index];
            frame.Ready.Wait();

            return frame;
        }

        private void PresentLoop()
        {
            foreach (Submission submission in submissions.GetConsumingEnumerable())
            {
                try
                {
                    PresentFrame(submission);
                }
                finally
                {
                    submission.Frame.Ready.Set();
                }
            }
        }

        private void PresentFrame(Submission submission)
        {
            if (aborting.IsSet)
            {
                submission.Wait.Wait();

                return;
            }

            Frame frame = submission.Frame;

            if (submission.Interpolated)
            {
                Copy(frame.Interpolated!, TextureLayout.CopyDst, [submission.Wait]);

                if (aborting.IsSet)
                {
                    return;
                }

                swapChain.Present();

                double delay = pacer.Next(true);

                if (aborting.IsSet)
                {
                    return;
                }

                Copy(frame.Color!, submission.Layout, []);

                if (pacer.Wait(delay, aborting))
                {
                    swapChain.Present();
                }
            }
            else
            {
                Copy(frame.Color!, submission.Layout, [submission.Wait]);

                if (!aborting.IsSet)
                {
                    swapChain.Present();
                }

                pacer.Next(false);
            }
        }

        private void Copy(Texture source, TextureLayout layout, ReadOnlySpan<TimelineValue> waits)
        {
            Texture drawable = swapChain.Drawable;
            CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

            commandBuffer.Transition(source, default, layout, TextureLayout.CopySrc);
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
    }

    private class Pacer(bool isVerticalSyncEnabled)
    {
        private readonly Stopwatch stopwatch = new();

        private double interval;

        public double Next(bool interpolated)
        {
            const double MinInterval = 0.001;
            const double MaxInterval = 0.1;
            const double VSyncMargin = 0.0005;

            if (!interpolated || !stopwatch.IsRunning)
            {
                stopwatch.Restart();
                interval = 0.0;

                return 0.0;
            }

            double elapsed = Math.Clamp(stopwatch.Elapsed.TotalSeconds, MinInterval, MaxInterval);

            stopwatch.Restart();
            interval = interval is 0.0 ? elapsed : double.Lerp(interval, elapsed, 0.25);

            return Math.Max((interval * 0.5) - (isVerticalSyncEnabled ? VSyncMargin : 0.0), 0.0);
        }

        public bool Wait(double delay, ManualResetEventSlim aborting)
        {
            const int SleepMargin = 16;

            int milliseconds = (int)((delay - stopwatch.Elapsed.TotalSeconds) * 1000.0) - SleepMargin;
            if (milliseconds > 0 && aborting.Wait(milliseconds))
            {
                return false;
            }

            SpinWait spinWait = new();
            while (!aborting.IsSet && stopwatch.Elapsed.TotalSeconds < delay)
            {
                spinWait.SpinOnce(-1);
            }

            return !aborting.IsSet;
        }

        public void Reset()
        {
            stopwatch.Reset();
            interval = 0.0;
        }
    }

    private class Frame : DisposableObject
    {
        public ManualResetEventSlim Ready { get; } = new(true);

        public Texture? Color { get; set; }

        public Texture? Interpolated { get; set; }

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

    private readonly struct Submission(Frame frame, TimelineValue wait, TextureLayout layout, bool interpolated)
    {
        public readonly Frame Frame = frame;

        public readonly TimelineValue Wait = wait;

        public readonly TextureLayout Layout = layout;

        public readonly bool Interpolated = interpolated;
    }
}
