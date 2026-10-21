using System.Diagnostics;
using Zenith.NET;

namespace CornellBox;

internal class FramePresenter : DisposableObject
{
    private readonly GraphicsContext context;
    private readonly PixelFormat format;
    private readonly SwapChain swapChain;
    private readonly Lock @lock = new();
    private readonly AutoResetEvent renderWake = new(false);
    private readonly AutoResetEvent presentWake = new(false);
    private readonly ManualResetEventSlim presentPaused = new(false);
    private readonly Queue<Slot> queued = new(3);

    private Slot[] slots = [];
    private Thread? renderThread;
    private Thread? presentThread;
    private bool isStopping;
    private bool isRenderFinished;
    private bool isPresentPaused;
    private bool isRendering;
    private (uint Width, uint Height)? pendingSize;
    private ulong group;

    public FramePresenter(GraphicsContext context, SwapChainDesc desc)
    {
        ArgumentOutOfRangeException.ThrowIfZero(desc.Surface.Width);
        ArgumentOutOfRangeException.ThrowIfZero(desc.Surface.Height);

        this.context = context;
        format = desc.Format;
        Width = desc.Surface.Width;
        Height = desc.Surface.Height;
        desc.IsPresentQueueEnabled = true;
        swapChain = context.CreateSwapChain(desc);

        CreateSlots();
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public event Action<double>? Update;

    public event Action<double>? Render;

    public event Action<uint, uint>? Resize;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (renderThread is not null)
        {
            return;
        }

        {
            using Lock.Scope _ = @lock.EnterScope();

            isStopping = false;
            isRenderFinished = false;
        }

        presentPaused.Reset();
        presentThread = new(PresentLoop)
        {
            Name = "CornellBox Present",
            IsBackground = true
        };
        renderThread = new(RenderLoop)
        {
            Name = "CornellBox Render",
            IsBackground = true
        };
        presentThread.Start();
        renderThread.Start();
    }

    public void Stop()
    {
        if (renderThread is null)
        {
            return;
        }

        if (Thread.CurrentThread == renderThread || Thread.CurrentThread == presentThread)
        {
            throw new InvalidOperationException("The presenter must be stopped by its controlling thread.");
        }

        {
            using Lock.Scope _ = @lock.EnterScope();

            isStopping = true;
        }

        renderWake.Set();
        presentWake.Set();
        renderThread.Join();
        presentThread?.Join();
        renderThread = null;
        presentThread = null;
    }

    public void RequestResize(uint width, uint height)
    {
        {
            using Lock.Scope _ = @lock.EnterScope();

            pendingSize = (width, height);
        }

        renderWake.Set();
    }

