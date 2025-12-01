namespace Zenith.NET.Views;

public class UpdateEventArgs(double deltaSeconds, double totalSeconds) : EventArgs
{
    public double DeltaSeconds { get; } = deltaSeconds;

    public double TotalSeconds { get; } = totalSeconds;
}
