using System.Diagnostics;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Helpers;

internal unsafe class FramePresenter : DisposableObject
{
    private const int IntervalCount = 8;

    private readonly SwapChain swapChain;
    private readonly Slot[] slots = [];
    private readonly Queue<Slot> freeSlots;
    private readonly Queue<long> intervalsInTicks = new(IntervalCount + 1);
    private readonly Stopwatch framerateWatch = new();
    private readonly object sync = new();
    private readonly Thread? thread;

    private Slot? pendingSlot;
    private bool isPresenting;
    private bool isDisposing;
    private long arrivalTicks;
    private long stalledTicks;
    private long intervalSumInTicks;
    private long presentStartTicks;
    private long presentEndTicks;
    private long displayIntervalTicks;
    private int framerateCount;
    private double framerate;

    public FramePresenter(GraphicsContext context, Surface surface, PixelFormat format)
    {
        swapChain = context.CreateSwapChain(new()
        {
            Surface = surface,
            Format = format,
            IsVerticalSyncEnabled = true,
            IsPresentQueueEnabled = true
        });

        // One slot is being presented while the other is being captured or waiting to be presented.
        slots = [new(context), new(context)];
        freeSlots = new(slots);

        thread = new(Run)
        {
            Name = "Frame Presenter",
            IsBackground = true
        };

        thread.Start();
    }

    public double Framerate => Volatile.Read(ref framerate);

    public void Present(Texture color, ReadOnlySpan<Texture> generatedColors, Buffer? disableInterpolation, params ReadOnlySpan<TimelineValue> waits)
    {
        Slot slot = AcquireSlot();

        slot.Capture(color, generatedColors, disableInterpolation, waits);
        slot.IntervalTicks = MeasureInterval();

        QueueSlot(slot);
    }

    public void Resize(uint width, uint height)
    {
        WaitIdle();

        swapChain.Resize(width, height);
    }

    protected override void Destroy()
    {
        WaitIdle();

        lock (sync)
        {
            isDisposing = true;

            Monitor.PulseAll(sync);
        }

        thread?.Join();

        foreach (Slot slot in slots)
        {
            slot.Dispose();
        }

        swapChain?.Dispose();
    }

    private void WaitIdle()
    {
        lock (sync)
        {
            while (pendingSlot is not null || isPresenting)
            {
                Monitor.Wait(sync);
            }
        }
    }

    private Slot AcquireSlot()
    {
        long startTicks = Stopwatch.GetTimestamp();

        lock (sync)
        {
            while (pendingSlot is not null)
            {
                Monitor.Wait(sync);
            }

            stalledTicks += Stopwatch.GetTimestamp() - startTicks;

            return freeSlots.Dequeue();
        }
    }

    private void QueueSlot(Slot slot)
    {
        lock (sync)
        {
            pendingSlot = slot;

            Monitor.PulseAll(sync);
        }
    }

    private long MeasureInterval()
    {
        long nowTicks = Stopwatch.GetTimestamp();

        // Time spent waiting for a free slot is excluded so that pacing follows the rendering cost.
        long intervalTicks = Math.Max(nowTicks - arrivalTicks - stalledTicks, 0);

        arrivalTicks = nowTicks;
        stalledTicks = 0;

        if (intervalTicks > Stopwatch.Frequency / 10)
        {
            intervalsInTicks.Clear();
            intervalSumInTicks = 0;

            return 0;
        }

        intervalsInTicks.Enqueue(intervalTicks);
        intervalSumInTicks += intervalTicks;

        if (intervalsInTicks.Count > IntervalCount)
        {
            intervalSumInTicks -= intervalsInTicks.Dequeue();
        }

        return intervalSumInTicks / intervalsInTicks.Count;
    }

    private void Run()
    {
        while (TakeSlot() is Slot slot)
        {
            try
            {
                PresentSlot(slot);
            }
            finally
            {
                ReleaseSlot(slot);
            }
        }
    }

    private Slot? TakeSlot()
    {
        lock (sync)
        {
            while (pendingSlot is null && !isDisposing)
            {
                Monitor.Wait(sync);
            }

            Slot? slot = pendingSlot;

            pendingSlot = null;
            isPresenting = slot is not null;

            Monitor.PulseAll(sync);

            return slot;
        }
    }

    private void ReleaseSlot(Slot slot)
    {
        lock (sync)
        {
            freeSlots.Enqueue(slot);
            isPresenting = false;

            Monitor.PulseAll(sync);
        }
    }

