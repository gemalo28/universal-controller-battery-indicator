using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using ControllerBattery.Models;
using ControllerBattery.Services;

namespace ControllerBattery;

public partial class SettingsWindow : Window
{
    private ModifierKeys _modifiers;
    private Key _key;
    private bool _capturing;
    private bool _closeAnimationRunning;
    private bool _allowClose;
    private readonly Action<OverlayPosition>? _testLowBatteryNotification;

    public SettingsWindow(
        AppSettings settings,
        Action<OverlayPosition>? testLowBatteryNotification = null)
    {
        InitializeComponent();
        _testLowBatteryNotification = testLowBatteryNotification;
        _modifiers = settings.OverlayModifiers;
        _key = settings.OverlayKey;
        PollingIntervalText.Text = settings.PollingIntervalSeconds.ToString();
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        ConnectionNotificationsCheckBox.IsChecked = settings.ShowConnectionNotifications;
        LowBatteryNotificationsCheckBox.IsChecked = settings.ShowLowBatteryNotifications;
        SetOverlayPosition(settings.OverlayPosition);
        UpdateText();
        PreviewKeyDown += SettingsWindow_PreviewKeyDown;
        SourceInitialized += SettingsWindow_SourceInitialized;
        Loaded += SettingsWindow_Loaded;
        Closing += SettingsWindow_Closing;
    }

    public AppSettings? Result { get; private set; }

    private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var darkMode = 1;
        var handle = new WindowInteropHelper(this).Handle;

        // Attribute 20 is supported by current Windows builds; 19 covers older Windows 10 builds.
        if (DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, 19, ref darkMode, sizeof(int));
        }
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fadeDuration = TimeSpan.FromMilliseconds(180);

        SettingsRoot.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, fadeDuration) { EasingFunction = easing });
    }

    private void SettingsWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;

        e.Cancel = true;
        CloseWithAnimation(false);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ChromeCloseButton_Click(object sender, RoutedEventArgs e) =>
        CloseWithAnimation(false);

    private void CloseWithAnimation(bool accepted)
    {
        if (_closeAnimationRunning) return;
        _closeAnimationRunning = true;

        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(120);
        var fade = new DoubleAnimation(SettingsRoot.Opacity, 0, duration)
        {
            EasingFunction = easing
        };
        fade.Completed += (_, _) =>
        {
            _allowClose = true;
            DialogResult = accepted;
        };

        SettingsRoot.BeginAnimation(OpacityProperty, fade);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int value, int valueSize);

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        _capturing = true;
        ShortcutText.Text = "Press shortcut now…";
        CaptureButton.Content = "Listening…";
        Keyboard.Focus(this);
    }

    private void SettingsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        TrySetShortcut(Keyboard.Modifiers, key);
    }

    internal bool TrySetShortcut(ModifierKeys modifiers, Key key)
    {
        if (modifiers == ModifierKeys.None || key is Key.Escape or Key.Tab)
        {
            ShortcutText.Text = "Include Ctrl, Alt, Shift, or Win with another key";
            return false;
        }

        _modifiers = modifiers;
        _key = key;
        _capturing = false;
        CaptureButton.Content = "Change shortcut";
        UpdateText();
        return true;
    }

    private void UpdateText() => ShortcutText.Text = AppSettings.FormatShortcut(_modifiers, _key);

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_capturing) return;
        if (!int.TryParse(PollingIntervalText.Text, out var interval) ||
            interval is < AppSettings.MinimumPollingIntervalSeconds or > AppSettings.MaximumPollingIntervalSeconds)
        {
            MessageBox.Show(this,
                $"Enter a polling interval from {AppSettings.MinimumPollingIntervalSeconds} to {AppSettings.MaximumPollingIntervalSeconds} seconds.",
                "Invalid polling interval", MessageBoxButton.OK, MessageBoxImage.Warning);
            PollingIntervalText.Focus();
            PollingIntervalText.SelectAll();
            return;
        }

        Result = new AppSettings(_modifiers, _key, interval, GetOverlayPosition(),
            StartWithWindowsCheckBox.IsChecked == true,
            ConnectionNotificationsCheckBox.IsChecked == true,
            LowBatteryNotificationsCheckBox.IsChecked == true);
        CloseWithAnimation(true);
    }

    private void SetOverlayPosition(OverlayPosition position)
    {
        TopLeftPosition.IsChecked = position == OverlayPosition.TopLeft;
        TopRightPosition.IsChecked = position == OverlayPosition.TopRight;
        BottomLeftPosition.IsChecked = position == OverlayPosition.BottomLeft;
        BottomRightPosition.IsChecked = position == OverlayPosition.BottomRight;
    }

    private OverlayPosition GetOverlayPosition() =>
        TopLeftPosition.IsChecked == true ? OverlayPosition.TopLeft :
        TopRightPosition.IsChecked == true ? OverlayPosition.TopRight :
        BottomLeftPosition.IsChecked == true ? OverlayPosition.BottomLeft :
        OverlayPosition.BottomRight;

    // Optional diagnostic callback; remove with the TEST card in SettingsWindow.xaml.
    private void TestLowBatteryButton_Click(object sender, RoutedEventArgs e) =>
        _testLowBatteryNotification?.Invoke(GetOverlayPosition());

    private async void CaptureDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureDiagnosticsButton.IsEnabled = false;
        CaptureDiagnosticsButton.Content = "Capturing…";
        try
        {
            var path = await ControllerDiagnosticsService.CaptureAsync();
            MessageBox.Show(this, $"Controller diagnostics saved to:\n\n{path}",
                "Diagnostics captured", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Diagnostics failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CaptureDiagnosticsButton.Content = "Capture controller diagnostics";
            CaptureDiagnosticsButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => CloseWithAnimation(false);
}
