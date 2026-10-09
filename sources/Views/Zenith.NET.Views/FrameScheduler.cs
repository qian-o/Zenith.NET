using System.Diagnostics;
using SystemBuffer = System.Buffer;

namespace Zenith.NET.Views;

public class FrameScheduler(IZenithView view)
{
    private static readonly TimeSpan Interval;

    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? tokenSource;
    private Task? task;

    static FrameScheduler()
    {
        const double MinInterval = 1.0 / 120.0;
        const double MaxInterval = 1.0 / 30.0;

        const int Iterations = 50;
        const int BufferSize = 512 * 1024;

        byte[] source = new byte[BufferSize];
        byte[] destination = new byte[BufferSize];

        SystemBuffer.BlockCopy(source, 0, destination, 0, BufferSize);

        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < Iterations; i++)
        {
            SystemBuffer.BlockCopy(source, 0, destination, 0, BufferSize);
        }

        stopwatch.Stop();

        double memoryThroughputMBps = Iterations * BufferSize / (1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;

        double performanceScore = Math.Clamp(memoryThroughputMBps / 5000.0, 0.0, 1.0);

        Interval = TimeSpan.FromSeconds(double.Lerp(MaxInterval, MinInterval, performanceScore));
    }

    public double UpdateSeconds
    {
        get
        {
            double seconds = updateStopwatch.Elapsed.TotalSeconds;

            updateStopwatch.Restart();

            return seconds;
        }
    }

    public double RenderSeconds
    {
        get
        {
            double seconds = renderStopwatch.Elapsed.TotalSeconds;

            renderStopwatch.Restart();

            return seconds;
        }
    }

    public double TotalSeconds => lifetimeStopwatch.Elapsed.TotalSeconds;

    public async Task StartAsync()
    {
        await StopAsync();

        updateStopwatch.Start();
        renderStopwatch.Start();
        lifetimeStopwatch.Start();

        tokenSource = new();
        task = Task.Run(async () =>
        {
            using ManualResetEventSlim @event = new(false);

            GraphicsContext? graphicsContext = null;

            void Frame()
            {
                try
                {
                    if (graphicsContext != view.GraphicsContext)
                    {
                        view.ReleaseResources();

                        graphicsContext = view.GraphicsContext;
                    }

                    view.EnsureResources();

                    view.Tick();

                    view.Present();
                }
                finally
                {
                    @event.Set();
                }
            }

            while (!tokenSource.IsCancellationRequested)
            {
                try
                {
                    view.UI(Frame);

                    @event.Wait(tokenSource.Token);
                    @event.Reset();

                    if (tokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.Delay(Interval, tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore other exceptions to keep the loop running.
                }
            }
        });
    }

    public async Task StopAsync()
    {
        tokenSource?.Cancel();
        await (task ?? Task.CompletedTask);

        task?.Dispose();
        task = null;

        tokenSource?.Dispose();
        tokenSource = null;

        lifetimeStopwatch.Reset();
        renderStopwatch.Reset();
        updateStopwatch.Reset();

        view.ReleaseResources();
    }
}
