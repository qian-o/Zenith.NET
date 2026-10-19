using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Buffer = Zenith.NET.Buffer;

namespace Zenith.NET.Helpers;

/// <summary>Captures frame groups and presents them on a dedicated thread.</summary>
/// <remarks>
/// This source file only depends on Zenith.NET and can be copied into other projects.
/// The presenter owns its swap chain and captured textures. Dispose it before the graphics context.
/// A capture or presentation failure stops the presenter and is rethrown by subsequent Present or Resize calls.
/// </remarks>
public sealed unsafe class FramePresenter : DisposableObject
{
    private const int IntervalCount = 8;

    private readonly SwapChain swapChain;
    private readonly Slot[] slots = [];
    private readonly Queue<Slot> freeSlots;
    private readonly Queue<long> intervalsInTicks = new(IntervalCount + 1);
    private readonly Stopwatch framerateWatch = new();
    private readonly Lock apiLock = new();
    private readonly object sync = new();
    private readonly Thread? thread;

    private ExceptionDispatchInfo? failure;
    private Slot? pendingSlot;
    private bool isPresenting;
    private bool isDisposing;
    private long arrivalTicks;
    private long stalledTicks;
    private long intervalSumInTicks;
    private long presentStartTicks;
    private long presentEndTicks;
    private long displayIntervalTicks;
    private long sleepOverheadTicks;
    private int framerateCount;
    private double framerate;

    public FramePresenter(GraphicsContext context, SwapChainDesc desc)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            swapChain = context.CreateSwapChain(desc);

