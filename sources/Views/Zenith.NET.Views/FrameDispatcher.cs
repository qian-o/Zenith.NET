using System.Diagnostics;
using SystemBuffer = System.Buffer;

namespace Zenith.NET.Views;

public class FrameDispatcher(IZenithView view)
{
    private static readonly double PresentInterval;

    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? cancellationTokenSource;
    private Task? task;

    static FrameDispatcher()
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

    public void Start()
    {
        Stop();

        updateStopwatch.Start();
        renderStopwatch.Start();
        lifetimeStopwatch.Start();

        cancellationTokenSource = new();
        task = Task.Run(async () =>
        {
            double lastPresentTime = 0;

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                view.UI(view.EnsureResources);

                view.Frame();

                double currentTime = lifetimeStopwatch.Elapsed.TotalSeconds;

                if (currentTime - lastPresentTime >= PresentInterval)
                {
                    view.UI(view.Present);

                    lastPresentTime = currentTime;
                }

                await Task.Yield();
            }
        });
    }

    public void Stop()
    {
        updateStopwatch.Reset();
        renderStopwatch.Reset();
        lifetimeStopwatch.Reset();

        cancellationTokenSource?.Cancel();

        task?.Wait(TimeSpan.FromSeconds(2));
        task = null;

        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }
}
