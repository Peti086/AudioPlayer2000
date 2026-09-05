using AudioPlayer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AudioPlayer;

public partial class PlayStatus : UserControl
{
    public PlayStatus()
    {
        InitializeComponent();

        PositionSlider.AddHandler(InputElement.PointerPressedEvent, Slider_PointerPressed, RoutingStrategies.Tunnel);
        PositionSlider.AddHandler(InputElement.PointerReleasedEvent, Slider_PointerReleased, RoutingStrategies.Tunnel);
    }

    private void Slider_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.BeginSeek();
        }
    }

    private void Slider_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is Avalonia.Controls.Slider slider)
        {
            vm.EndSeek(slider.Value);
        }
    }
}