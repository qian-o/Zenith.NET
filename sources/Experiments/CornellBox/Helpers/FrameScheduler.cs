using System.Collections.Concurrent;
using System.Diagnostics;
using Buffer = Zenith.NET.Buffer;

namespace Zenith.NET.Helpers;

public class FrameScheduler : DisposableObject
{
    private readonly GraphicsContext context;
    private readonly SwapChain swapChain = null!;
    private readonly Slot[] slots = new Slot[2];

    private BlockingCollection<Slot>? freeSlots;
    private BlockingCollection<Slot>? readySlots;
    private CancellationTokenSource? cancellation;
    private Thread? renderThread;
    private Thread? presentThread;
    private Slot? renderSlot;
    private long frameStartTicks;
    private long lastPresentTicks;
    private long rateStartTicks;
    private int presentCount;
    private double framerate;

    public FrameScheduler(GraphicsContext context, SwapChainDesc desc)
    {
        this.context = context;
        swapChain = context.CreateSwapChain(desc);

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new(context);
        }
    }

    public double Framerate => Volatile.Read(ref framerate);

    public event Action<double>? Update;

    public event Action<double>? Render;

    public void Start()
    {
        if (renderThread is not null || Render is null)
        {
            return;
        }

        cancellation = new();
        freeSlots = new(slots.Length);
        readySlots = new(slots.Length);

        foreach (Slot slot in slots)
        {
            freeSlots.Add(slot);
        }

        lastPresentTicks = 0;
        rateStartTicks = 0;
        presentCount = 0;
        Volatile.Write(ref framerate, 0);

        renderThread = new(RenderLoop)
        {
            Name = "Frame Scheduler - Render",
            IsBackground = true
        };
        presentThread = new(PresentLoop)
        {
            Name = "Frame Scheduler - Present",
            IsBackground = true
        };

        presentThread.Start();
        renderThread.Start();
    }

    public void Stop()
    {
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        Join(renderThread);
        readySlots?.CompleteAdding();
        Join(presentThread);
        renderThread = null;
        presentThread = null;
        renderSlot = null;

        context.GraphicsQueue.Timeline.Signal().Wait();
        swapChain.Queue.Timeline.Signal().Wait();

        readySlots?.Dispose();
        readySlots = null;
        freeSlots?.Dispose();
        freeSlots = null;
        cancellation.Dispose();
        cancellation = null;
    }

    public void Resize(uint width, uint height)
    {
        swapChain.Resize(width, height);
    }

    public void Present(Texture color, ReadOnlySpan<Texture> generatedColors = default, Buffer? disableInterpolation = null, params ReadOnlySpan<TimelineValue> waits)
    {
        Slot slot = renderSlot!;
        slot.Capture(color, generatedColors, disableInterpolation, waits);
        slot.ReadyTicks = Stopwatch.GetTimestamp();
        slot.DurationTicks = Math.Max(slot.ReadyTicks - frameStartTicks, 1);

        readySlots!.Add(slot);
        renderSlot = null;
    }

    protected override void Destroy()
    {
        Stop();

        for (int i = slots.Length - 1; i >= 0; i--)
        {
            slots[i]?.Dispose();
        }

        swapChain?.Dispose();
    }

    private void RenderLoop()
    {
        CancellationToken cancellationToken = cancellation!.Token;
        long previousStart = Stopwatch.GetTimestamp();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                renderSlot = freeSlots!.Take(cancellationToken);
                frameStartTicks = Stopwatch.GetTimestamp();
                double deltaSeconds = (frameStartTicks - previousStart) / (double)Stopwatch.Frequency;
                previousStart = frameStartTicks;

                Update?.Invoke(deltaSeconds);
                Render?.Invoke(deltaSeconds);

                if (renderSlot is not null)
                {
                    freeSlots.Add(renderSlot);
                    renderSlot = null;
                }
            }
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
        {
            // Cancellation ends the render loop after a stop request.
        }
    }

    private void PresentLoop()
    {
        foreach (Slot slot in readySlots!.GetConsumingEnumerable())
        {
            PresentSlot(slot);
            freeSlots!.Add(slot);
        }
    }

    private void PresentSlot(Slot slot)
    {
        ReadOnlySpan<Texture> frames = slot.Frames;
        long spacing = Math.Max(slot.DurationTicks / frames.Length, 1);
        for (int i = 0; i < frames.Length; i++)
        {
            long target = slot.ReadyTicks + (i * spacing);
            long expires = i == frames.Length - 1 ? long.MaxValue : target + spacing;
            PresentFrame(frames[i], target, expires, spacing);
        }
    }

    private void PresentFrame(Texture texture, long target, long expires, long spacing)
    {
        if (cancellation!.IsCancellationRequested && expires != long.MaxValue)
        {
            return;
        }

        long deadline = Math.Max(target, lastPresentTicks + spacing);
        if (Math.Max(Stopwatch.GetTimestamp(), deadline) >= expires)
        {
            return;
        }

        CommandBuffer commands = swapChain.Queue.CommandBuffer();
        CopyTexture(commands, texture, swapChain.Drawable, TextureLayout.Present);
        commands.Submit().Wait();

        WaitUntil(deadline);

        long now = Stopwatch.GetTimestamp();
        if (now >= expires || (cancellation.IsCancellationRequested && expires != long.MaxValue))
        {
            return;
        }

        lastPresentTicks = now;
        swapChain.Present();

        if (rateStartTicks is 0)
        {
            rateStartTicks = now;
        }

        presentCount++;
        long elapsed = Stopwatch.GetTimestamp() - rateStartTicks;
        if (elapsed >= Stopwatch.Frequency)
        {
            Volatile.Write(ref framerate, presentCount * (double)Stopwatch.Frequency / elapsed);
            presentCount = 0;
            rateStartTicks = Stopwatch.GetTimestamp();
        }
    }

    private void WaitUntil(long deadline)
    {
        while (!cancellation!.IsCancellationRequested)
        {
            long remaining = deadline - Stopwatch.GetTimestamp();
            if (remaining <= 0)
            {
                return;
            }

            int milliseconds = (int)Math.Min(remaining * 1000.0 / Stopwatch.Frequency, int.MaxValue);
            if (milliseconds > 0)
            {
                cancellation.Token.WaitHandle.WaitOne(milliseconds);
            }
            else
            {
                Thread.Yield();
            }
        }
    }

    private static void Join(Thread? thread)
    {
        if (thread is not null && (thread.ThreadState & System.Threading.ThreadState.Unstarted) is 0)
        {
            thread.Join();
        }
    }

    private static void CopyTexture(CommandBuffer commands, Texture src, Texture dst, TextureLayout layout)
    {
        commands.Transition(dst, default, TextureLayout.Undefined, TextureLayout.CopyDst);
        commands.CopyTexture(src, default, default, dst, default, default, new()
        {
            Width = src.Desc.Width,
            Height = src.Desc.Height,
            Depth = 1
        });
        commands.Transition(dst, default, TextureLayout.CopyDst, layout);
    }

    private class Slot : DisposableObject
    {
        private readonly GraphicsContext context;
        private readonly Buffer readback = null!;

        public long ReadyTicks;

        public long DurationTicks;

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

        public ReadOnlySpan<Texture> Frames => textures.AsSpan(textures.Length - generatedCount - 1);

        public void Capture(Texture color, ReadOnlySpan<Texture> generatedColors, Buffer? disableInterpolation, ReadOnlySpan<TimelineValue> waits)
        {
            Resize(color.Desc, generatedColors.Length + 1);

            CommandBuffer commands = context.GraphicsQueue.CommandBuffer();
            for (int i = 0; i < textures.Length; i++)
            {
                CopyTexture(commands, i < generatedColors.Length ? generatedColors[i] : color, textures[i], TextureLayout.CopySrc);
            }

            if (disableInterpolation is not null)
            {
                commands.CopyBuffer(disableInterpolation, 0, readback, 0, sizeof(uint));
            }

            commands.Submit(waits).Wait();
            generatedCount = generatedColors.Length;

            if (disableInterpolation is not null)
            {
                nint pointer = readback.Map();

                unsafe
                {
                    if (*(byte*)pointer is not 0)
                    {
                        generatedCount = 0;
                    }
                }

                readback.Unmap();
            }
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

            TextureDesc captureDesc = TextureDesc.Texture2D(desc.Format, desc.Width, desc.Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.TransferSrc | TextureUsages.TransferDst
            };
            textures = new Texture[count];

            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = context.CreateTexture(captureDesc);
            }
        }

        private void DestroyTextures()
        {
            for (int i = textures.Length - 1; i >= 0; i--)
            {
                textures[i]?.Dispose();
            }
            textures = [];
        }
    }
}
