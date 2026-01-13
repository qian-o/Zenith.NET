using System.Diagnostics;

namespace Zenith.NET.Views;

public class FrameCoordinator(Action<double, double> render, Action present)
{
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? cancellationTokenSource;
    private AutoResetEvent? renderEvent;
    private AutoResetEvent? presentEvent;

    public void Start()
    {
        Stop();

        renderStopwatch.Start();
        lifetimeStopwatch.Start();

        cancellationTokenSource = new();
        renderEvent = new(false);
        presentEvent = new(true);

        Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                presentEvent.WaitOne();

                render(renderStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds);
                renderStopwatch.Restart();

                renderEvent.Set();

                await Task.Yield();
            }
        }, cancellationTokenSource.Token);

        Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                renderEvent.WaitOne();

                present();

                presentEvent.Set();

                await Task.Delay(16, cancellationTokenSource.Token);
            }
        }, cancellationTokenSource.Token);
    }

    public void Stop()
    {
        renderStopwatch.Reset();
        lifetimeStopwatch.Reset();

        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();

        renderEvent?.Close();
        renderEvent?.Dispose();

        presentEvent?.Close();
        presentEvent?.Dispose();
    }
}
