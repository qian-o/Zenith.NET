using System.Diagnostics;

namespace Zenith.NET.Views;

public class ViewTimer
{
    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    public double TotalSeconds => lifetimeStopwatch.Elapsed.TotalSeconds;

    public void Start()
    {
        updateStopwatch.Start();
        renderStopwatch.Start();
        lifetimeStopwatch.Start();
    }

    public void Stop()
    {
        updateStopwatch.Stop();
        renderStopwatch.Stop();
        lifetimeStopwatch.Stop();
    }

    public void Reset()
    {
        updateStopwatch.Reset();
        renderStopwatch.Reset();
        lifetimeStopwatch.Reset();
    }

    public double GetAndRestartUpdate()
    {
        double seconds = updateStopwatch.Elapsed.TotalSeconds;

        updateStopwatch.Restart();

        return seconds;
    }

    public double GetAndRestartRender()
    {
        double seconds = renderStopwatch.Elapsed.TotalSeconds;

        renderStopwatch.Restart();

        return seconds;
    }
}
