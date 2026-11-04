using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zenith.NET;

namespace CornellBox;

internal class FramePresenter : DisposableObject
{
    private const int BatchCount = 2;
    private const int MaxIntervalGrowth = 2;

    private readonly SwapChain swapChain;
    private readonly Lock @lock = new();
    private readonly AutoResetEvent renderWake = new(false);
    private readonly BlockingCollection<FrameBatch> freeBatches = [];
    private readonly BlockingCollection<FrameBatch> queuedBatches = [];

    private FrameBatch? currentBatch;
    private Thread? renderThread;
    private Thread? presentThread;
    private (uint Width, uint Height) requestedSize;
    private double framesPerSecond;
    private volatile bool isStopping;

    public FramePresenter(GraphicsContext context, SwapChainDesc desc)
    {
        swapChain = context.CreateSwapChain(desc);
        requestedSize = (Width, Height);

        CreateBatches();
    }

    public uint Width => swapChain.Desc.Surface.Width;

    public uint Height => swapChain.Desc.Surface.Height;

    public double FramesPerSecond => Volatile.Read(ref framesPerSecond);

    public event Action<CommandBuffer, double>? Render;

    public event Action? Resize;

    public void Start()
    {
        renderThread = new(RenderLoop)
        {
            IsBackground = true,
            Name = "FramePresenter Render"
        };
        renderThread.Start();

        presentThread = new(PresentLoop)
        {
            IsBackground = true,
            Name = "FramePresenter Present"
        };
        presentThread.Start();
    }

    public void Stop()
    {
        isStopping = true;
        renderWake.Set();

        renderThread?.Join();
        presentThread?.Join();
    }

    public void RequestResize(uint width, uint height)
    {
        using Lock.Scope _ = @lock.EnterScope();

        requestedSize = (width, height);
        renderWake.Set();
    }

