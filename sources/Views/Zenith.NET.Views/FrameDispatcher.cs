using System.Diagnostics;

namespace Zenith.NET.Views;

public class FrameDispatcher(IZenithView view, Action<Action> dispatcher)
{
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private CancellationTokenSource? cancellationTokenSource;
    private AutoResetEvent? renderEvent;
    private AutoResetEvent? presentEvent;

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

                dispatcher(view.PrepareFrame);

                view.Render();

                renderEvent.Set();

                await Task.Yield();
            }
        }, cancellationTokenSource.Token);

        Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                renderEvent.WaitOne();

                dispatcher(view.Present);

                presentEvent.Set();

                await Task.Yield();
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