    public void Submit(Texture texture, TextureLayout currentLayout, params ReadOnlySpan<TimelineValue> waits)
    {
        if (Thread.CurrentThread != renderThread || !isRendering)
        {
            throw new InvalidOperationException("Output must be submitted from the Render callback.");
        }

        ref readonly TextureDesc desc = ref texture.Desc;
        if (texture.Context != context || desc.Type is not TextureType.Texture2D || desc.Format != format || desc.Width != Width || desc.Height != Height || desc.Depth is not 1 || desc.ArrayLayers is not 1 || desc.SampleCount is not SampleCount.Count1 || !desc.Usages.HasFlag(TextureUsages.TransferSrc))
        {
            throw new ArgumentException("The output must be a matching single-sample 2D texture with transfer source usage.", nameof(texture));
        }

        Slot slot = AcquireSlot();
        CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();
        commandBuffer.Transition(texture, default, currentLayout, TextureLayout.CopySrc);
        commandBuffer.Transition(slot.Texture, default, slot.Layout, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(texture, default, default, slot.Texture, default, default, new()
        {
            Width = Width,
            Height = Height,
            Depth = 1
        });
        commandBuffer.Transition(texture, default, TextureLayout.CopySrc, currentLayout);
        commandBuffer.Transition(slot.Texture, default, TextureLayout.CopyDst, TextureLayout.CopySrc);
        commandBuffer.Submit(waits).Wait();
        slot.Layout = TextureLayout.CopySrc;

        {
            using Lock.Scope _ = @lock.EnterScope();

            while (queued.TryPeek(out Slot? previous) && previous.Group != group)
            {
                queued.Dequeue().IsInUse = false;
            }

            slot.Group = group;
            queued.Enqueue(slot);
        }

        presentWake.Set();
    }

    protected override void Destroy()
    {
        Stop();
        DestroySlots();
        swapChain?.Dispose();
        presentPaused.Dispose();
        presentWake.Dispose();
        renderWake.Dispose();
    }

    private void RenderLoop()
    {
        long previousStart = Stopwatch.GetTimestamp();

        while (WaitForFrame())
        {
            (uint Width, uint Height)? resize;

            {
                using Lock.Scope _ = @lock.EnterScope();

                if (isStopping)
                {
                    break;
                }

                resize = pendingSize;
                pendingSize = null;
            }

            if (resize.HasValue)
            {
                ResizeResources(resize.Value);
                previousStart = Stopwatch.GetTimestamp();

                continue;
            }

            group++;
            long start = Stopwatch.GetTimestamp();
            double deltaSeconds = (double)(start - previousStart) / Stopwatch.Frequency;
            previousStart = start;
            Update?.Invoke(deltaSeconds);
            isRendering = true;
            Render?.Invoke(deltaSeconds);
            isRendering = false;
        }

        context.GraphicsQueue.Timeline.Signal().Wait();

        {
            using Lock.Scope _ = @lock.EnterScope();

            ClearQueued();
            isRenderFinished = true;
        }

        presentWake.Set();
    }

    private bool WaitForFrame()
    {
        while (true)
        {
            {
                using Lock.Scope _ = @lock.EnterScope();

                if (isStopping)
                {
                    return false;
                }

                if (pendingSize.HasValue || (!isPresentPaused && Array.Exists(slots, static slot => !slot.IsInUse)))
                {
                    return true;
                }
            }

            renderWake.WaitOne();
        }
    }

    private Slot AcquireSlot()
    {
        while (true)
        {
            {
                using Lock.Scope _ = @lock.EnterScope();

                foreach (Slot slot in slots)
                {
                    if (!slot.IsInUse)
                    {
                        slot.IsInUse = true;

                        return slot;
                    }
                }
            }

            renderWake.WaitOne();
        }
    }

    private void PresentLoop()
    {
        while (true)
        {
            Slot? slot = null;

            {
                using Lock.Scope _ = @lock.EnterScope();

                if (isRenderFinished)
                {
                    return;
                }

                if (isPresentPaused)
                {
                    presentPaused.Set();
                }
                else
                {
                    queued.TryDequeue(out slot);
                }
            }

            if (slot is null)
            {
                presentWake.WaitOne();

                continue;
            }

            Texture drawable = swapChain.Drawable;
            CommandBuffer commandBuffer = context.PresentQueue.CommandBuffer();
            commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.CopyTexture(slot.Texture, default, default, drawable, default, default, new()
            {
                Width = Width,
                Height = Height,
                Depth = 1
            });
            commandBuffer.Transition(drawable, default, TextureLayout.CopyDst, TextureLayout.Present);
            commandBuffer.Submit();
            swapChain.Present();

            {
                using Lock.Scope _ = @lock.EnterScope();

                slot.IsInUse = false;
            }

            renderWake.Set();
        }
    }

    private void ResizeResources((uint Width, uint Height) size)
    {
        presentPaused.Reset();

        {
            using Lock.Scope _ = @lock.EnterScope();

            isPresentPaused = true;
        }

        presentWake.Set();
        presentPaused.Wait();
        context.GraphicsQueue.Timeline.Signal().Wait();

        {
            using Lock.Scope _ = @lock.EnterScope();

            ClearQueued();
        }

        if (size.Width is 0 || size.Height is 0)
        {
            return;
        }

        DestroySlots();
        swapChain.Resize(size.Width, size.Height);
        Width = size.Width;
        Height = size.Height;
        CreateSlots();
        Resize?.Invoke(Width, Height);

        {
            using Lock.Scope _ = @lock.EnterScope();

            isPresentPaused = false;
        }

        presentWake.Set();
    }

    private void ClearQueued()
    {
        while (queued.TryDequeue(out Slot? slot))
        {
            slot.IsInUse = false;
        }
    }

    private void CreateSlots()
    {
        slots = new Slot[3];

        for (int i = 0; i < slots.Length; i++)
        {
            Texture texture = context.CreateTexture(TextureDesc.Texture2D(format, Width, Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
            });

            slots[i] = new(texture);
        }
    }

    private void DestroySlots()
    {
        foreach (Slot slot in slots)
        {
            slot?.Texture.Dispose();
        }

        slots = [];
    }

    private class Slot(Texture texture)
    {
        public readonly Texture Texture = texture;

        public TextureLayout Layout;

        public bool IsInUse;

        public ulong Group;
    }
}
