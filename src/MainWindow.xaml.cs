using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using ControllerBattery.Models;
using ControllerBattery.Providers;
using ControllerBattery.Services;

namespace ControllerBattery;

public partial class MainWindow : Window
{
    private const int OverlayHotkeyId = 0x4342;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly Brush HealthyBrush = BrushFrom("#8B7CF6");
    private static readonly Brush ChargingBrush = BrushFrom("#60D394");
    private static readonly Brush WarningBrush = BrushFrom("#F7B955");
    private static readonly Brush CriticalBrush = BrushFrom("#FF6B6B");
    private static readonly Brush OfflineBrush = BrushFrom("#6D6980");

    private readonly DispatcherTimer _refreshTimer;
    private IReadOnlyList<ControllerDevice> _detectedControllers = [];
    private IReadOnlyList<ControllerDevice> _controllers = [];
    private readonly IControllerProvider _provider = CreateHardwareProvider();
    private readonly Dictionary<string, ControllerProfile> _profiles = ControllerProfileStore.Load();
    private string? _selectedId;
    private AppSettings _settings = AppSettingsStore.Load();
    private OverlayWindow? _overlay;
    private LowBatteryNotificationWindow? _lowBatteryNotification;
    private readonly HashSet<string> _lowBatteryControllers = [];
    private HwndSource? _windowSource;

    private static IControllerProvider CreateHardwareProvider() =>
        new CompositeControllerProvider(
        [
            new XInputControllerProvider(),
            new DualSenseHidProvider(),
            new EightBitDoHidProvider(),
            new NintendoSwitchProHidProvider()
        ]);

