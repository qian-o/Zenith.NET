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
        if (frameGeneration is not null && frameGeneration.Desc.RenderWidth == renderWidth && frameGeneration.Desc.RenderHeight == renderHeight && frameGeneration.Desc.OutputWidth == Desc.Surface.Width && frameGeneration.Desc.OutputHeight == Desc.Surface.Height)
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
        private readonly ManualResetEventSlim presenting = new(true);
        private readonly Stopwatch timelineClock = Stopwatch.StartNew();
        private readonly GraphicsContext context;
        private readonly SwapChain swapChain;
        private readonly Pacer pacer;
        private readonly Thread thread;

        private Texture? lastPresentedDrawable;
        private ulong submittedId;
        private ulong gpuCompletedId;
        private double lastReturnSeconds;
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
                Name = nameof(FrameGenerationSwapChain),
                Priority = ThreadPriority.AboveNormal
            };
            thread.Start();
        }

        public Texture Drawable => CurrentFrame().Color ??= UpscalingBridge.CreateOutput(context, swapChain.Desc.Format, swapChain.Desc.Surface.Width, swapChain.Desc.Surface.Height);

        public Texture Interpolated => CurrentFrame().Interpolated ??= UpscalingBridge.CreateOutput(context, swapChain.Desc.Format, swapChain.Desc.Surface.Width, swapChain.Desc.Surface.Height);

        public void Submit(TimelineValue wait, TextureLayout layout, bool interpolated)
        {
            Frame frame = CurrentFrame();
            frame.Ready.Reset();

            ulong id = ++submittedId;
            double renderStart = lastReturnSeconds;

            submissions.Add(new(id, frame, wait, layout, interpolated));

            index = (index + 1) % frames.Length;

            wait.Wait();

            if (interpolated && renderStart > 0.0)
            {
                pacer.RecordRenderDuration(timelineClock.Elapsed.TotalSeconds - renderStart);
            }

            Volatile.Write(ref gpuCompletedId, id);

            frames[index].Ready.Wait();

            lastReturnSeconds = timelineClock.Elapsed.TotalSeconds;
        }

        public void Resize(uint width, uint height)
        {
            BeginReset();

            swapChain.Resize(width, height);

            aborting.Reset();
        }

        public void Refresh(Surface surface)
        {
            BeginReset();

            swapChain.Refresh(surface);

            aborting.Reset();
        }

        protected override void Destroy()
        {
            aborting.Set();
            submissions.CompleteAdding();
            thread?.Join();

            swapChain?.Dispose();
            presenting.Dispose();
            aborting.Dispose();
            submissions.Dispose();

            foreach (Frame frame in frames)
            {
                frame.Dispose();
            }
        }

        private void BeginReset()
        {
            aborting.Set();

            foreach (Frame frame in frames)
            {
                frame.Ready.Wait();
            }

            presenting.Wait();

            foreach (Frame frame in frames)
            {
                frame.Reset();
            }

            lastPresentedDrawable = null;
            submittedId = 0;
            gpuCompletedId = 0;
            lastReturnSeconds = 0.0;
            index = 0;
            pacer.Reset();
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
                presenting.Reset();

                try
                {
                    PresentFrame(submission);
                }
                finally
                {
                    submission.Frame.Ready.Set();
                    presenting.Set();
                }
            }
        }

        private void PresentFrame(Submission submission)
        {
            if (aborting.IsSet || ReferenceEquals(swapChain.Drawable, lastPresentedDrawable))
            {
                submission.Wait.Wait();

                return;
            }

            Frame frame = submission.Frame;

            if (submission.Interpolated)
            {
                double presentStart = timelineClock.Elapsed.TotalSeconds;
                TimelineValue copy1Done = Copy(frame.Interpolated!, TextureLayout.CopyDst, [submission.Wait]);

                submission.Wait.Wait();
                copy1Done.Wait();

                if (aborting.IsSet)
                {
                    return;
                }

                double ready1 = timelineClock.Elapsed.TotalSeconds;
                if (!pacer.WaitForNextPresent(timelineClock, ready1, submission.Id, ref gpuCompletedId, false, aborting))
                {
                    return;
                }

                double beforeFlip1 = timelineClock.Elapsed.TotalSeconds;
                double pace1 = Math.Max(beforeFlip1 - ready1, 0.0);
                PresentDrawable();

                double afterPresent1 = timelineClock.Elapsed.TotalSeconds;
                pacer.MarkPresent(afterPresent1, afterPresent1 - beforeFlip1, false);

                if (aborting.IsSet || ReferenceEquals(swapChain.Drawable, lastPresentedDrawable))
                {
                    return;
                }

                Copy(frame.Color!, submission.Layout, []).Wait();

                double ready2 = timelineClock.Elapsed.TotalSeconds;
                if (!pacer.WaitForNextPresent(timelineClock, ready2, submission.Id, ref gpuCompletedId, true, aborting))
                {
                    return;
                }

                if (!aborting.IsSet)
                {
                    double beforeFlip2 = timelineClock.Elapsed.TotalSeconds;
                    double pace2 = Math.Max(beforeFlip2 - ready2, 0.0);
                    PresentDrawable();

                    double afterPresent2 = timelineClock.Elapsed.TotalSeconds;
                    pacer.MarkPresent(afterPresent2, afterPresent2 - beforeFlip2, true);
                    pacer.RecordFrameWork(afterPresent2, afterPresent2 - presentStart - pace1 - pace2);
                }
            }
            else
            {
                TimelineValue copyDone = Copy(frame.Color!, submission.Layout, [submission.Wait]);

                submission.Wait.Wait();
                copyDone.Wait();

                if (!aborting.IsSet)
                {
                    double beforeFlip = timelineClock.Elapsed.TotalSeconds;
                    PresentDrawable();

                    double afterPresent = timelineClock.Elapsed.TotalSeconds;
                    pacer.MarkPresent(afterPresent, afterPresent - beforeFlip, true);
                }
            }
        }

        private void PresentDrawable()
        {
            lastPresentedDrawable = swapChain.Drawable;
            swapChain.Present();
        }

        private TimelineValue Copy(Texture source, TextureLayout layout, ReadOnlySpan<TimelineValue> waits)
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

            return commandBuffer.Submit(waits);
        }
    }

    private class Pacer(bool isVerticalSyncEnabled)
    {
        private const int WindowSize = 8;
        private const double MinInterval = 0.001;
        private const double MaxInterval = 0.1;

        private readonly Lock @lock = new();
        private readonly double[] history = new double[WindowSize];

        private int historyIndex;
        private int historyCount;
        private double latestRenderDuration;
        private double latestPresentWork;
        private double pairPeriod;
        private double presentLead1;
        private double presentLead2;
        private double lastFirstHalf;
        private double lastPairSeconds;
        private double lastPresentSeconds;

        public void RecordRenderDuration(double seconds)
        {
            using Lock.Scope _ = @lock.EnterScope();

            latestRenderDuration = Math.Clamp(seconds, MinInterval, MaxInterval);
        }

        public void RecordFrameWork(double afterPresent2Seconds, double presentWorkSeconds)
        {
            using Lock.Scope _ = @lock.EnterScope();

            double clampedPresent = Math.Clamp(presentWorkSeconds, 0.0, MaxInterval);
            latestPresentWork = latestPresentWork <= 0.0 ? clampedPresent : double.Lerp(latestPresentWork, clampedPresent, 0.35);

            double achievedPair = lastPairSeconds > 0.0 ? Math.Clamp(afterPresent2Seconds - lastPairSeconds, MinInterval, MaxInterval) : 0.0;
            lastPairSeconds = afterPresent2Seconds;

            double unthrottled = Math.Clamp(Math.Max(latestRenderDuration, clampedPresent), MinInterval, MaxInterval);
            double rawPeriod = isVerticalSyncEnabled && achievedPair > 0.0 ? Math.Max(unthrottled, achievedPair) : unthrottled;
            double sample = pairPeriod > 0.0 ? Math.Min(rawPeriod, pairPeriod * 2.0) : rawPeriod;

            history[historyIndex] = sample;
            historyIndex = (historyIndex + 1) % WindowSize;

            if (historyCount < WindowSize)
            {
                historyCount++;
            }

            double sum = 0.0;
            for (int i = 0; i < historyCount; i++)
            {
                sum += history[i];
            }

            double mean = sum / historyCount;
            if (pairPeriod <= 0.0)
            {
                pairPeriod = sample;
            }
            else if (sample > mean)
            {
                pairPeriod = double.Lerp(mean, sample, 0.35);
            }
            else
            {
                pairPeriod = mean;
            }
        }

        public void MarkPresent(double afterPresentSeconds, double presentCallSeconds, bool isSubFrame)
        {
            const double MaxLead = 0.02;

            using Lock.Scope _ = @lock.EnterScope();

            double clamped = Math.Clamp(presentCallSeconds, 0.0, MaxLead);
            double previousFlip = Volatile.Read(ref lastPresentSeconds);

            if (!isSubFrame)
            {
                presentLead1 = presentLead1 <= 0.0 ? clamped : double.Lerp(presentLead1, clamped, 0.25);

                if (previousFlip > 0.0)
                {
                    lastFirstHalf = afterPresentSeconds - previousFlip;
                }
            }
            else
            {
                presentLead2 = presentLead2 <= 0.0 ? clamped : double.Lerp(presentLead2, clamped, 0.25);
            }

            Volatile.Write(ref lastPresentSeconds, afterPresentSeconds);
        }

        public bool WaitForNextPresent(Stopwatch clock, double readySeconds, ulong currentId, ref ulong gpuCompletedId, bool isSubFrame, ManualResetEventSlim aborting)
        {
            const double CoarseWaitInSeconds = 0.019;
            const double SpinThresholdInSeconds = 0.0005;
            const int SleepMarginInMilliseconds = 18;

            if (aborting.IsSet)
            {
                return false;
            }

            double lastFlip = Volatile.Read(ref lastPresentSeconds);
            if (lastFlip <= 0.0)
            {
                return !aborting.IsSet;
            }

            bool nextGpuReady = Volatile.Read(ref gpuCompletedId) > currentId;
            double target = ComputeTargetPresent(lastFlip, readySeconds, nextGpuReady, isSubFrame);

            double initialRemaining = target - clock.Elapsed.TotalSeconds;
            if (initialRemaining > CoarseWaitInSeconds)
            {
                int milliseconds = (int)(initialRemaining * 1000.0) - SleepMarginInMilliseconds;
                if (milliseconds > 0 && aborting.Wait(milliseconds))
                {
                    return false;
                }
            }

            SpinWait spinWait = new();
            while (!aborting.IsSet)
            {
                if (isSubFrame && !nextGpuReady && Volatile.Read(ref gpuCompletedId) > currentId)
                {
                    nextGpuReady = true;
                    target = Math.Min(target, ComputeTargetPresent(lastFlip, readySeconds, true, true));
                }

                double remaining = target - clock.Elapsed.TotalSeconds;
                if (remaining <= 0.0)
                {
                    break;
                }

                if (remaining > SpinThresholdInSeconds)
                {
                    spinWait.SpinOnce(-1);
                }
                else
                {
                    Thread.SpinWait(64);
                }
            }

            return !aborting.IsSet;
        }

        public void Reset()
        {
            using Lock.Scope _ = @lock.EnterScope();

            Array.Clear(history);
            historyIndex = 0;
            historyCount = 0;
            latestRenderDuration = 0.0;
            latestPresentWork = 0.0;
            pairPeriod = 0.0;
            presentLead1 = 0.0;
            presentLead2 = 0.0;
            lastFirstHalf = 0.0;
            lastPairSeconds = 0.0;

            Volatile.Write(ref lastPresentSeconds, 0.0);
        }

        private double ComputeTargetPresent(double lastFlip, double readySeconds, bool nextGpuReady, bool isSubFrame)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (pairPeriod <= 0.0 && latestRenderDuration <= 0.0)
            {
                return readySeconds;
            }

            double basePeriod = pairPeriod > 0.0 ? pairPeriod : latestRenderDuration;
            double effectivePeriod = !isVerticalSyncEnabled && nextGpuReady && latestRenderDuration > 0.0 && (latestPresentWork < (latestRenderDuration * 0.85)) ? Math.Min(basePeriod, Math.Max(latestRenderDuration, latestPresentWork)) : basePeriod;

            double half = effectivePeriod * 0.5;
            if (!isVerticalSyncEnabled && isSubFrame && (lastFirstHalf > half))
            {
                half = double.Lerp(half, Math.Min(lastFirstHalf, half * 1.3), 0.40);
            }

            double rawLead = (isSubFrame ? presentLead2 : presentLead1) + (isVerticalSyncEnabled ? 0.0005 : 0.0);
            double maxLeadFactor = isVerticalSyncEnabled ? 0.45 : 0.55;
            double lead = Math.Min(rawLead, half * maxLeadFactor);
            double minEntry = lastFlip + Math.Max((half * 0.85) - lead, half * 0.45);
            double idealEntry = Math.Max(lastFlip + (half - lead), minEntry);

            if (isSubFrame)
            {
                return idealEntry;
            }

            if (readySeconds >= idealEntry)
            {
                return readySeconds;
            }

            double gain = isVerticalSyncEnabled ? 0.25 : 0.15;

            return Math.Max(idealEntry - ((idealEntry - readySeconds) * gain), minEntry);
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

    private readonly struct Submission(ulong id, Frame frame, TimelineValue wait, TextureLayout layout, bool interpolated)
    {
        public readonly ulong Id = id;

        public readonly Frame Frame = frame;

        public readonly TimelineValue Wait = wait;

        public readonly TextureLayout Layout = layout;

        public readonly bool Interpolated = interpolated;
    }
}
