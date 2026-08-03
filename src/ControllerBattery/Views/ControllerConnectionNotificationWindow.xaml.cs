using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery;

public partial class ControllerConnectionNotificationWindow : Window
{
    private readonly DispatcherTimer _dismissTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _closing;

    public ControllerConnectionNotificationWindow()
    {
        InitializeComponent();
        _dismissTimer.Tick += (_, _) => BeginClose();
    }

    public void ShowChanges(IReadOnlyList<ControllerConnectionChange> changes)
    {
        var connected = changes.Count(change => change.IsConnected);
        var disconnected = changes.Count - connected;
        var one = changes.Count == 1 ? changes[0] : null;

        TitleText.Text = one is not null
            ? one.IsConnected ? "Controller connected" : "Controller disconnected"
            : connected == changes.Count ? $"{connected} controllers connected"
            : disconnected == changes.Count ? $"{disconnected} controllers disconnected"
            : "Controller status changed";
        MessageText.Text = one is not null
            ? one.Controller.Name
            : string.Join("  •  ", changes.Take(3).Select(change =>
                $"{change.Controller.Name} {(change.IsConnected ? "connected" : "disconnected")}"));

        ControllerIcon.Content = MainWindow.CreateControllerFamilyIcon(changes[0].Controller);

        _closing = false;
        _dismissTimer.Stop();
        if (!IsVisible) Show();
        UpdateLayout();
        DisplayPlacementService.PositionTopmost(this, OverlayPosition.BottomLeft);
        DisplayPlacementService.ScheduleTopmost(this, OverlayPosition.BottomLeft);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
            TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        _dismissTimer.Start();
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