    public MainWindow()
    {
        InitializeComponent();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settings.PollingIntervalSeconds)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshControllersAsync();
        _refreshTimer.Start();
        UpdatePollingText();

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += async (_, _) => await RefreshControllersAsync();
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            UnregisterOverlayHotkey();
            _windowSource?.RemoveHook(WindowMessageHook);
            _overlay?.Close();
            _lowBatteryNotification?.Close();
        };
    }

    private async Task RefreshControllersAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "Scanning…";

        try
        {
            _detectedControllers = await _provider.GetControllersAsync();
            _controllers = ApplyProfiles(_detectedControllers);
            await ShowLowBatteryAlertsAsync();
            RenderControllerList();

            var count = _controllers.Count;
            DeviceCount.Text = count.ToString();
            EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = count == 0 ? "No controllers found" : "Monitoring";
            StatusDot.Fill = count == 0 ? OfflineBrush : ChargingBrush;
            LastUpdated.Text = $"Last scan: {DateTime.Now:h:mm:ss tt}";

            var selected = _controllers.FirstOrDefault(device => DeviceKey(device) == _selectedId);
            if (selected is not null)
            {
                ShowControllerDetail(selected);
            }
            else if (_selectedId is not null)
            {
                ShowOverview();
            }
        }
        catch (Exception exception)
        {
            StatusText.Text = "Scan failed";
            StatusDot.Fill = CriticalBrush;
            MessageBox.Show(this, exception.Message, "Controller scan failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void RenderControllerList()
    {
        ControllerList.Children.Clear();
        foreach (var controller in _controllers)
        {
            ControllerList.Children.Add(CreateControllerCard(controller));
        }
    }

    private async Task ShowLowBatteryAlertsAsync()
    {
        var lowNow = _controllers
            .Where(controller => !controller.IsCharging &&
                controller.BatteryLevel is BatteryLevel.Empty or BatteryLevel.Low)
            .ToDictionary(DeviceKey);
        var newlyLow = lowNow
            .Where(pair => !_lowBatteryControllers.Contains(pair.Key))
            .Select(pair => pair.Value)
            .ToArray();

        _lowBatteryControllers.Clear();
        foreach (var key in lowNow.Keys)
        {
            _lowBatteryControllers.Add(key);
        }

        if (newlyLow.Length == 0) return;

        _lowBatteryNotification ??= new LowBatteryNotificationWindow();
        _lowBatteryNotification.ShowAlert(newlyLow, _settings.OverlayPosition);

        if (_provider is IAttentionPulseControllerProvider pulseProvider)
        {
            foreach (var controller in newlyLow)
            {
                try
                {
                    await pulseProvider.PulseAsync(controller);
                }
                catch
                {
                    // The visual warning remains useful if another app owns HID output.
                }
            }
        }
    }

    private Border CreateControllerCard(ControllerDevice controller)
    {
        var accent = ProfileAccent(controller);
        var card = new Border
        {
            Style = (Style)FindResource("ControllerCard"),
            Tag = DeviceKey(controller),
            ToolTip = $"Open details for {controller.Name}"
        };
        if (DeviceKey(controller) == _selectedId)
        {
            card.Background = BrushFrom("#302A45");
            card.BorderBrush = accent;
            card.BorderThickness = new Thickness(2);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });

        grid.Children.Add(new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(10),
            Background = BrushFrom("#2B273B"),
            BorderBrush = DeviceKey(controller) == _selectedId
                ? accent
                : BrushFrom("#4A4657"),
            BorderThickness = new Thickness(DeviceKey(controller) == _selectedId ? 1.5 : 1),
            Margin = new Thickness(0, 0, 8, 0),
            Child = CreateControllerFamilyIcon(controller)
        });

        var details = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        details.Children.Add(new TextBlock
        {
            Text = controller.Name.Trim(),
            Style = (Style)FindResource("CardTitle"),
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120,
            Margin = new Thickness(0)
        });
        details.Children.Add(new TextBlock
        {
            Text = controller.Connection,
            Style = (Style)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        var battery = new Border
        {
            Width = 72,
            Background = GetBatteryChipBackground(controller),
            BorderBrush = DeviceKey(controller) == _selectedId
                ? accent
                : BrushFrom("#4A4657"),
            BorderThickness = new Thickness(DeviceKey(controller) == _selectedId ? 1.5 : 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4, 4, 4, 4),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = CreateBatteryBadgeContent(controller)
        };
        Grid.SetColumn(battery, 2);
        grid.Children.Add(battery);

        card.Child = grid;
        card.MouseLeftButtonUp += ControllerCard_MouseLeftButtonUp;
        return card;
    }

    private static FrameworkElement CreateBatteryBadgeContent(ControllerDevice controller)
    {
        var brush = GetBatteryBrush(controller);
        if (!controller.IsCharging)
        {
            return new TextBlock
            {
                Text = FormatBattery(controller),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var content = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(-1.5, 0)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M5,0 L0,6 H3 L1.5,12 L8,4.5 H4.5 Z"),
            Fill = brush,
            Width = 7,
            Height = 11,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 3, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        var text = new TextBlock
        {
            Text = FormatBattery(controller),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);
        content.Children.Add(text);
        return content;
    }

    private void ControllerCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: string id } &&
            _controllers.FirstOrDefault(device => DeviceKey(device) == id) is { } controller)
        {
            if (id == _selectedId)
            {
                ShowOverview();
                RenderControllerList();
                return;
            }

            ShowControllerDetail(controller, animate: id != _selectedId);
            RenderControllerList();
        }
    }

    private void ShowControllerDetail(ControllerDevice controller, bool animate = false)
    {
        _selectedId = DeviceKey(controller);
        WelcomePanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        PageTitle.Text = "Controller details";
        DetailName.Text = controller.Name;
        DetailName.Foreground = ProfileAccent(controller);
        DetailConnection.Text = $"{controller.Connection}  •  {controller.Kind}";
        DetailBattery.Text = FormatBattery(controller);
        BatteryBar.Value = controller.BatteryPercent ?? 0;
        BatteryBar.Visibility = controller.BatteryPercent.HasValue
            ? Visibility.Visible
            : Visibility.Hidden;
        var batteryBrush = GetBatteryBrush(controller);
        BatteryBar.Foreground = batteryBrush;
        BatteryFill.Width = 28 * GetBatteryFillRatio(controller);
        BatteryFill.Background = batteryBrush;
        BatteryOutline.BorderBrush = batteryBrush;
        BatteryTerminal.Background = batteryBrush;
        DetailStatus.Text = controller.BatteryNote ?? (controller.IsCharging
            ? "Charging"
            : controller.BatteryLevel == BatteryLevel.Full ? "Fully charged"
            : IsBatteryLow(controller) ? "Low battery" : "Ready to play");
        DetailUpdated.Text = $"Updated {controller.UpdatedAt:h:mm:ss tt}";
        PowerOffButton.Visibility = controller.CanPowerOff
            ? Visibility.Visible
            : Visibility.Collapsed;
        IdentifyButton.Visibility = controller.CanIdentify
            ? Visibility.Visible
            : Visibility.Collapsed;
        PowerOffButton.Margin = controller.CanIdentify
            ? new Thickness(10, 0, 0, 0)
            : new Thickness(0);

        if (animate)
        {
            AnimateControllerDetail();
        }
    }

    private void AnimateControllerDetail()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(220);

        DetailPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = easing
        });
        DetailPanelTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(16, 0, duration) { EasingFunction = easing });
    }

    private void ShowOverview()
    {
        _selectedId = null;
        WelcomePanel.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed;
        PageTitle.Text = "Overview";
    }

    private static Brush GetBatteryBrush(ControllerDevice controller) =>
        controller.BatteryLevel == BatteryLevel.Unknown ? OfflineBrush :
        controller.IsCharging ? ChargingBrush :
        controller.BatteryLevel is BatteryLevel.Empty or BatteryLevel.Low ? CriticalBrush :
        controller.BatteryLevel == BatteryLevel.Medium ? WarningBrush : HealthyBrush;

    private static string FormatBattery(ControllerDevice controller)
    {
        if (controller.BatteryPercent is { } percent)
        {
            return $"{percent}%";
        }

        return controller.BatteryLevel switch
        {
            BatteryLevel.Empty => "Empty",
            BatteryLevel.Low => "Low",
            BatteryLevel.Medium => "Medium",
            BatteryLevel.High => "High",
            BatteryLevel.Full => "Full",
            _ => "Unknown"
        };
    }

    private static bool IsBatteryLow(ControllerDevice controller) =>
        controller.BatteryLevel is BatteryLevel.Empty or BatteryLevel.Low;

    private static double GetBatteryFillRatio(ControllerDevice controller)
    {
        if (controller.BatteryPercent is { } percent)
        {
            return Math.Clamp(percent / 100d, 0, 1);
        }

        return controller.BatteryLevel switch
        {
            BatteryLevel.Empty => 0,
            BatteryLevel.Low => 0.2,
            BatteryLevel.Medium => 0.5,
            BatteryLevel.High => 0.8,
            BatteryLevel.Full => 1,
            _ => 0
        };
    }

    internal static FrameworkElement CreateControllerFamilyIcon(ControllerDevice controller)
    {
        var family = controller.ProfileIconKind ?? controller.Kind;
        var accent = ProfileAccent(controller);
        var label = family switch
        {
            "PlayStation" => "PS",
            "Nintendo" => "NS",
            "8BitDo" => "8B",
            _ when family.Contains("Xbox", StringComparison.OrdinalIgnoreCase) => "XB",
            _ => "PAD"
        };
        var surface = BrushFrom("#403953");
        var canvas = new Canvas { Width = 30, Height = 22 };
        var bodyData = family == "8BitDo"
            ? "M3,5 Q3,3 5,3 H25 Q27,3 27,5 V17 Q27,19 25,19 H5 Q3,19 3,17 Z"
            : family == "PlayStation"
                ? "M7,4 C4,4 3,7 2,12 L1,17 C1,20 5,21 7,18 L10,15 H20 L23,18 C25,21 29,20 29,17 L28,12 C27,7 26,4 23,4 C20,4 19,6 15,6 C11,6 10,4 7,4 Z"
                : family == "Nintendo"
                    ? "M6,4 C3,4 2,7 2,12 L1,17 C1,20 5,21 7,18 L9,15 H21 L23,18 C25,21 29,20 29,17 L28,12 C28,7 27,4 24,4 C21,4 20,6 15,6 C10,6 9,4 6,4 Z"
                    : "M6,5 C3,5 2,8 2,12 L1,17 C1,20 5,21 7,18 L10,15 H20 L23,18 C25,21 29,20 29,17 L28,12 C28,8 26,5 23,5 C20,5 18,6 15,6 C12,6 10,5 6,5 Z";

        canvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(bodyData),
            Fill = surface,
            Stroke = accent,
            StrokeThickness = 1.25,
            StrokeLineJoin = PenLineJoin.Round
        });

        var asymmetricLayout = family == "Nintendo" ||
            family.Contains("Xbox", StringComparison.OrdinalIgnoreCase);
        AddDPad(canvas, 8, asymmetricLayout ? 14 : 10, accent);
        if (family.Contains("Xbox", StringComparison.OrdinalIgnoreCase))
        {
            AddControlDot(canvas, 9, 8, accent);
            AddControlDot(canvas, 22, 9, accent);
        }
        else if (family == "Nintendo")
        {
            AddControlDot(canvas, 9, 8, accent);
            AddControlDot(canvas, 21, 14, accent);
        }
        else
        {
            AddControlDot(canvas, 21, 9, accent);
            if (family == "PlayStation")
            {
                AddControlDot(canvas, 12, 14, accent);
                AddControlDot(canvas, 18, 14, accent);
            }
        }

        var icon = new Viewbox
        {
            Width = 25,
            Height = 18,
            Stretch = Stretch.Uniform,
            Child = canvas,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var badge = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Children.Add(icon);
        badge.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = accent,
            FontSize = label == "PAD" ? 6.5 : 7.5,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        });
        return badge;
    }

    private static void AddDPad(Canvas canvas, double x, double y, Brush brush)
    {
        var dpad = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M2,0 H4 V2 H6 V4 H4 V6 H2 V4 H0 V2 H2 Z"),
            Fill = brush,
            Stretch = Stretch.Uniform,
            Width = 6,
            Height = 6
        };
        Canvas.SetLeft(dpad, x - 3);
        Canvas.SetTop(dpad, y - 3);
        canvas.Children.Add(dpad);
    }

    private static void AddControlDot(Canvas canvas, double x, double y, Brush brush)
    {
        var dot = new Ellipse { Width = 3.2, Height = 3.2, Fill = brush };
        Canvas.SetLeft(dot, x - 1.6);
        Canvas.SetTop(dot, y - 1.6);
        canvas.Children.Add(dot);
    }

    private static Brush GetBatteryChipBackground(ControllerDevice controller) =>
        controller.BatteryLevel == BatteryLevel.Unknown ? BrushFrom("#292733") :
        controller.IsCharging ? BrushFrom("#1E392F") :
        IsBatteryLow(controller) ? BrushFrom("#40272D") :
        controller.BatteryLevel == BatteryLevel.Medium ? BrushFrom("#3D3424") : BrushFrom("#302B49");

    private static string DeviceKey(ControllerDevice controller) =>
        $"{controller.ProviderId}:{controller.Id}";

    private IReadOnlyList<ControllerDevice> ApplyProfiles(IReadOnlyList<ControllerDevice> controllers) =>
        controllers.Select(controller =>
        {
            if (!_profiles.TryGetValue(DeviceKey(controller), out var profile))
                return controller with { AccentColor = ControllerProfile.DefaultAccentColor };

            return controller with
            {
                Name = string.IsNullOrWhiteSpace(profile.CustomName) ? controller.Name : profile.CustomName.Trim(),
                AccentColor = profile.AccentColor,
                ProfileIconKind = profile.IconKind
            };
        }).ToArray();

    private static Brush ProfileAccent(ControllerDevice controller)
    {
        try
        {
            return BrushFrom(controller.AccentColor ?? ControllerProfile.DefaultAccentColor);
        }
        catch (FormatException)
        {
            return BrushFrom(ControllerProfile.DefaultAccentColor);
        }
    }

    private static Brush BrushFrom(string color) =>
        (Brush)new BrushConverter().ConvertFromString(color)!;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximized();

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshControllersAsync();

    private async void PowerOffButton_Click(object sender, RoutedEventArgs e)
    {
        var controller = _controllers.FirstOrDefault(device => DeviceKey(device) == _selectedId);
        if (controller is null || !controller.CanPowerOff ||
            _provider is not IPowerOffControllerProvider powerProvider)
        {
            return;
        }

        if (MessageBox.Show(this, $"Turn off {controller.Name}?", "Turn off controller",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        PowerOffButton.IsEnabled = false;
        try
        {
            await powerProvider.PowerOffAsync(controller);
            await Task.Delay(750);
            await RefreshControllersAsync();
            if (_controllers.Any(device => DeviceKey(device) == DeviceKey(controller)))
            {
                throw new IOException("The controller accepted the disconnect request but reconnected immediately.");
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not turn off controller",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PowerOffButton.IsEnabled = true;
        }
    }

    private async void IdentifyButton_Click(object sender, RoutedEventArgs e)
    {
        var controller = _controllers.FirstOrDefault(device => DeviceKey(device) == _selectedId);
        if (controller is null || !controller.CanIdentify ||
            _provider is not IAttentionPulseControllerProvider pulseProvider)
        {
            return;
        }

        IdentifyButton.IsEnabled = false;
        try
        {
            await pulseProvider.PulseAsync(controller);
        }
        catch (TimeoutException)
        {
            // Identification is best-effort. Some HID transports time out after
            // delivering the rumble packet, so there is no actionable error to show.
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not identify controller",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IdentifyButton.IsEnabled = true;
        }
    }

    private void EditProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var controller = _controllers.FirstOrDefault(device => DeviceKey(device) == _selectedId);
        if (controller is null) return;

        _profiles.TryGetValue(DeviceKey(controller), out var existing);
        var dialog = new ProfileWindow(controller, existing) { Owner = this };
        bool? accepted;
        SetModalBackdrop(true);
        try
        {
            accepted = dialog.ShowDialog();
        }
        finally
        {
            SetModalBackdrop(false);
        }

        if (accepted != true || dialog.Result is not { } profile) return;

        if (string.IsNullOrWhiteSpace(profile.CustomName) &&
            profile.AccentColor.Equals(ControllerProfile.DefaultAccentColor, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(profile.IconKind))
            _profiles.Remove(profile.DeviceKey);
        else
            _profiles[profile.DeviceKey] = profile;

        try
        {
            ControllerProfileStore.Save(_profiles);
        }
        catch (IOException exception)
        {
            MessageBox.Show(this, exception.Message, "Could not save profile",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _controllers = ApplyProfiles(_detectedControllers);
        RenderControllerList();
        ShowControllerDetail(_controllers.First(device => DeviceKey(device) == profile.DeviceKey));
        if (_overlay?.IsVisible == true)
            _overlay.Update(_controllers, _settings.OverlayShortcutText, _settings.OverlayPosition);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource.AddHook(WindowMessageHook);
        if (!RegisterOverlayHotkey(_settings))
        {
            MessageBox.Show(this,
                $"The overlay shortcut {_settings.OverlayShortcutText} is already used by another application. Change it in Settings.",
                "Shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == OverlayHotkeyId)
        {
            handled = true;
            _ = ToggleOverlayAsync();
        }

        return IntPtr.Zero;
    }

    private async Task ToggleOverlayAsync()
    {
        if (_overlay?.IsVisible == true)
        {
            _overlay.Hide();
            return;
        }

        _overlay ??= new OverlayWindow();
        _overlay.Update(_controllers, _settings.OverlayShortcutText, _settings.OverlayPosition);
        _overlay.Show();
        _overlay.Update(_controllers, _settings.OverlayShortcutText, _settings.OverlayPosition);

        try
        {
            _detectedControllers = await _provider.GetControllersAsync();
            _controllers = ApplyProfiles(_detectedControllers);
            if (_overlay.IsVisible)
                _overlay.Update(_controllers, _settings.OverlayShortcutText, _settings.OverlayPosition);
        }
        catch
        {
            // Keep the last successful snapshot visible if a provider is busy.
        }
    }

    private bool RegisterOverlayHotkey(AppSettings settings)
    {
        var modifiers = (uint)settings.OverlayModifiers | ModNoRepeat;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(settings.OverlayKey);
        return NativeMethods.RegisterHotKey(new WindowInteropHelper(this).Handle,
            OverlayHotkeyId, modifiers, virtualKey);
    }

    private void UnregisterOverlayHotkey()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            NativeMethods.UnregisterHotKey(handle, OverlayHotkeyId);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, ShowTestLowBatteryNotification) { Owner = this };
        bool? accepted;
        SetModalBackdrop(true);
        try
        {
            accepted = dialog.ShowDialog();
        }
        finally
        {
            SetModalBackdrop(false);
        }

        if (accepted != true || dialog.Result is not { } updated) return;

        UnregisterOverlayHotkey();
        if (!RegisterOverlayHotkey(updated))
        {
            RegisterOverlayHotkey(_settings);
            MessageBox.Show(this, $"{updated.OverlayShortcutText} is already used by another application.",
                "Shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings = updated;
        AppSettingsStore.Save(_settings);
        _refreshTimer.Interval = TimeSpan.FromSeconds(_settings.PollingIntervalSeconds);
        UpdatePollingText();
        if (_overlay?.IsVisible == true)
            _overlay.Update(_controllers, _settings.OverlayShortcutText, _settings.OverlayPosition);
    }

    private void SetModalBackdrop(bool visible)
    {
        MainContent.BeginAnimation(OpacityProperty, new DoubleAnimation(
            MainContent.Opacity,
            visible ? 0.58 : 1,
            TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void UpdatePollingText() =>
        PollingText.Text = $"{_settings.PollingIntervalSeconds}s refresh";

    // Optional diagnostic callback; remove with the TEST card in SettingsWindow.xaml.
    private void ShowTestLowBatteryNotification(OverlayPosition position)
    {
        var testController = new ControllerDevice(
            "notification-test", "diagnostics", "Example controller", "Test", "Wireless",
            15, BatteryLevel.Low, false, DateTime.Now);
        _lowBatteryNotification ??= new LowBatteryNotificationWindow();
        _lowBatteryNotification.ShowAlert([testController], position);
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutWindow { Owner = this };
        SetModalBackdrop(true);
        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            SetModalBackdrop(false);
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);
    }
}
