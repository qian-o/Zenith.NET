using System.Collections.Concurrent;
using System.Diagnostics;
using Zenith.NET;

namespace CornellBox;

internal class FramePresenter : DisposableObject
{
    private const int BatchCount = 2;

    private readonly GraphicsContext context;
    private readonly SwapChain swapChain;
    private readonly Lock @lock = new();
    private readonly AutoResetEvent renderWake = new(false);
    private readonly BlockingCollection<FrameBatch> freeBatches = [];

    private BlockingCollection<FrameBatch> queuedBatches = [];
    private FrameBatch batch = null!;
    private CommandBuffer copies = null!;
    private Thread? renderThread;
    private Thread? presentThread;
    private (uint Width, uint Height) requestedSize;
    private double framesPerSecond;
    private volatile bool isStopping;

    public FramePresenter(GraphicsContext context, SwapChainDesc desc)
    {
        this.context = context;

        swapChain = context.CreateSwapChain(desc);
        requestedSize = (Width, Height);

        CreateBatches();
    }

    public uint Width => swapChain.Desc.Surface.Width;

    public uint Height => swapChain.Desc.Surface.Height;

    public double FramesPerSecond => Volatile.Read(ref framesPerSecond);

    public event Action<double>? Update;

    public event Action<double>? Render;

    public event Action<uint, uint>? Resize;

    public void Start()
    {
        Stop();

        isStopping = false;

        queuedBatches.Dispose();
        queuedBatches = [];

        renderThread = new(RenderLoop)
        {
            Name = "CornellBox Render",
            IsBackground = true
        };
        renderThread.Start();

        presentThread = new(PresentLoop)
        {
            Name = "CornellBox Present",
            IsBackground = true
        };
        presentThread.Start();
    }

    public void Stop()
    {
        isStopping = true;
        renderWake.Set();

        renderThread?.Join();
        presentThread?.Join();

        renderThread = null;
        presentThread = null;

        Volatile.Write(ref framesPerSecond, 0.0);
    }

    public void RequestResize(uint width, uint height)
    {
        using Lock.Scope _ = @lock.EnterScope();

        requestedSize = (width, height);
        renderWake.Set();
    }

    public void Submit(ReadOnlySpan<Texture?> textures, TextureLayout currentLayout)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] is not Texture texture)
            {
                continue;
            }

            double position = (i + 1.0) / textures.Length;

            if (batch.Count == batch.Frames.Count)
            {
                batch.Frames.Add((context.CreateTexture(TextureDesc.Texture2D(swapChain.Desc.Format, Width, Height, 1, SampleCount.Count1) with
                {
                    Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
                }), position));
            }
            else
            {
                batch.Frames[batch.Count] = (batch.Frames[batch.Count].Texture, position);
            }

            Texture slot = batch.Frames[batch.Count++].Texture;

            copies.Transition(texture, default, currentLayout, TextureLayout.CopySrc);
            CopyFrame(copies, texture, slot, TextureLayout.CopySrc);
            copies.Transition(texture, default, TextureLayout.CopySrc, currentLayout);
        }
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
        long previousStart = Stopwatch.GetTimestamp();

        while (!isStopping)
        {
            (uint Width, uint Height) size;

            using (Lock.Scope _ = @lock.EnterScope())
            {
                size = requestedSize;
            }

            if (size.Width is 0 || size.Height is 0)
            {
                DiscardQueued();

                renderWake.WaitOne();

                previousStart = Stopwatch.GetTimestamp();
            }
            else if (size != (Width, Height))
            {
                ResizeResources(size.Width, size.Height);

                previousStart = Stopwatch.GetTimestamp();
            }
            else
            {
                batch = freeBatches.Take();
                if (isStopping)
                {
                    freeBatches.Add(batch);

                    break;
                }

                long start = Stopwatch.GetTimestamp();
                double deltaSeconds = Stopwatch.GetElapsedTime(previousStart, start).TotalSeconds;

                previousStart = start;

                Update?.Invoke(deltaSeconds);

                batch.Count = 0;
                copies = context.GraphicsQueue.CommandBuffer();

                Render?.Invoke(deltaSeconds);

                copies.Submit().Wait();
                batch.RenderTicks = Stopwatch.GetTimestamp() - start;

                queuedBatches.Add(batch);
            }
        }

        DiscardQueued();

        queuedBatches.CompleteAdding();
    }

    private void PresentLoop()
    {
        long previousPresent = 0;
        long start = Stopwatch.GetTimestamp();
        int count = 0;

        foreach (FrameBatch frame in queuedBatches.GetConsumingEnumerable())
        {
            double previousPosition = 0.0;
            for (int i = 0; i < frame.Count && !isStopping; i++)
            {
                (Texture texture, double position) = frame.Frames[i];
                long intervalTicks = (long)(frame.RenderTicks * (position - previousPosition));
                CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

                CopyFrame(commandBuffer, texture, swapChain.Drawable, TextureLayout.Present);
                commandBuffer.Submit().Wait();

                WaitForPresent(previousPresent + intervalTicks);

                if (isStopping)
                {
                    break;
                }

                previousPresent = Stopwatch.GetTimestamp();
                previousPosition = position;

                swapChain.Present();

                count++;

                long end = Stopwatch.GetTimestamp();
                double elapsedSeconds = Stopwatch.GetElapsedTime(start, end).TotalSeconds;
                if (elapsedSeconds >= 1.0)
                {
                    Volatile.Write(ref framesPerSecond, count / elapsedSeconds);

                    start = end;
                    count = 0;
                }
            }

            freeBatches.Add(frame);
        }
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
        DiscardQueued();
        DestroyBatches();

        swapChain.Resize(width, height);

        CreateBatches();

        Resize?.Invoke(width, height);
    }

    private void DiscardQueued()
    {
        while (queuedBatches.TryTake(out FrameBatch? frame))
        {
            freeBatches.Add(frame);
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
            freeBatches.Add(new());
        }
    }

    private void DestroyBatches()
    {
        for (int i = 0; i < BatchCount; i++)
        {
            freeBatches.Take().Dispose();
        }
    }

    private class FrameBatch : DisposableObject
    {
        public readonly List<(Texture Texture, double Position)> Frames = [];

        public int Count;

        public long RenderTicks;

        protected override void Destroy()
        {
            for (int i = Frames.Count - 1; i >= 0; i--)
            {
                Frames[i].Texture.Dispose();
            }
            Frames.Clear();
        }
    }
}
