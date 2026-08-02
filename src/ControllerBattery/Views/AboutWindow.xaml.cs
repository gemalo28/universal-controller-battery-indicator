using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace ControllerBattery;

public partial class AboutWindow : Window
{
    private bool _closeAnimationRunning;
    private bool _allowClose;

    public AboutWindow()
    {
        InitializeComponent();
        var assembly = Assembly.GetExecutingAssembly();
        BuildVersionText.Text = $"v{assembly.GetName().Version?.ToString(3) ?? "unknown"}";
        SourceInitialized += AboutWindow_SourceInitialized;
        Loaded += AboutWindow_Loaded;
        Closing += AboutWindow_Closing;
    }

    private void AboutWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var darkMode = 1;
        var handle = new WindowInteropHelper(this).Handle;
        if (DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref darkMode, sizeof(int));
    }

    private void AboutWindow_Loaded(object sender, RoutedEventArgs e) =>
        AboutRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
            TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseWithAnimation();

    private void AboutWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        CloseWithAnimation();
    }

    private void CloseWithAnimation()
    {
        if (_closeAnimationRunning) return;
        _closeAnimationRunning = true;
        var fade = new DoubleAnimation(AboutRoot.Opacity, 0, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            _allowClose = true;
            Close();
        };
        AboutRoot.BeginAnimation(OpacityProperty, fade);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int value, int valueSize);
}
