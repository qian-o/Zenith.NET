using System.Windows.Controls;
using Sample.WPF.ViewModels;

namespace Sample.WPF.Views;

public partial class MainView : Page
{
    public MainView()
    {
        InitializeComponent();

        SizeChanged += (_, _) =>
        {
            MainViewModel viewModel = (MainViewModel)DataContext;
            viewModel.ActualWidth = ActualWidth;
            viewModel.ActualHeight = ActualHeight;
        };
    }
}