    private void PresentSlot(Slot slot)
    {
        ReadOnlySpan<Texture> frames = slot.Frames;
        long stepTicks = Math.Max(slot.IntervalTicks / frames.Length, displayIntervalTicks);
        bool isDisplayBound = false;

        for (int i = 0; i < frames.Length; i++)
        {
            // Generated frames are spread over the render interval, so even the first one waits a step after the previous present.
            long deadlineTicks = frames.Length is 1 ? 0 : presentStartTicks + stepTicks;
            long previousEndTicks = presentEndTicks;

            // A late present that still blocked on the swap chain reveals the display interval.
            if (PresentFrame(frames[i], deadlineTicks) && i is not 0)
            {
                long sampleTicks = presentEndTicks - previousEndTicks;

                displayIntervalTicks = displayIntervalTicks is 0 ? sampleTicks : displayIntervalTicks + ((sampleTicks - displayIntervalTicks) / 8);
                isDisplayBound = true;
            }
        }

        if (!isDisplayBound)
        {
            displayIntervalTicks -= displayIntervalTicks / 1024;
        }
    }

    private bool PresentFrame(Texture texture, long deadlineTicks)
    {
        long copyStartTicks = Stopwatch.GetTimestamp();
        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

        CopyTexture(commandBuffer, texture, swapChain.Drawable, TextureLayout.Present);

        commandBuffer.Submit().Wait();

        long copyEndTicks = Stopwatch.GetTimestamp();

        WaitUntil(deadlineTicks);

        presentStartTicks = Stopwatch.GetTimestamp();

        swapChain.Present();

        presentEndTicks = Stopwatch.GetTimestamp();

        UpdateFramerate();

        bool isLate = copyEndTicks >= deadlineTicks;
        bool isBlocked = copyEndTicks - copyStartTicks + (presentEndTicks - presentStartTicks) > Stopwatch.Frequency / 1000;

        return isLate && isBlocked;
    }

    private void UpdateFramerate()
    {
        if (!framerateWatch.IsRunning)
        {
            framerateWatch.Start();
        }

        framerateCount++;

        if (framerateWatch.Elapsed.TotalSeconds >= 1.0)
        {
            Volatile.Write(ref framerate, framerateCount / framerateWatch.Elapsed.TotalSeconds);

            framerateCount = 0;
            framerateWatch.Restart();
        }
    }

    private static void WaitUntil(long ticks)
    {
        while (Stopwatch.GetTimestamp() < ticks)
        {
            Thread.Yield();
        }
    }

    private static void CopyTexture(CommandBuffer commandBuffer, Texture source, Texture destination, TextureLayout layout)
    {
        commandBuffer.Transition(destination, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(source, default, default, destination, default, default, new()
        {
            Width = source.Desc.Width,
            Height = source.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(destination, default, TextureLayout.CopyDst, layout);
    }

    private class Slot(GraphicsContext context) : DisposableObject
    {
        private readonly Buffer readback = context.CreateBuffer(new()
        {
            SizeInBytes = sizeof(uint),
            Usages = BufferUsages.TransferDst,
            Residency = MemoryResidency.CpuReadOnly
        });

        private Texture[] textures = [];
        private int generatedCount;

        public long IntervalTicks { get; set; }

        public ReadOnlySpan<Texture> Frames => textures.AsSpan(textures.Length - generatedCount - 1);

        public void Capture(Texture color, ReadOnlySpan<Texture> generatedColors, Buffer? disableInterpolation, ReadOnlySpan<TimelineValue> waits)
        {
            Resize(color.Desc, generatedColors.Length + 1);

            CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();

            for (int i = 0; i < textures.Length; i++)
            {
                CopyTexture(commandBuffer, i < generatedColors.Length ? generatedColors[i] : color, textures[i], TextureLayout.CopySrc);
            }

            if (disableInterpolation is not null)
            {
                commandBuffer.CopyBuffer(disableInterpolation, 0, readback, 0, sizeof(uint));
            }

            // Once the copies complete, the caller may reuse or recreate the source resources.
            commandBuffer.Submit(waits).Wait();

            bool isInterpolationDisabled = false;

            if (disableInterpolation is not null)
            {
                isInterpolationDisabled = *(byte*)readback.Map() is not 0;

                readback.Unmap();
            }

            generatedCount = isInterpolationDisabled ? 0 : generatedColors.Length;
        }

        protected override void Destroy()
        {
            DestroyTextures();
            readback?.Dispose();
        }

        private void Resize(TextureDesc desc, int count)
        {
            if (textures.Length == count && textures[0].Desc.Width == desc.Width && textures[0].Desc.Height == desc.Height && textures[0].Desc.Format == desc.Format)
            {
                return;
            }

            DestroyTextures();

            TextureDesc stagingDesc = TextureDesc.Texture2D(desc.Format, desc.Width, desc.Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
            };

            textures = [.. Enumerable.Range(0, count).Select(_ => context.CreateTexture(stagingDesc))];
        }

        private void DestroyTextures()
        {
            foreach (Texture texture in textures)
            {
                texture.Dispose();
            }

            textures = [];
        }
    }
}
