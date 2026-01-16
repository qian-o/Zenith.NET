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
        const double minInterval = 1.0 / 120.0;
        const double maxInterval = 1.0 / 30.0;

        const int iterations = 50;
        const int bufferSize = 512 * 1024;

        byte[] source = new byte[bufferSize];
        byte[] destination = new byte[bufferSize];

        SystemBuffer.BlockCopy(source, 0, destination, 0, bufferSize);

        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            SystemBuffer.BlockCopy(source, 0, destination, 0, bufferSize);
        }

        stopwatch.Stop();

        double memoryThroughputMBps = iterations * bufferSize / (1024.0 * 1024.0) / stopwatch.Elapsed.TotalSeconds;

        double performanceScore = Math.Clamp(memoryThroughputMBps / 5000.0, 0, 1);

        Interval = TimeSpan.FromSeconds(double.Lerp(maxInterval, minInterval, performanceScore));
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
        task = new(async () =>
        {
            GraphicsContext? graphicsContext = null;

            void Frame()
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

            try
            {
                while (!tokenSource.IsCancellationRequested)
                {
                    view.UI(Frame);

                    await Task.Delay(Interval);
                }
            }
            finally
            {
                view.UI(view.ReleaseResources);
            }
        });
    }

    public async Task StopAsync()
    {
        tokenSource?.Cancel();

        await (task ?? Task.CompletedTask);

        updateStopwatch.Reset();
        renderStopwatch.Reset();
        lifetimeStopwatch.Reset();

        task?.Dispose();
        task = null;

        tokenSource?.Dispose();
        tokenSource = null;
    }
}