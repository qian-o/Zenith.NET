using System.Diagnostics;

namespace Zenith.NET.Views;

public class FrameDispatcher(IZenithView view)
{
    private const double PresentInterval = 1.0 / 60.0;

    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? cancellationTokenSource;
    private Task? task;

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
