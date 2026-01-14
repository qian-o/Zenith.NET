using System.Diagnostics;
using SystemBuffer = System.Buffer;

namespace Zenith.NET.Views;

public class ViewDispatcher(IZenithView view)
{
    private static readonly double PresentInterval;

    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? cancellationTokenSource;
    private Task? task;

    static ViewDispatcher()
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

        PresentInterval = double.Lerp(maxInterval, minInterval, performanceScore);
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

        cancellationTokenSource = new();
        task = Task.Run(() =>
        {
            try
            {
                GraphicsContext? currentGraphicsContext = null;
                double lastPresentTime = 0;

                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    if (view.GraphicsContext != currentGraphicsContext)
                    {
                        view.ReleaseResources();

                        currentGraphicsContext = view.GraphicsContext;
                    }

                    view.UI(view.EnsureResources);

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    view.Tick();

                    double currentTime = lifetimeStopwatch.Elapsed.TotalSeconds;

                    if (currentTime - lastPresentTime >= PresentInterval)
                    {
                        view.UI(view.Present);

                        lastPresentTime = currentTime;
                    }
                }
            }
            finally
            {
                view.ReleaseResources();
            }
        });
    }

    public async Task StopAsync()
    {
        cancellationTokenSource?.Cancel();

        await (task ?? Task.CompletedTask);

        updateStopwatch.Reset();
        renderStopwatch.Reset();
        lifetimeStopwatch.Reset();

        task = null;

        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }
}
