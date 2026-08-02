using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ControllerBattery.Models;

namespace ControllerBattery;

public partial class ProfileWindow : Window
{
    private static readonly string[] Colors =
    [
        "#A99CF8", "#8FB8FF", "#60D394", "#F7B955",
        "#FF8A65", "#FF7D86", "#E879F9", "#D7D3E3"
    ];
    private static readonly (string Label, string? Kind)[] Icons =
    [
        ("Automatic", null), ("Xbox", "Xbox"), ("PlayStation", "PlayStation"),
        ("Nintendo", "Nintendo"), ("8BitDo", "8BitDo"), ("Generic", "Generic")
    ];

    private readonly string _deviceKey;
    private readonly ControllerDevice _controller;
    private string _selectedColor;
    private string? _selectedIconKind;
    private readonly List<Button> _colorButtons = [];
    private readonly List<Button> _iconButtons = [];
    private bool _closeAnimationRunning;
    private bool _allowClose;

    public ControllerProfile? Result { get; private set; }

    public ProfileWindow(ControllerDevice controller, ControllerProfile? profile)
    {
        InitializeComponent();
        _controller = controller;
        _deviceKey = $"{controller.ProviderId}:{controller.Id}";
        _selectedColor = profile?.AccentColor ?? ControllerProfile.DefaultAccentColor;
        _selectedIconKind = profile?.IconKind;
        DeviceDescription.Text = $"{controller.Name}  •  {controller.Connection}";
        CustomNameTextBox.Text = profile?.CustomName ?? string.Empty;

        foreach (var (label, kind) in Icons)
        {
            var button = new Button
            {
                Tag = kind ?? string.Empty,
                Style = (Style)FindResource("IconOption"),
                ToolTip = label
            };
            button.Click += Icon_Click;
            _iconButtons.Add(button);
            IconChoices.Children.Add(button);
        }

        foreach (var color in Colors)
        {
            var button = new Button
            {
                Tag = color,
                Style = (Style)FindResource("ColorOption"),
                Background = (Brush)new BrushConverter().ConvertFromString(color)!,
                BorderBrush = Brushes.White
            };
            button.Click += Color_Click;
            _colorButtons.Add(button);
            ColorChoices.Children.Add(button);
        }

        UpdateColorSelection();
        UpdateIconSelection();
        SourceInitialized += ProfileWindow_SourceInitialized;
        Loaded += ProfileWindow_Loaded;
        Closing += ProfileWindow_Closing;
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        _selectedColor = (string)((Button)sender).Tag;
        UpdateColorSelection();
    }

    private void UpdateColorSelection()
    {
        foreach (var button in _colorButtons)
        {
            var selected = string.Equals((string)button.Tag, _selectedColor, StringComparison.OrdinalIgnoreCase);
            button.BorderThickness = new Thickness(selected ? 3 : 0);
        }
        UpdateIconSelection();
    }

    private void Icon_Click(object sender, RoutedEventArgs e)
    {
        var kind = (string)((Button)sender).Tag;
        _selectedIconKind = kind.Length == 0 ? null : kind;
        UpdateIconSelection();
    }

    private void UpdateIconSelection()
    {
        foreach (var button in _iconButtons)
        {
            var kind = (string)button.Tag;
            var selected = string.Equals(kind, _selectedIconKind ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
            button.Content = MainWindow.CreateControllerFamilyIcon(_controller with
            {
                AccentColor = _selectedColor,
                ProfileIconKind = kind.Length == 0 ? null : kind
            });
            button.Background = ColorBrush(selected ? "#302A50" : "#191823");
            button.BorderBrush = ColorBrush(selected ? "#8B7CF6" : "#3B374B");
            button.Foreground = ColorBrush(selected ? "#F4F2FF" : "#CBC7D5");
        }
    }

    private static Brush ColorBrush(string color) =>
        (Brush)new BrushConverter().ConvertFromString(color)!;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var customName = CustomNameTextBox.Text.Trim();
        Result = new ControllerProfile(_deviceKey, customName.Length == 0 ? null : customName,
            _selectedColor, _selectedIconKind);
        CloseWithAnimation(true);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseWithAnimation(false);

    private void ChromeCloseButton_Click(object sender, RoutedEventArgs e) => CloseWithAnimation(false);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void ProfileWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var darkMode = 1;
        var handle = new WindowInteropHelper(this).Handle;
        if (DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref darkMode, sizeof(int));
    }

    private void ProfileWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ProfileRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
            TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        CustomNameTextBox.Focus();
        CustomNameTextBox.SelectAll();
    }

    private void ProfileWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        CloseWithAnimation(false);
    }

    private void CloseWithAnimation(bool accepted)
    {
        if (_closeAnimationRunning) return;
        _closeAnimationRunning = true;
        var fade = new DoubleAnimation(ProfileRoot.Opacity, 0, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            _allowClose = true;
            DialogResult = accepted;
        };
        ProfileRoot.BeginAnimation(OpacityProperty, fade);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int value, int valueSize);
}
