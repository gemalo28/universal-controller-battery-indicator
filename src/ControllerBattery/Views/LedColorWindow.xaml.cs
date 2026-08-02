using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ControllerBattery;

public partial class LedColorWindow : Window
{
    private static readonly string[] SuggestedColorValues =
    [
        "#FF0000", "#00FF00", "#0066FF", "#00FFFF",
        "#FF00FF", "#FFFF00", "#FF6600", "#FFFFFF"
    ];
    private readonly string _startingColor;
    private readonly Func<string, Task>? _previewAsync;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(75) };
    private bool _initialized;
    private bool _accepted;

    public string SelectedColor { get; private set; }

    public LedColorWindow(string color, Func<string, Task>? previewAsync)
    {
        InitializeComponent();
        _startingColor = color;
        SelectedColor = color;
        _previewAsync = previewAsync;
        var parsed = (Color)ColorConverter.ConvertFromString(color);
        RedSlider.Value = parsed.R;
        GreenSlider.Value = parsed.G;
        BlueSlider.Value = parsed.B;
        foreach (var suggestedColor in SuggestedColorValues)
        {
            var button = new Button
            {
                Tag = suggestedColor,
                ToolTip = suggestedColor,
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(suggestedColor)),
                Style = (Style)FindResource("SuggestedColor")
            };
            button.Click += SuggestedColor_Click;
            SuggestedColors.Children.Add(button);
        }
        _initialized = true;
        UpdatePreview(false);
        _previewTimer.Tick += PreviewTimer_Tick;
        Closed += LedColorWindow_Closed;
    }

    private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        UpdatePreview(true);
    }

    private void SuggestedColor_Click(object sender, RoutedEventArgs e)
    {
        var color = (Color)ColorConverter.ConvertFromString((string)((Button)sender).Tag);
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        UpdatePreview(true);
    }

    private void UpdatePreview(bool sendToController)
    {
        var red = (byte)RedSlider.Value;
        var green = (byte)GreenSlider.Value;
        var blue = (byte)BlueSlider.Value;
        SelectedColor = $"#{red:X2}{green:X2}{blue:X2}";
        ColorPreview.Background = new SolidColorBrush(Color.FromRgb(red, green, blue));
        if (!HexInput.IsKeyboardFocused)
            HexInput.Text = SelectedColor;
        RedValue.Text = red.ToString();
        GreenValue.Text = green.ToString();
        BlueValue.Text = blue.ToString();
        if (!sendToController) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void HexInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        ApplyHexInput();
        Keyboard.ClearFocus();
    }

    private void HexInput_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        ApplyHexInput();

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!HexInput.IsKeyboardFocused || IsInsideHexInput(e.OriginalSource as DependencyObject))
            return;

        Keyboard.ClearFocus();
    }

    private bool IsInsideHexInput(DependencyObject? element)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, HexInput)) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void ApplyHexInput()
    {
        if (!TryParseHexColor(HexInput.Text, out var color))
        {
            HexInput.Text = SelectedColor;
            HexInput.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            return;
        }

        HexInput.BorderBrush = new SolidColorBrush(Color.FromRgb(81, 75, 104));
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        UpdatePreview(true);
        HexInput.Text = SelectedColor;
    }

    private static bool TryParseHexColor(string value, out Color color)
    {
        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(character => new string(character, 2)));

        if (hex.Length == 6 && int.TryParse(hex,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb))
        {
            color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }

        color = default;
        return false;
    }

    private async void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        if (_previewAsync is not null) await _previewAsync(SelectedColor);
    }

    private async void Ok_Click(object sender, RoutedEventArgs e)
    {
        _previewTimer.Stop();
        if (_previewAsync is not null) await _previewAsync(SelectedColor);
        _accepted = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void LedColorWindow_Closed(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        if (!_accepted && _previewAsync is not null) _ = _previewAsync(_startingColor);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
