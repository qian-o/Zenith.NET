namespace Zenith.NET.Views;

public static class Extensions
{
    extension(MauiAppBuilder builder)
    {
        public MauiAppBuilder UseZenithView()
        {
            return builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<ZenithView, ZenithViewHandler>());
        }
    }
}
