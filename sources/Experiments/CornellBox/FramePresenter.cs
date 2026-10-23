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
    }

    public void RequestResize(uint width, uint height)
    {
        using Lock.Scope _ = @lock.EnterScope();

        requestedSize = (width, height);
        renderWake.Set();
    }

    public void Submit(Texture texture, TextureLayout currentLayout)
    {
        if (batch.Count == batch.Textures.Count)
        {
            batch.Textures.Add(context.CreateTexture(TextureDesc.Texture2D(swapChain.Desc.Format, Width, Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
            }));
        }

        Texture slot = batch.Textures[batch.Count++];

        copies.Transition(texture, default, currentLayout, TextureLayout.CopySrc);
        CopyFrame(copies, texture, slot, TextureLayout.CopySrc);
        copies.Transition(texture, default, TextureLayout.CopySrc, currentLayout);
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

        foreach (FrameBatch frame in queuedBatches.GetConsumingEnumerable())
        {
            long intervalTicks = frame.Count > 1 ? frame.RenderTicks / frame.Count : 0;

            for (int i = 0; i < frame.Count && !isStopping; i++)
            {
                CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

                CopyFrame(commandBuffer, frame.Textures[i], swapChain.Drawable, TextureLayout.Present);
                commandBuffer.Submit().Wait();

                WaitForPresent(previousPresent + intervalTicks);

                if (isStopping)
                {
                    break;
                }

                previousPresent = Stopwatch.GetTimestamp();

                swapChain.Present();
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
        public readonly List<Texture> Textures = [];

        public int Count;

        public long RenderTicks;

        protected override void Destroy()
        {
            for (int i = Textures.Count - 1; i >= 0; i--)
            {
                Textures[i].Dispose();
            }
            Textures.Clear();
        }
    }
}
