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
        Context = context;
        Desc = desc;

        CreateResources();

        swapChain = context.CreateSwapChain(new()
        {
            Surface = desc.Surface,
            Format = desc.ColorFormat,
            IsVerticalSyncEnabled = desc.IsVerticalSyncEnabled,
            IsPresentQueueEnabled = true
        });

        CreatePresenter();
    }

    public GraphicsContext Context { get; }

    public FrameGenerationSwapChainDesc Desc { get; private set; }

    public Texture Color => resources!.Current.Color;

    public Texture Depth => resources!.Current.Depth;

    public Texture MotionVectors => resources!.Current.MotionVectors;

    public Texture? Hudless => resources!.Current.Hudless;

    public void Present(TimelineValue rendered, FrameGenerationSwapChainArgs args)
    {
        Frame frame = resources!.Current;

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

        public readonly FrameGeneration FrameGeneration;

        public readonly Texture Output;

        private int frameIndex;

        public Resources(GraphicsContext context, FrameGenerationSwapChainDesc desc)
        {
            FrameGeneration = context.CreateFrameGeneration(new()
            {
                ColorFormat = desc.ColorFormat,
                DepthFormat = desc.DepthFormat,
                MotionVectorsFormat = desc.MotionVectorsFormat,
                HudlessFormat = desc.HudlessFormat,
                OutputFormat = desc.ColorFormat,
                IsHdr = desc.IsHdr,
                Width = desc.Surface.Width,
                Height = desc.Surface.Height,
                AuxiliaryWidth = desc.AuxiliaryWidth,
                AuxiliaryHeight = desc.AuxiliaryHeight,
                Flags = desc.Flags
            });

            Output = context.CreateTexture(TextureDesc.Texture2D(desc.ColorFormat, desc.Surface.Width, desc.Surface.Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.ColorAttachment | TextureUsages.Storage | TextureUsages.TransferSrc
            });

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

        protected override void Destroy()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i].Dispose();
            }

            Output.Dispose();
            FrameGeneration.Dispose();
        }
    }

    private class Presenter : DisposableObject
    {
        private readonly SwapChain swapChain;
        private readonly Resources resources;
        private readonly Lock @lock = new();
        private readonly Queue<Submitted> submitteds = new(FrameCount);
        private readonly CancellationTokenSource tokenSource = new();
        private readonly AutoResetEvent available = new(false);
        private readonly WaitHandle[] completionHandles = new WaitHandle[2];
        private readonly WaitHandle stopping;

        public readonly Task Completion;

        private bool hasHistory;
        private long lastPresentationTimestamp;

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

                while (TryTake(out Submitted submitted))
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

        private void ProcessFrame(in Submitted submitted)
        {
            Frame frame = submitted.Frame;
            ref readonly FrameGenerationSwapChainArgs args = ref submitted.Args;
            bool reset = !hasHistory || args.Reset;
            CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

            frame.Transition(commandBuffer, TextureLayout.Present, TextureLayout.Sampled);
            commandBuffer.Transition(resources.Output, default, TextureLayout.Undefined, TextureLayout.Storage);

            resources.FrameGeneration.Dispatch(commandBuffer, new()
            {
                Color = frame.Color,
                Depth = frame.Depth,
                MotionVectors = frame.MotionVectors,
                Hudless = frame.Hudless,
                Output = resources.Output,
                JitterOffset = args.JitterOffset,
                WorldToView = args.WorldToView,
                ViewToClip = args.ViewToClip,
                DeltaTimeInSeconds = args.DeltaTimeInSeconds,
                MinLuminanceInNits = args.MinLuminanceInNits,
                MaxLuminanceInNits = args.MaxLuminanceInNits,
                Reset = reset
            });

            frame.Transition(commandBuffer, TextureLayout.Sampled, TextureLayout.Present);

            if (!reset)
            {
                commandBuffer.Transition(resources.Output, default, TextureLayout.Storage, TextureLayout.CopySrc);
                CopyToDrawable(commandBuffer, resources.Output);
                commandBuffer.Transition(resources.Output, default, TextureLayout.CopySrc, TextureLayout.Present);
                commandBuffer.Submit(submitted.Rendered);

                lastPresentationTimestamp = Stopwatch.GetTimestamp();
                swapChain.Present();

                CommandBuffer realCommandBuffer = swapChain.Queue.CommandBuffer();
                realCommandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.CopySrc);
                CopyToDrawable(realCommandBuffer, frame.Color);
                realCommandBuffer.Transition(frame.Color, default, TextureLayout.CopySrc, TextureLayout.Present);
                realCommandBuffer.Submit();

                if (swapChain.Desc.IsVerticalSyncEnabled && lastPresentationTimestamp is not 0)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(args.DeltaTimeInSeconds * 0.5) - Stopwatch.GetElapsedTime(lastPresentationTimestamp);
                    if (delay > TimeSpan.Zero)
                    {
                        stopping.WaitOne(delay);
                    }
                }

                lastPresentationTimestamp = Stopwatch.GetTimestamp();
                swapChain.Present();
                frame.Completion.Set();
            }
            else
            {
                commandBuffer.Transition(resources.Output, default, TextureLayout.Storage, TextureLayout.Present);
                commandBuffer.Transition(frame.Color, default, TextureLayout.Present, TextureLayout.CopySrc);
                CopyToDrawable(commandBuffer, frame.Color);
                commandBuffer.Transition(frame.Color, default, TextureLayout.CopySrc, TextureLayout.Present);
                commandBuffer.Submit(submitted.Rendered);

                lastPresentationTimestamp = Stopwatch.GetTimestamp();
                swapChain.Present();
                frame.Completion.Set();
            }

            hasHistory = true;
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

    private class Frame : DisposableObject
    {
        public readonly ManualResetEvent Completion = new(true);

        public readonly Texture Color;

        public readonly Texture Depth;

        public readonly Texture MotionVectors;

        public readonly Texture? Hudless;

        public Frame(GraphicsContext context, FrameGenerationSwapChainDesc desc)
        {
            Color = CreateTexture(context, desc.ColorFormat, desc.Surface.Width, desc.Surface.Height);
            Depth = CreateTexture(context, desc.DepthFormat, desc.AuxiliaryWidth, desc.AuxiliaryHeight);
            MotionVectors = CreateTexture(context, desc.MotionVectorsFormat, desc.AuxiliaryWidth, desc.AuxiliaryHeight);

            if (desc.HudlessFormat is PixelFormat hudlessFormat)
            {
                Hudless = CreateTexture(context, hudlessFormat, desc.Surface.Width, desc.Surface.Height);
            }
        }

        public void Transition(CommandBuffer commandBuffer, TextureLayout before, TextureLayout after)
        {
            commandBuffer.Transition(Color, default, before, after);
            commandBuffer.Transition(Depth, default, before, after);
            commandBuffer.Transition(MotionVectors, default, before, after);

            if (Hudless is not null)
            {
                commandBuffer.Transition(Hudless, default, before, after);
            }
        }

        protected override void Destroy()
        {
            Hudless?.Dispose();
            MotionVectors.Dispose();
            Depth.Dispose();
            Color.Dispose();
            Completion.Dispose();
        }

        private static Texture CreateTexture(GraphicsContext context, PixelFormat format, uint width, uint height)
        {
            TextureUsages usages = TextureUsages.Sampled | TextureUsages.TransferSrc | TextureUsages.TransferDst;
            usages |= ZenithHelper.HasDepth(format) ? TextureUsages.DepthStencilAttachment : TextureUsages.ColorAttachment | TextureUsages.Storage;

            return context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with { Usages = usages });
        }
    }
}