            // One slot is being presented while the other is being captured or waiting to be presented.
            slots = new Slot[2];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new(context);
            }

            freeSlots = new(slots);

            Thread worker = new(Run)
            {
                Name = "Frame Presenter",
                IsBackground = true
            };

            worker.Start();
            thread = worker;
        }
        catch
        {
            Dispose();

            throw;
        }
    }

    /// <summary>Gets the rate of completed Present calls, not measured display scanouts.</summary>
    public double Framerate => Volatile.Read(ref framerate);

    /// <summary>Captures generated frames in display order, followed by the real frame.</summary>
    /// <remarks>
    /// Textures must belong to the same context, match the swap chain's size and format,
    /// and be single-sampled 2D textures with TransferSrc usage and CopySrc layout.
    /// The optional disable buffer must have TransferSrc usage and at least four bytes;
    /// a nonzero first byte skips the generated frames. Dependencies are waited on before capture.
    /// On return the source resources may be reused; presentation may still be pending.
    /// </remarks>
    public void Present(Texture color, ReadOnlySpan<Texture> generatedColors = default, Buffer? disableInterpolation = null, params ReadOnlySpan<TimelineValue> waits)
    {
        using Lock.Scope _ = apiLock.EnterScope();

        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(color);
        ThrowIfFaulted();

        Slot slot = AcquireSlot();

        try
        {
            slot.Capture(color, generatedColors, disableInterpolation, waits);
            slot.IntervalTicks = MeasureInterval();

            QueueSlot(slot);
        }
        catch (Exception exception)
        {
            RecordFailure(exception);

            throw;
        }
    }

    public void Resize(uint width, uint height)
    {
        using Lock.Scope _ = apiLock.EnterScope();

        ObjectDisposedException.ThrowIf(IsDisposed, this);
        WaitIdle();

        try
        {
            swapChain.Resize(width, height);
        }
        catch (Exception exception)
        {
            RecordFailure(exception);

            throw;
        }

        intervalsInTicks.Clear();
        intervalSumInTicks = 0;
        arrivalTicks = 0;
        stalledTicks = 0;
        presentStartTicks = 0;
        presentEndTicks = 0;
        displayIntervalTicks = 0;
        sleepOverheadTicks = 0;
    }

    protected override void Destroy()
    {
        using Lock.Scope _ = apiLock.EnterScope();

        lock (sync)
        {
            isDisposing = true;

            Monitor.PulseAll(sync);
        }

        thread?.Join();

        foreach (Slot slot in slots)
        {
            slot?.Dispose();
        }

        swapChain?.Dispose();
    }

    private void WaitIdle()
    {
        lock (sync)
        {
            while (pendingSlot is not null || isPresenting)
            {
                ThrowIfFaulted();
                Monitor.Wait(sync);
            }

            ThrowIfFaulted();
        }
    }

    private Slot AcquireSlot()
    {
        long startTicks = Stopwatch.GetTimestamp();

        lock (sync)
        {
            while (pendingSlot is not null || freeSlots.Count is 0)
            {
                ThrowIfFaulted();
                Monitor.Wait(sync);
            }

            ThrowIfFaulted();
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            stalledTicks += Stopwatch.GetTimestamp() - startTicks;

            return freeSlots.Dequeue();
        }
    }

    private void QueueSlot(Slot slot)
    {
        lock (sync)
        {
            ThrowIfFaulted();
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
            catch (Exception exception)
            {
                RecordFailure(exception);

                return;
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
            while (pendingSlot is null && !isDisposing && failure is null)
            {
                Monitor.Wait(sync);
            }

            if (failure is not null)
            {
                return null;
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

    private void RecordFailure(Exception exception)
    {
        lock (sync)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
            pendingSlot = null;

            Monitor.PulseAll(sync);
        }
    }

    private void ThrowIfFaulted()
    {
        Volatile.Read(ref failure)?.Throw();
    }

    private void PresentSlot(Slot slot)
    {
        ReadOnlySpan<Texture> frames = slot.Frames;
        long stepTicks = Math.Max(slot.IntervalTicks / frames.Length, displayIntervalTicks);
        bool isDisplayBound = false;

        for (int i = 0; i < frames.Length; i++)
        {
            ThrowIfFaulted();

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

    private void WaitUntil(long ticks)
    {
        // Age out scheduling stalls even when recent deadlines needed no blocking wait.
        sleepOverheadTicks -= sleepOverheadTicks / IntervalCount;

        while (Stopwatch.GetTimestamp() < ticks)
        {
            ThrowIfFaulted();

            double remainingMilliseconds = (ticks - Stopwatch.GetTimestamp() - sleepOverheadTicks) * 1000.0 / Stopwatch.Frequency;
            int milliseconds = (int)Math.Clamp(remainingMilliseconds, 0, int.MaxValue);
            if (milliseconds > 1)
            {
                lock (sync)
                {
                    ThrowIfFaulted();
                    remainingMilliseconds = (ticks - Stopwatch.GetTimestamp() - sleepOverheadTicks) * 1000.0 / Stopwatch.Frequency;
                    milliseconds = (int)Math.Clamp(remainingMilliseconds, 0, int.MaxValue);

                    // Leave a millisecond tail in addition to the measured timeout overhead.
                    if (milliseconds > 1)
                    {
                        milliseconds--;
                        long startTicks = Stopwatch.GetTimestamp();

                        if (!Monitor.Wait(sync, milliseconds))
                        {
                            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                            long requestedTicks = (long)(milliseconds * (double)Stopwatch.Frequency / 1000.0);
                            sleepOverheadTicks = Math.Max(sleepOverheadTicks, elapsedTicks - requestedTicks);
                        }

                        continue;
                    }
                }
            }

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

    private class Slot : DisposableObject
    {
        private readonly GraphicsContext context;
        private readonly Buffer readback;

        private Texture[] textures = [];
        private int generatedCount;

        public Slot(GraphicsContext context)
        {
            this.context = context;
            readback = context.CreateBuffer(new()
            {
                SizeInBytes = sizeof(uint),
                Usages = BufferUsages.TransferDst,
                Residency = MemoryResidency.CpuReadOnly
            });
        }

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

            textures = new Texture[count];
            try
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    textures[i] = context.CreateTexture(stagingDesc);
                }
            }
            catch
            {
                DestroyTextures();

                throw;
            }
        }

        private void DestroyTextures()
        {
            foreach (Texture texture in textures)
            {
                texture?.Dispose();
            }

            textures = [];
        }
    }
}
