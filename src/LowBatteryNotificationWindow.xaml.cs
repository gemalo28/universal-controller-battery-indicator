using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery;

public partial class LowBatteryNotificationWindow : Window
{
    private readonly DispatcherTimer _dismissTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    private bool _closing;

    public LowBatteryNotificationWindow()
    {
        InitializeComponent();
        _dismissTimer.Tick += (_, _) => BeginClose();
    }

    public void ShowAlert(IReadOnlyList<ControllerDevice> controllers, OverlayPosition position)
    {
        var first = controllers[0];
        MessageText.Text = controllers.Count == 1
            ? $"{first.Name} needs charging."
            : $"{controllers.Count} controllers need charging.";
        BatteryText.Text = controllers.Count == 1 ? FormatBattery(first) : "Low battery";

        _closing = false;
        _dismissTimer.Stop();
        if (!IsVisible) Show();
        UpdateLayout();
        PositionOnScreen(position);

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        _dismissTimer.Start();
    }

    private static string FormatBattery(ControllerDevice controller) =>
        controller.BatteryPercent is { } percent ? $"{percent}% remaining" :
        controller.BatteryLevel == BatteryLevel.Empty ? "Battery empty" : "Battery low";

    private void PositionOnScreen(OverlayPosition position)
    {
        DisplayPlacementService.PositionTopmost(this, position);
    }

    private void BeginClose()
    {
        if (_closing) return;
        _closing = true;
        _dismissTimer.Stop();
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) => Hide();
        BeginAnimation(OpacityProperty, fade);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => BeginClose();
}
