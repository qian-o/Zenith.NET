using System.Collections.Concurrent;
using System.Diagnostics;
using Zenith.NET;

namespace CornellBox;

internal class FramePresenter : DisposableObject
{
    private const int SlotCount = 3;

    private readonly GraphicsContext context;
    private readonly SwapChain swapChain;
    private readonly Lock @lock = new();
    private readonly AutoResetEvent renderWake = new(false);
    private readonly BlockingCollection<Texture> freeSlots = [];

    private BlockingCollection<Texture> queuedSlots = [];
    private Thread? renderThread;
    private Thread? presentThread;
    private (uint Width, uint Height) requestedSize;
    private volatile bool isStopping;
    private bool isFirstOutput;

    public FramePresenter(GraphicsContext context, SwapChainDesc desc)
    {
        this.context = context;

        swapChain = context.CreateSwapChain(desc);
        requestedSize = (Width, Height);

        CreateSlots();
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

        queuedSlots.Dispose();
        queuedSlots = [];

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

    public void Submit(Texture texture, TextureLayout currentLayout, params ReadOnlySpan<TimelineValue> waits)
    {
        Texture slot = freeSlots.Take();

        CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(texture, default, currentLayout, TextureLayout.CopySrc);
        CopyFrame(commandBuffer, texture, slot, TextureLayout.CopySrc);
        commandBuffer.Transition(texture, default, TextureLayout.CopySrc, currentLayout);
        commandBuffer.Submit(waits).Wait();

        if (isFirstOutput)
        {
            DiscardQueued();

            isFirstOutput = false;
        }

        queuedSlots.Add(slot);
    }

    protected override void Destroy()
    {
        Stop();

        if (swapChain is not null)
        {
            DestroySlots();

            swapChain.Dispose();
        }

        queuedSlots.Dispose();
        freeSlots.Dispose();
        renderWake.Dispose();
    }

    private void RenderLoop()
    {
        long previousStart = Stopwatch.GetTimestamp();

        while (true)
        {
            // Waiting for a free slot first keeps Update from consuming input that cannot be submitted yet.
            freeSlots.Add(freeSlots.Take());

            if (isStopping)
            {
                break;
            }

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
                long start = Stopwatch.GetTimestamp();
                double deltaSeconds = Stopwatch.GetElapsedTime(previousStart, start).TotalSeconds;

                previousStart = start;

                Update?.Invoke(deltaSeconds);

                isFirstOutput = true;

                Render?.Invoke(deltaSeconds);
            }
        }

        DiscardQueued();

        queuedSlots.CompleteAdding();
    }

    private void PresentLoop()
    {
        foreach (Texture slot in queuedSlots.GetConsumingEnumerable())
        {
            CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

            CopyFrame(commandBuffer, slot, swapChain.Drawable, TextureLayout.Present);
            commandBuffer.Submit();

            swapChain.Present();

            freeSlots.Add(slot);
        }
    }

    private void ResizeResources(uint width, uint height)
    {
        DiscardQueued();
        DestroySlots();

        swapChain.Resize(width, height);

        CreateSlots();

        Resize?.Invoke(width, height);
    }

    private void DiscardQueued()
    {
        while (queuedSlots.TryTake(out Texture? slot))
        {
            freeSlots.Add(slot);
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

    private void CreateSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            Texture slot = context.CreateTexture(TextureDesc.Texture2D(swapChain.Desc.Format, Width, Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
            });

            freeSlots.Add(slot);
        }
    }

    private void DestroySlots()
    {
        // Taking back every slot waits for the current presentation and leaves the present thread idle.
        for (int i = 0; i < SlotCount; i++)
        {
            freeSlots.Take().Dispose();
        }
    }
}
