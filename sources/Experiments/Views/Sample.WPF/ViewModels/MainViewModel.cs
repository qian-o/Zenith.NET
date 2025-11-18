using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zenith.NET;
using Zenith.NET.Views.WPF;

namespace Sample.WPF.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    [RelayCommand]
    private void Update(UpdateEventArgs args)
    {
        // Method implementation
    }

    [RelayCommand]
    private void Render(RenderEventArgs args)
    {
        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        commandBuffer.BindFrameBuffer(args.FrameBuffer, ClearValues.Default);
        commandBuffer.Submit();

        App.Context.Graphics.WaitIdle();
    }
}
