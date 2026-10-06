using System.Collections.Concurrent;
using System.Diagnostics;
using Zenith.NET;

namespace CornellBox.Helpers;

internal sealed class FramePresenter : IDisposable
{
    private const int IntervalCount = 8;

    private readonly SwapChain swapChain;
    private readonly Task[] slots;
    private readonly BlockingCollection<Batch> batches;
    private readonly Queue<long> timestamps = [];
    private readonly Stopwatch framerateWatch = new();
    private readonly Thread thread;

    private int framerateCount;
    private long interval;

    public FramePresenter(SwapChain swapChain, int slotCount)
    {
        this.swapChain = swapChain;

        slots = [.. Enumerable.Repeat(Task.CompletedTask, slotCount)];
        batches = new(slotCount);
        thread = new(Run)
        {
            Name = "Frame Presenter",
            IsBackground = true
        };

        thread.Start();
    }

    public double Framerate { get; private set; }

    public void Wait(int slot)
    {
        slots[slot].Wait();
    }

    public void Present(int slot, Texture real, Texture? generated, TimelineValue value)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        slots[slot] = completion.Task;
        batches.Add(new(real, generated, value, completion));
    }

    public void Drain()
    {
        Task.WaitAll(slots);
    }

    public void Dispose()
    {
        batches.CompleteAdding();
        thread.Join();
        batches.Dispose();
    }

    private void Run()
    {
        foreach (Batch batch in batches.GetConsumingEnumerable())
        {
            if (batch.Generated is not null)
            {
                Present(batch.Generated, batch.Value);

                long deadline = Stopwatch.GetTimestamp() + (interval / 2);

                while (Stopwatch.GetTimestamp() < deadline)
                {
                    Thread.Yield();
                }
            }

            Present(batch.Real, batch.Value);

            long now = Stopwatch.GetTimestamp();

            timestamps.Enqueue(now);

            if (timestamps.Count > IntervalCount + 1)
            {
                timestamps.Dequeue();
            }

            interval = Math.Min((now - timestamps.Peek()) / Math.Max(timestamps.Count - 1, 1), Stopwatch.Frequency / 10);

            batch.Completion.SetResult();
        }
    }

    private void Present(Texture texture, TimelineValue value)
    {
        Texture drawable = swapChain.Drawable;

        CommandBuffer commandBuffer = swapChain.Queue.CommandBuffer();

        commandBuffer.Transition(drawable, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commandBuffer.CopyTexture(texture, default, default, drawable, default, default, new()
        {
            Width = texture.Desc.Width,
            Height = texture.Desc.Height,
            Depth = 1
        });
        commandBuffer.Transition(drawable, default, TextureLayout.CopyDst, TextureLayout.Present);
        commandBuffer.Submit(value);

        swapChain.Present();

        if (!framerateWatch.IsRunning)
        {
            framerateWatch.Start();
        }

        framerateCount++;

        if (framerateWatch.Elapsed.TotalSeconds >= 1.0)
        {
            Framerate = framerateCount / framerateWatch.Elapsed.TotalSeconds;

            framerateCount = 0;
            framerateWatch.Restart();
        }
    }

    private sealed record Batch(Texture Real, Texture? Generated, TimelineValue Value, TaskCompletionSource Completion);
}
