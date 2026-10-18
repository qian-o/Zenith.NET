using System.Diagnostics;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Helpers;

internal unsafe class FramePresenter : DisposableObject
{
    private const int IntervalCount = 8;

    private readonly SwapChain swapChain;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Queue<long> timestampsInTicks = new(IntervalCount + 1);
    private readonly Stopwatch framerateWatch = new();
    private readonly ManualResetEventSlim pending = new();
    private readonly ManualResetEventSlim sourcesCopied = new(initialState: true);
    private readonly ManualResetEventSlim idle = new(initialState: true);
    private readonly Thread? thread;

    private TimelineValue[] waits = new TimelineValue[4];
    private Texture? color;
    private Texture? generatedColor;
    private Buffer? interpolationStatus;
    private int waitCount;
    private int framerateCount;
    private long intervalTicks;
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

        thread = new(Run)
        {
            Name = "Frame Presenter",
            IsBackground = true
        };

        thread.Start();
    }

    public double Framerate => Volatile.Read(ref framerate);

    public void Present(Texture color, Texture? generatedColor, Buffer? interpolationStatus, params ReadOnlySpan<TimelineValue> waits)
    {
        idle.Wait();

        if (this.waits.Length < waits.Length)
        {
            this.waits = new TimelineValue[waits.Length];
        }

        waits.CopyTo(this.waits);
        waitCount = waits.Length;
        this.color = color;
        this.generatedColor = generatedColor;
        this.interpolationStatus = interpolationStatus;

        idle.Reset();
        sourcesCopied.Reset();
        pending.Set();
        sourcesCopied.Wait();
    }

    public void WaitIdle()
    {
        idle.Wait();
    }

    public void Resize(uint width, uint height)
    {
        WaitIdle();
        swapChain.Resize(width, height);
    }

    protected override void Destroy()
    {
        WaitIdle();
        cancellation.Cancel();
        pending.Set();
        thread?.Join();

        swapChain?.Dispose();
        idle.Dispose();
        sourcesCopied.Dispose();
        pending.Dispose();
        cancellation.Dispose();
    }

    private void Run()
    {
        while (true)
        {
            pending.Wait();
            pending.Reset();

            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            try
            {
                PresentFrame();
            }
            finally
            {
                color = null;
                generatedColor = null;
                interpolationStatus = null;
                Array.Clear(waits, 0, waitCount);
                waitCount = 0;
                sourcesCopied.Set();
                idle.Set();
            }
        }
    }

    private void PresentFrame()
    {
        ReadOnlySpan<TimelineValue> dependencies = waits.AsSpan(0, waitCount);
        long deadlineTicks = 0;

        if (generatedColor is Texture generated && interpolationStatus is Buffer status)
        {
            foreach (TimelineValue dependency in dependencies)
            {
                dependency.Wait();
            }

            uint* pointer = (uint*)status.Map();
            bool canInterpolate = *pointer is 0;
            status.Unmap();

            if (canInterpolate)
            {
                CopyToDrawable(generated, dependencies);
                PresentDrawable();

                deadlineTicks = Stopwatch.GetTimestamp() + (intervalTicks / 2);
            }
        }

        CopyToDrawable(color!, dependencies);
        sourcesCopied.Set();

        while (Stopwatch.GetTimestamp() < deadlineTicks)
        {
            Thread.Yield();
        }

        PresentDrawable();

        long nowTicks = Stopwatch.GetTimestamp();

        if (timestampsInTicks.Count > IntervalCount)
        {
            timestampsInTicks.Dequeue();
        }

        timestampsInTicks.Enqueue(nowTicks);
        intervalTicks = Math.Min((nowTicks - timestampsInTicks.Peek()) / Math.Max(timestampsInTicks.Count - 1, 1), Stopwatch.Frequency / 10);
    }

    private void CopyToDrawable(Texture color, ReadOnlySpan<TimelineValue> waits)
    {
        Texture drawable = swapChain.Drawable;
        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(color, default, default, drawable, default, default, new()
        {
            Width = color.Desc.Width,
            Height = color.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(drawable, default, TextureLayout.CopyDst, TextureLayout.Present);
        commandBuffer.Submit(waits).Wait();
    }

    private void PresentDrawable()
    {
        swapChain.Present();

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
}
