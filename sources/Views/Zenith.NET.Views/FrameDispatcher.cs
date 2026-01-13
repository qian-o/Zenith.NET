using System.Diagnostics;

namespace Zenith.NET.Views;

public class FrameDispatcher(Action frame, Action present)
{
    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? cancellationTokenSource;
    private AutoResetEvent? frameEvent;
    private AutoResetEvent? presentEvent;

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
        frameEvent = new(false);
        presentEvent = new(true);

        Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                presentEvent.WaitOne();

                frame();

                frameEvent.Set();

                await Task.Yield();
            }
        }, cancellationTokenSource.Token);

        Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                frameEvent.WaitOne();

                present();

                presentEvent.Set();

                await Task.Yield();
            }
        }, cancellationTokenSource.Token);
    }

    public void Stop()
    {
        updateStopwatch.Reset();
        renderStopwatch.Reset();
        lifetimeStopwatch.Reset();

        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();

        frameEvent?.Close();
        frameEvent?.Dispose();

        presentEvent?.Close();
        presentEvent?.Dispose();
    }
}
