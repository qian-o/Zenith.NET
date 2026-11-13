using System.Diagnostics;

namespace Zenith.NET.Extensions.Upscaling;

public class FrameGenerationSwapChain : DisposableObject
{
    private const int FrameCount = 3;

    private readonly SwapChain swapChain;

    private Resources? resources;
    private Presenter? presenter;

    public FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc)
    {
        if (!context.UpscalingCapabilities.FrameGenerationSupported)
        {
            throw new NotSupportedException("This device does not support the frame generation with any of the specified upscaling APIs.");
        }

        Context = context;
        Desc = desc;

        CreateResources();

        swapChain = context.CreateSwapChain(new()
        {
            Surface = desc.Surface,
            Format = desc.Format,
            IsVerticalSyncEnabled = desc.IsVerticalSyncEnabled,
            IsPresentQueueEnabled = true
        });

        CreatePresenter();
    }

    public GraphicsContext Context { get; }

    public FrameGenerationSwapChainDesc Desc { get; private set; }

    public Texture Drawable => resources!.Current.Color;

    public void Present(CommandBuffer commandBuffer, FrameGenerationSwapChainArgs args)
    {
        Frame frame = resources!.Current;
        frame.Capture(Context, commandBuffer, args);

        TimelineValue rendered = commandBuffer.Submit();

        presenter!.Submit(frame, args, rendered);
        presenter.Wait(resources.Advance());
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

        presenter!.Stop();
        presenter.Completion.GetAwaiter().GetResult();

        DestroyPresenter();
        WaitForResources();
        DestroyResources();

        swapChain.Resize(width, height);

        CreateResources();
        CreatePresenter();
    }

    public void Refresh(Surface surface)
    {
        Desc = Desc with { Surface = surface };

        presenter!.Stop();
        presenter.Completion.GetAwaiter().GetResult();

        DestroyPresenter();
        WaitForResources();
        DestroyResources();

        swapChain.Refresh(surface);

        CreateResources();
        CreatePresenter();
    }

    protected override void Destroy()
    {
        DestroyPresenter();
        WaitForResources();

        swapChain.Dispose();

        DestroyResources();
    }

    private void CreateResources()
    {
        resources = new(Context, Desc);
    }

    private void DestroyResources()
    {
        resources?.Dispose();
        resources = null;
    }

    private void CreatePresenter()
    {
        presenter = new(swapChain, resources!);
    }

    private void DestroyPresenter()
    {
        presenter?.Dispose();
        presenter = null;
    }

    private void WaitForResources()
    {
        if (resources is not null)
        {
            Context.GraphicsQueue.Timeline.Signal().Wait();
            swapChain.Queue.Timeline.Signal().Wait();
        }
    }

    private class Resources : DisposableObject
    {
        private readonly Frame[] frames = new Frame[FrameCount];

        public FrameGeneration? FrameGeneration;

        private int frameIndex;

        public Resources(GraphicsContext context, FrameGenerationSwapChainDesc desc)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = new(context, desc);
            }
        }

        public Frame Current => frames[frameIndex];

        public Frame Advance()
        {
            frameIndex = (frameIndex + 1) % frames.Length;

            return Current;
        }

        public FrameGeneration EnsureFrameGeneration(GraphicsContext context, PixelFormat format, uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight, out bool recreated)
        {
            if (FrameGeneration is not null && FrameGeneration.Desc.Format == format && FrameGeneration.Desc.RenderWidth == renderWidth && FrameGeneration.Desc.RenderHeight == renderHeight && FrameGeneration.Desc.OutputWidth == outputWidth && FrameGeneration.Desc.OutputHeight == outputHeight)
            {
                recreated = false;

                return FrameGeneration;
            }

            if (FrameGeneration is not null)
            {
                context.GraphicsQueue.Timeline.Signal().Wait();
                context.PresentQueue.Timeline.Signal().Wait();
                FrameGeneration.Dispose();
            }

            FrameGeneration = context.CreateFrameGeneration(new()
            {
                Format = format,
                RenderWidth = renderWidth,
                RenderHeight = renderHeight,
                OutputWidth = outputWidth,
                OutputHeight = outputHeight
            });

            recreated = true;

            return FrameGeneration;
        }

        protected override void Destroy()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i].Dispose();
            }

            FrameGeneration?.Dispose();
        }
    }

    private class Presenter : DisposableObject
    {
        private readonly SwapChain swapChain;
        private readonly Resources resources;
        private readonly Lock @lock = new();
        private readonly Queue<Submitted> submitteds = [with(FrameCount)];
        private readonly CancellationTokenSource tokenSource = new();
        private readonly AutoResetEvent available = new(false);
        private readonly WaitHandle[] completionHandles = new WaitHandle[2];
        private readonly WaitHandle stopping;

        public readonly Task Completion;

        private bool hasHistory;
        private long lastPresentationTimestamp;
        private double targetIntervalSeconds;

        public Presenter(SwapChain swapChain, Resources resources)
        {
            this.swapChain = swapChain;
            this.resources = resources;

            stopping = tokenSource.Token.WaitHandle;
            Completion = Task.Factory.StartNew(ProcessSubmissions, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            completionHandles[1] = ((IAsyncResult)Completion).AsyncWaitHandle;
        }

        public void Submit(Frame frame, FrameGenerationSwapChainArgs args, TimelineValue rendered)
        {
            using Lock.Scope _ = @lock.EnterScope();

            frame.Completion.Reset();
            submitteds.Enqueue(new(frame, args, rendered));
            available.Set();
        }

        public void Wait(Frame frame)
        {
            completionHandles[0] = frame.Completion;

            if (WaitHandle.WaitAny(completionHandles) is 1)
            {
                Completion.GetAwaiter().GetResult();
            }
        }

        public void Stop()
        {
            tokenSource.Cancel();
            available.Set();
            completionHandles[1].WaitOne();
        }

        protected override void Destroy()
        {
            Stop();

            submitteds.Clear();

            Completion.Dispose();
            available.Dispose();
            tokenSource.Dispose();
        }

        private void ProcessSubmissions()
        {
            Thread thread = Thread.CurrentThread;
            thread.Name ??= nameof(FrameGenerationSwapChain);
            thread.IsBackground = true;

            while (true)
            {
                available.WaitOne();

                while (!tokenSource.IsCancellationRequested && TryTake(out Submitted submitted))
                {
                    ProcessFrame(submitted);
                }

                if (tokenSource.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private bool TryTake(out Submitted submitted)
        {
            using Lock.Scope _ = @lock.EnterScope();

            return submitteds.TryDequeue(out submitted);
        }

        private void ProcessFrame(Submitted submitted)
        {
            Frame frame = submitted.Frame;
            FrameGenerationSwapChainArgs args = submitted.Args;

            FrameGeneration frameGeneration = resources.EnsureFrameGeneration(swapChain.Context,
                                                                              frame.Color.Desc.Format,
                                                                              frame.Depth!.Desc.Width,
                                                                              frame.Depth.Desc.Height,
                                                                              frame.Color.Desc.Width,
                                                                              frame.Color.Desc.Height,
                                                                              out bool recreated);

            bool reset = !hasHistory || recreated || args.Reset;
            CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

            commandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.Sampled);

            frameGeneration.Dispatch(commandBuffer, new()
            {
                Color = frame.Color,
                Depth = frame.Depth,
                MotionVectors = frame.MotionVectors!,
                Hudless = frame.ActiveHudless,
                JitterOffset = args.JitterOffset,
                View = args.View,
                Projection = args.Projection,
                DeltaTimeInSeconds = args.DeltaTimeInSeconds,
                MinLuminanceInNits = args.MinLuminanceInNits,
                MaxLuminanceInNits = args.MaxLuminanceInNits,
                Reset = reset
            });

            commandBuffer.Transition(frame.Color, default, TextureLayout.Sampled, TextureLayout.Present);

            if (!reset)
            {
                commandBuffer.Transition(frameGeneration.Output, default, TextureLayout.Sampled, TextureLayout.CopySrc);
                CopyToDrawable(commandBuffer, frameGeneration.Output);
                commandBuffer.Transition(frameGeneration.Output, default, TextureLayout.CopySrc, TextureLayout.Sampled);
                commandBuffer.Submit(submitted.Rendered);

                double elapsedSeconds = Stopwatch.GetElapsedTime(lastPresentationTimestamp).TotalSeconds;
                swapChain.Present();
                lastPresentationTimestamp = Stopwatch.GetTimestamp();
                targetIntervalSeconds = targetIntervalSeconds is 0.0 ? elapsedSeconds : (targetIntervalSeconds * 0.8) + (elapsedSeconds * 0.2);

                CommandBuffer realCommandBuffer = swapChain.Queue.CommandBuffer();
                realCommandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.CopySrc);
                CopyToDrawable(realCommandBuffer, frame.Color);
                realCommandBuffer.Transition(frame.Color, default, TextureLayout.CopySrc, TextureLayout.Present);
                realCommandBuffer.Submit().Wait();
                frame.Completion.Set();

                WaitUntil(lastPresentationTimestamp, swapChain.Desc.IsVerticalSyncEnabled ? args.DeltaTimeInSeconds * 0.5 : targetIntervalSeconds);

                if (tokenSource.IsCancellationRequested)
                {
                    return;
                }

                swapChain.Present();
                lastPresentationTimestamp = Stopwatch.GetTimestamp();
            }
            else
            {
                commandBuffer.Submit(submitted.Rendered).Wait();

                if (tokenSource.IsCancellationRequested)
                {
                    frame.Completion.Set();

                    return;
                }

                CommandBuffer realCommandBuffer = swapChain.Queue.CommandBuffer();
                realCommandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.CopySrc);
                CopyToDrawable(realCommandBuffer, frame.Color);
                realCommandBuffer.Transition(frame.Color, default, TextureLayout.CopySrc, TextureLayout.Present);
                realCommandBuffer.Submit().Wait();
                frame.Completion.Set();

                if (tokenSource.IsCancellationRequested)
                {
                    return;
                }

                swapChain.Present();
                lastPresentationTimestamp = Stopwatch.GetTimestamp();
                targetIntervalSeconds = 0.0;
            }

            hasHistory = true;
        }

        private void WaitUntil(long startTimestamp, double targetSeconds)
        {
            double remainingSeconds = targetSeconds - Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            if (remainingSeconds <= 0.0)
            {
                return;
            }

            int sleepMilliseconds = (int)((remainingSeconds - 0.016) * 1000.0);
            if (sleepMilliseconds > 0 && stopping.WaitOne(sleepMilliseconds))
            {
                return;
            }

            while (!tokenSource.IsCancellationRequested && Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds < targetSeconds)
            {
                Thread.SpinWait(64);
            }
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

        private readonly struct Submitted(Frame frame, FrameGenerationSwapChainArgs args, TimelineValue rendered)
        {
            public readonly Frame Frame = frame;

            public readonly FrameGenerationSwapChainArgs Args = args;

            public readonly TimelineValue Rendered = rendered;
        }
    }

    private class Frame(GraphicsContext context, FrameGenerationSwapChainDesc desc) : DisposableObject
    {
        public readonly ManualResetEvent Completion = new(true);

        public readonly Texture Color = context.CreateTexture(TextureDesc.Texture2D(desc.Format, desc.Surface.Width, desc.Surface.Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferSrc | TextureUsages.TransferDst
        });

        public Texture? Depth;

        public Texture? MotionVectors;

        public Texture? Hudless;

        public bool HasHudless;

        public Texture? ActiveHudless => HasHudless ? Hudless : null;

        public void Capture(GraphicsContext context, CommandBuffer commandBuffer, FrameGenerationSwapChainArgs args)
        {
            Depth = EnsureAndCopy(context, commandBuffer, args.Depth, Depth);
            MotionVectors = EnsureAndCopy(context, commandBuffer, args.MotionVectors, MotionVectors);

            if (args.Hudless is Texture hudless)
            {
                Hudless = EnsureAndCopy(context, commandBuffer, hudless, Hudless);
                HasHudless = true;
            }
            else
            {
                HasHudless = false;
            }
        }

        protected override void Destroy()
        {
            Hudless?.Dispose();
            MotionVectors?.Dispose();
            Depth?.Dispose();
            Color.Dispose();
            Completion.Dispose();
        }

        private static Texture EnsureAndCopy(GraphicsContext context, CommandBuffer commandBuffer, Texture source, Texture? target)
        {
            if (target is null || target.Desc.Format != source.Desc.Format || target.Desc.Width != source.Desc.Width || target.Desc.Height != source.Desc.Height)
            {
                if (target is not null)
                {
                    context.GraphicsQueue.Timeline.Signal().Wait();
                    context.PresentQueue.Timeline.Signal().Wait();
                    target.Dispose();
                }

                target = context.CreateTexture(TextureDesc.Texture2D(source.Desc.Format, source.Desc.Width, source.Desc.Height, 1, SampleCount.Count1) with
                {
                    Usages = TextureUsages.Sampled | TextureUsages.TransferSrc | TextureUsages.TransferDst
                });
            }

            commandBuffer.Transition(source, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(target, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.CopyTexture(source, default, default, target, default, default, new()
            {
                Width = source.Desc.Width,
                Height = source.Desc.Height,
                Depth = 1
            });
            commandBuffer.Transition(target, default, TextureLayout.CopyDst, TextureLayout.Sampled);
            commandBuffer.Transition(source, default, TextureLayout.CopySrc, TextureLayout.Sampled);

            return target;
        }
    }
}
