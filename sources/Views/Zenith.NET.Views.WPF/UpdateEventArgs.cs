namespace Zenith.NET.Views.WPF;

public class UpdateEventArgs(double delta) : EventArgs
{
    public double Delta { get; } = delta;
}
