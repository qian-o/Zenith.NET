using System.Diagnostics;

namespace Zenith.NET.Views;

public class FrameDispatcher(IZenithView view)
{
    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? cancellationTokenSource;
    private AutoResetEvent? frameEvent;
    private AutoResetEvent? presentEvent;
    private Task? frameTask;
    private Task? presentTask;

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

        frameTask = Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                presentEvent.WaitOne();

                view.UI(view.Prepare);

                view.Frame();

                frameEvent.Set();

                await Task.Yield();
            }
        });

        presentTask = Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                frameEvent.WaitOne();

                view.UI(view.Present);

                presentEvent.Set();

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

        presentEvent?.Set();
        frameTask?.Wait(TimeSpan.FromSeconds(1));
        frameTask = null;

        frameEvent?.Set();
        presentTask?.Wait(TimeSpan.FromSeconds(1));
        presentTask = null;

        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;

        frameEvent?.Close();
        frameEvent?.Dispose();
        frameEvent = null;

        presentEvent?.Close();
        presentEvent?.Dispose();
        presentEvent = null;
    }
}