    public void Submit(CommandBuffer commandBuffer, Texture texture)
    {
        Texture frame = currentBatch!.AddFrame();

        commandBuffer.Transition(texture, default, TextureLayout.Sampled, TextureLayout.CopySrc);
        CopyFrame(commandBuffer, texture, frame, TextureLayout.CopySrc);
        commandBuffer.Transition(texture, default, TextureLayout.CopySrc, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        Stop();

        if (swapChain is not null)
        {
            DestroyBatches();

            swapChain.Dispose();
        }

        queuedBatches.Dispose();
        freeBatches.Dispose();
        renderWake.Dispose();
    }

    private void RenderLoop()
    {
        long previousStartTimestamp = Stopwatch.GetTimestamp();
        long previousIntervalTicks = 0;

        while (!isStopping)
        {
            (uint Width, uint Height) size = GetRequestedSize();
            if (size.Width is 0 || size.Height is 0)
            {
                DiscardQueuedBatches();
                renderWake.WaitOne();

                previousStartTimestamp = Stopwatch.GetTimestamp();

                continue;
            }

            if (size != (Width, Height))
            {
                ResizeResources(size.Width, size.Height);

                previousStartTimestamp = Stopwatch.GetTimestamp();

                continue;
            }

            long readyTimestamp = Stopwatch.GetTimestamp();
            FrameBatch batch = freeBatches.Take();

            if (isStopping)
            {
                freeBatches.Add(batch);

                break;
            }

            long startTimestamp = Stopwatch.GetTimestamp();
            double deltaSeconds = Stopwatch.GetElapsedTime(previousStartTimestamp, startTimestamp).TotalSeconds;
            long intervalTicks = readyTimestamp - previousStartTimestamp;

            batch.Reset(previousIntervalTicks is 0 ? intervalTicks : Math.Min(intervalTicks, previousIntervalTicks * MaxIntervalGrowth));
            previousIntervalTicks = batch.IntervalTicks;
            previousStartTimestamp = startTimestamp;

            CommandBuffer commandBuffer = swapChain.Context.GraphicsQueue.CommandBuffer();

            currentBatch = batch;
            Render?.Invoke(commandBuffer, deltaSeconds);
            currentBatch = null;

            commandBuffer.Submit().Wait();

            queuedBatches.Add(batch);
        }

        DiscardQueuedBatches();
        queuedBatches.CompleteAdding();
    }

    private void PresentLoop()
    {
        long previousPresentTimestamp = 0;
        long sampleStartTimestamp = Stopwatch.GetTimestamp();
        int presentedFrameCount = 0;

        foreach (FrameBatch batch in queuedBatches.GetConsumingEnumerable())
        {
            ReadOnlySpan<Texture> frames = batch.Frames;
            long intervalTicks = batch.IntervalTicks / frames.Length;

            for (int i = 0; i < frames.Length && !isStopping; i++)
            {
                CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

                CopyFrame(commandBuffer, frames[i], swapChain.Drawable, TextureLayout.Present);
                commandBuffer.Submit().Wait();

                WaitForPresent(previousPresentTimestamp + intervalTicks);

                if (isStopping)
                {
                    break;
                }

                previousPresentTimestamp = Stopwatch.GetTimestamp();
                swapChain.Present();

                presentedFrameCount++;

                long endTimestamp = Stopwatch.GetTimestamp();
                double elapsedSeconds = Stopwatch.GetElapsedTime(sampleStartTimestamp, endTimestamp).TotalSeconds;
                if (elapsedSeconds >= 1.0)
                {
                    Volatile.Write(ref framesPerSecond, presentedFrameCount / elapsedSeconds);

                    sampleStartTimestamp = endTimestamp;
                    presentedFrameCount = 0;
                }
            }

            freeBatches.Add(batch);
        }
    }

    private (uint Width, uint Height) GetRequestedSize()
    {
        using Lock.Scope _ = @lock.EnterScope();

        return requestedSize;
    }

    private void WaitForPresent(long timestamp)
    {
        SpinWait spin = new();
        while (!isStopping && Stopwatch.GetTimestamp() < timestamp)
        {
            spin.SpinOnce(-1);
        }
    }

    private void ResizeResources(uint width, uint height)
    {
        DiscardQueuedBatches();
        DestroyBatches();

        swapChain.Resize(width, height);

        CreateBatches();

        Resize?.Invoke();
    }

    private void DiscardQueuedBatches()
    {
        while (queuedBatches.TryTake(out FrameBatch? batch))
        {
            freeBatches.Add(batch);
        }
    }

    private void CopyFrame(CommandBuffer commandBuffer, Texture src, Texture dst, TextureLayout dstLayout)
    {
        commandBuffer.Transition(dst, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(src, default, default, dst, default, default, new()
        {
            Width = Width,
            Height = Height,
            Depth = 1
        });
        commandBuffer.Transition(dst, default, TextureLayout.CopyDst, dstLayout);
    }

    private void CreateBatches()
    {
        for (int i = 0; i < BatchCount; i++)
        {
            freeBatches.Add(new(swapChain));
        }
    }

    private void DestroyBatches()
    {
        for (int i = 0; i < BatchCount; i++)
        {
            freeBatches.Take().Dispose();
        }
    }

    private class FrameBatch(SwapChain swapChain) : DisposableObject
    {
        private readonly List<Texture> textures = [];

        private int frameCount;

        public ReadOnlySpan<Texture> Frames => CollectionsMarshal.AsSpan(textures)[..frameCount];

        public long IntervalTicks { get; private set; }

        public void Reset(long intervalTicks)
        {
            frameCount = 0;
            IntervalTicks = intervalTicks;
        }

        public Texture AddFrame()
        {
            if (frameCount == textures.Count)
            {
                textures.Add(swapChain.Context.CreateTexture(TextureDesc.Texture2D(swapChain.Desc.Format, swapChain.Desc.Surface.Width, swapChain.Desc.Surface.Height, 1, SampleCount.Count1) with
                {
                    Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
                }));
            }

            Texture frame = textures[frameCount];
            frameCount++;

            return frame;
        }

        protected override void Destroy()
        {
            for (int i = textures.Count - 1; i >= 0; i--)
            {
                textures[i].Dispose();
            }
            textures.Clear();
        }
    }
}
