using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OverlayWindow_SourceInitialized;
    }

    public void Update(
        IReadOnlyList<ControllerDevice> controllers,
        string shortcut,
        OverlayPosition position)
    {
        ShortcutHint.Text = $"{shortcut} to close";
        ControllerRows.Children.Clear();
        EmptyText.Visibility = controllers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var controller in controllers)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(43, 39, 59)),
                BorderBrush = ProfileBrush(controller),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Child = MainWindow.CreateControllerFamilyIcon(controller)
            });

            var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            details.Children.Add(new TextBlock
            {
                Text = controller.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            details.Children.Add(new TextBlock
            {
                Text = $"{controller.Connection} • {controller.Kind}",
                Foreground = new SolidColorBrush(Color.FromRgb(153, 149, 170)),
                FontSize = 12,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(details, 1);
            row.Children.Add(details);

            var battery = new TextBlock
            {
                Text = FormatBattery(controller),
                Foreground = BatteryBrush(controller),
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0)
            };
            Grid.SetColumn(battery, 2);
            row.Children.Add(battery);
            ControllerRows.Children.Add(row);
        }

        UpdateLayout();
        PositionOnScreen(position);
    }

    private void PositionOnScreen(OverlayPosition position)
    {
        DisplayPlacementService.PositionTopmost(this, position);
        DisplayPlacementService.ScheduleTopmost(this, position);
    }

    private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle,
            new IntPtr(styles | WsExToolWindow | WsExNoActivate));
    }

    private static string FormatBattery(ControllerDevice controller) => controller.BatteryPercent is { } percent
        ? $"{percent}%"
        : controller.BatteryLevel.ToString();

    private static Brush BatteryBrush(ControllerDevice controller) =>
        controller.BatteryLevel == BatteryLevel.Unknown ? new SolidColorBrush(Color.FromRgb(109, 105, 128)) :
        controller.IsCharging ? new SolidColorBrush(Color.FromRgb(96, 211, 148)) :
        controller.BatteryLevel is BatteryLevel.Empty or BatteryLevel.Low
            ? new SolidColorBrush(Color.FromRgb(255, 107, 107))
            : ProfileBrush(controller);

    private static Brush ProfileBrush(ControllerDevice controller)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(
                controller.AccentColor ?? ControllerProfile.DefaultAccentColor)!;
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Color.FromRgb(169, 156, 248));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr window, int index);

    private static IntPtr GetWindowLongPtr(IntPtr window, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(IntPtr window, int index, IntPtr value);

    private static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : SetWindowLong32(window, index, value);
}
