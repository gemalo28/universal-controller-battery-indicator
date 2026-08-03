using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using ControllerBattery.Models;
using ControllerBattery.Services;
using ControllerBattery.Behaviors;
using ControllerBattery.Interop;
using System.Windows.Controls.Primitives;

namespace ControllerBattery.Tests.Integration;

public sealed class WpfWindowIntegrationTests
{
    [Fact]
    public void Windows_RenderControllerFamiliesAndSettings_OnStaThread() => RunSta(() =>
    {
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var controllers = ControllerCases().ToArray();

        TestOverlay(controllers);
        TestNotifications(controllers);
        TestSettings();
        TestProfiles(controllers);
        TestLedEditor();
        TestAbout();
        TestMainWindowPresentationHelpers(controllers);
        TestMainWindowRendering(controllers);
        TestWpfHelpers();
        var lifetime = App.LifetimeToken;
        Invoke(app, "OnExit", CreateExitEvent());
        Assert.True(lifetime.IsCancellationRequested);
    });

    private static void TestOverlay(IReadOnlyList<ControllerDevice> controllers)
    {
        var overlay = new OverlayWindow();
        overlay.Show();
        overlay.Update(controllers, "Ctrl+Alt+B", OverlayPosition.BottomLeft);
        Assert.Equal(controllers.Count, overlay.ControllerRows.Children.Count);
        overlay.Update([], "Ctrl+Shift+O", OverlayPosition.TopRight);
        Assert.Equal(Visibility.Visible, overlay.EmptyText.Visibility);
        overlay.Close();
    }

    private static void TestNotifications(IReadOnlyList<ControllerDevice> controllers)
    {
        var connection = new ControllerConnectionNotificationWindow();
        connection.ShowChanges([new(controllers[0], true)]);
        Assert.Equal("Controller connected", connection.TitleText.Text);
        connection.ShowChanges([new(controllers[1], false)]);
        Assert.Equal("Controller disconnected", connection.TitleText.Text);
        connection.ShowChanges([
            new(controllers[0], true), new(controllers[1], false), new(controllers[2], true)]);
        Assert.Equal("Controller status changed", connection.TitleText.Text);
        Invoke(connection, "BeginClose");
        connection.Close();

        var low = new LowBatteryNotificationWindow();
        low.ShowAlert([controllers[0]], OverlayPosition.BottomLeft);
        Assert.Contains(controllers[0].Name, low.MessageText.Text);
        low.ShowAlert(controllers.Take(2).ToArray(), OverlayPosition.TopRight);
        Assert.Contains("2 controllers", low.MessageText.Text);
        Invoke(low, "BeginClose");
        low.Close();
    }

    private static void TestSettings()
    {
        foreach (var position in Enum.GetValues<OverlayPosition>())
        {
            var settings = AppSettings.Default with
            {
                OverlayPosition = position,
                StartWithWindows = true,
                ShowConnectionNotifications = false,
                ShowLowBatteryNotifications = false
            };
            var testedPosition = position;
            var window = new SettingsWindow(settings, value => testedPosition = value);
            Invoke(window, "SettingsWindow_SourceInitialized", window, EventArgs.Empty);
            Invoke(window, "SettingsWindow_Loaded", window, new RoutedEventArgs());
            Invoke(window, "CaptureButton_Click", window, new RoutedEventArgs());
            using var inputSource = new System.Windows.Interop.HwndSource(
                new System.Windows.Interop.HwndSourceParameters("ControllerBatteryKeyTest")
                { Width = 1, Height = 1 });
            var key = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice, inputSource, 0,
                System.Windows.Input.Key.K)
            { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent };
            Invoke(window, "SettingsWindow_PreviewKeyDown", window, key);
            var modifierOnly = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice, inputSource, 0,
                System.Windows.Input.Key.LeftCtrl)
            { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent };
            Invoke(window, "SettingsWindow_PreviewKeyDown", window, modifierOnly);
            Assert.False(window.TrySetShortcut(ModifierKeys.None, System.Windows.Input.Key.Escape));
            Assert.True(window.TrySetShortcut(ModifierKeys.Control | ModifierKeys.Alt,
                System.Windows.Input.Key.K));
            SetField(window, "_capturing", false);
            Assert.Equal(position, Invoke<OverlayPosition>(window, "GetOverlayPosition"));
            window.PollingIntervalText.Text = "45";
            Invoke(window, "TestLowBatteryButton_Click", window, new RoutedEventArgs());
            Assert.Equal(position, testedPosition);
            Invoke(window, "SaveButton_Click", window, new RoutedEventArgs());
            Assert.NotNull(window.Result);
            Assert.Equal(45, window.Result!.PollingIntervalSeconds);
            var closing = new System.ComponentModel.CancelEventArgs();
            Invoke(window, "SettingsWindow_Closing", window, closing);
            Assert.True(closing.Cancel);
            Invoke(window, "CancelButton_Click", window, new RoutedEventArgs());
            SetField(window, "_allowClose", true);
            window.Close();
        }
    }

    private static void TestProfiles(IReadOnlyList<ControllerDevice> controllers)
    {
        foreach (var controller in controllers)
        {
            var key = ControllerProfileService.DeviceKey(controller);
            var profile = new ControllerProfile(key, "Custom", "#336699", controller.Kind,
                LedColor: controller.CanSetLed ? "#112233" : null, LedBrightness: 1,
                SyncLedWithProfile: controller.CanSetLed);
            var previewCount = 0;
            var window = new ProfileWindow(controller, profile, (_, _) =>
            {
                previewCount++;
                return Task.CompletedTask;
            });
            Invoke(window, "ProfileWindow_SourceInitialized", window, EventArgs.Empty);
            Invoke(window, "ProfileWindow_Loaded", window, new RoutedEventArgs());
            Assert.Equal("Custom", window.CustomNameTextBox.Text);
            foreach (var button in GetField<List<Button>>(window, "_colorButtons"))
                Invoke(window, "Color_Click", button, new RoutedEventArgs());
            foreach (var button in GetField<List<Button>>(window, "_iconButtons"))
                Invoke(window, "Icon_Click", button, new RoutedEventArgs());
            window.CustomLedCheckBox.IsChecked = controller.CanSetLed;
            window.SyncLedWithProfileCheckBox.IsChecked = controller.CanSetLed;
            Invoke(window, "SyncLedWithProfileCheckBox_Changed", window, new RoutedEventArgs());
            window.BrightLedBrightness.IsChecked = true;
            Invoke(window, "LedBrightness_Changed", window, new RoutedEventArgs());
            window.MediumLedBrightness.IsChecked = true;
            Invoke(window, "LedBrightness_Changed", window, new RoutedEventArgs());
            window.DimLedBrightness.IsChecked = true;
            Invoke(window, "LedBrightness_Changed", window, new RoutedEventArgs());
            Invoke(window, "UpdateColorSelection");
            Invoke(window, "UpdateIconSelection");
            Invoke(window, "UpdateLedColorControls");
            Invoke(window, "Save_Click", window, new RoutedEventArgs());
            Assert.NotNull(window.Result);
            Assert.Equal(key, window.Result!.DeviceKey);
            var closing = new System.ComponentModel.CancelEventArgs();
            Invoke(window, "ProfileWindow_Closing", window, closing);
            Assert.True(closing.Cancel);
            Invoke(window, "Cancel_Click", window, new RoutedEventArgs());
            Assert.Equal(controller.CanSetLed, previewCount > 0);
            SetField(window, "_allowClose", true);
            window.Close();
        }
    }

    private static void TestLedEditor()
    {
        var previews = new List<string>();
        var window = new LedColorWindow("#123456", color =>
        {
            previews.Add(color);
            return Task.CompletedTask;
        });
        Assert.Equal(8, window.SuggestedColors.Children.Count);
        var first = Assert.IsType<Button>(window.SuggestedColors.Children[0]);
        Invoke(window, "SuggestedColor_Click", first, new RoutedEventArgs());
        Assert.Equal("#FF0000", window.SelectedColor);

        window.HexInput.Text = "#0F8";
        Invoke(window, "ApplyHexInput");
        Assert.Equal("#00FF88", window.SelectedColor);
        window.HexInput.Text = "invalid";
        Invoke(window, "ApplyHexInput");
        Assert.Equal("#00FF88", window.HexInput.Text);

        using var inputSource = new System.Windows.Interop.HwndSource(
            new System.Windows.Interop.HwndSourceParameters("ControllerBatteryColorKeyTest")
            { Width = 1, Height = 1 });
        var enter = new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice, inputSource, 0,
            System.Windows.Input.Key.Enter)
        { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent };
        Invoke(window, "HexInput_KeyDown", window.HexInput, enter);
        Assert.True(enter.Handled);
        var otherKey = new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice, inputSource, 0,
            System.Windows.Input.Key.K)
        { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent };
        Invoke(window, "HexInput_KeyDown", window.HexInput, otherKey);
        Invoke(window, "HexInput_LostKeyboardFocus", window.HexInput, null);
        Assert.True(Invoke<bool>(window, "IsInsideHexInput", window.HexInput));
        Assert.False(Invoke<bool>(window, "IsInsideHexInput", new Border()));

        Assert.True(InvokeTryParse("ABC", out var shorthand));
        Assert.Equal(Color.FromRgb(0xAA, 0xBB, 0xCC), shorthand);
        Assert.False(InvokeTryParse("nope", out _));
        Invoke(window, "Cancel_Click", window, new RoutedEventArgs());
    }

    private static void TestAbout()
    {
        var window = new AboutWindow();
        Invoke(window, "AboutWindow_SourceInitialized", window, EventArgs.Empty);
        Invoke(window, "AboutWindow_Loaded", window, new RoutedEventArgs());
        Assert.StartsWith("v", window.BuildVersionText.Text);
        Invoke(window, "CloseButton_Click", window, new RoutedEventArgs());
        var closing = new System.ComponentModel.CancelEventArgs();
        Invoke(window, "AboutWindow_Closing", window, closing);
        SetField(window, "_allowClose", true);
        window.Close();
    }

    private static void TestMainWindowPresentationHelpers(IEnumerable<ControllerDevice> controllers)
    {
        foreach (var controller in controllers.Concat([
            controllers.First() with { BatteryPercent = 100, IsCharging = true },
            controllers.First() with { BatteryPercent = null, BatteryLevel = BatteryLevel.Unknown },
            controllers.First() with { BatteryPercent = null, BatteryLevel = BatteryLevel.Empty },
            controllers.First() with { BatteryPercent = null, BatteryLevel = BatteryLevel.Unknown,
                BatteryNote = "Unavailable" },
            controllers.First() with { BatteryPercent = null, BatteryLevel = BatteryLevel.Medium },
            controllers.First() with { BatteryPercent = null, BatteryLevel = BatteryLevel.High },
            controllers.First() with { BatteryPercent = null, BatteryLevel = BatteryLevel.Full }
        ]))
        {
            var icon = MainWindow.CreateControllerFamilyIcon(controller);
            Assert.NotNull(icon);
            Assert.NotNull(InvokeStatic<Brush>(typeof(MainWindow), "GetBatteryBrush", controller));
            Assert.False(string.IsNullOrWhiteSpace(
                InvokeStatic<string>(typeof(MainWindow), "FormatBattery", controller)));
            Assert.InRange(InvokeStatic<double>(typeof(MainWindow), "GetBatteryFillRatio", controller), 0, 1);
            Assert.NotNull(InvokeStatic<FrameworkElement>(typeof(MainWindow),
                "CreateBatteryBadgeContent", controller));
        }
        var invalidAccent = controllers.First() with { AccentColor = "not-a-color" };
        Assert.NotNull(InvokeStatic<Brush>(typeof(MainWindow), "ProfileAccent", invalidAccent));
    }

    private static void TestMainWindowRendering(IReadOnlyList<ControllerDevice> controllers)
    {
        var provider = new ControllerBattery.Tests.Fakes.FakeControllerProvider("ui");
        var profiles = new Dictionary<string, ControllerProfile>();
        var window = new MainWindow(provider, AppSettings.Default with
        {
            OverlayKey = System.Windows.Input.Key.F11,
            ShowConnectionNotifications = false,
            ShowLowBatteryNotifications = false
        }, profiles, showTrayIcon: false, _ => { });
        var child = controllers[0] with { Id = "virtual", ProviderId = "xinput", Name = "Output" };
        var childKey = ControllerProfileService.DeviceKey(child);
        var parentKey = ControllerProfileService.DeviceKey(controllers[1]);
        profiles[childKey] = new(childKey, null, ControllerProfile.DefaultAccentColor,
            ParentDeviceKey: parentKey);
        var all = controllers.Concat([child]).ToArray();
        provider.Enqueue(_ => Task.FromResult<IReadOnlyList<ControllerDevice>>(all));
        SetField(window, "_controllers", all);
        SetField(window, "_detectedControllers", all);

        Invoke(window, "RenderControllerList");
        Assert.Equal(controllers.Count, window.ControllerList.Children.Count);
        Assert.Equal(controllers.Count, Invoke<IReadOnlyList<ControllerDevice>>(
            window, "PresentationControllers").Count);
        Assert.Equal(controllers[1], Invoke<ControllerDevice?>(window, "LinkedParent", child));
        Assert.Single(Invoke<IReadOnlyList<ControllerDevice>>(window, "LinkedOutputs", controllers[1]));
        var data = new DataObject("ControllerBattery.ControllerDeviceKey", childKey);
        var targetCard = new Border { Tag = parentKey };
        Assert.True(InvokeStatic<bool>(typeof(MainWindow), "HasControllerDrag", data));
        Assert.True(Invoke<bool>(window, "CanLinkDrop", targetCard, data));
        var dragSource = new Border { Tag = childKey };
        Assert.False(window.TryGetDragDeviceKey(dragSource, MouseButtonState.Released,
            new Point(100, 100), out _));
        Assert.False(window.TryGetDragDeviceKey(new Border(), MouseButtonState.Pressed,
            new Point(100, 100), out _));
        Assert.False(window.TryGetDragDeviceKey(targetCard, MouseButtonState.Pressed,
            new Point(100, 100), out _));
        Assert.False(window.TryGetDragDeviceKey(dragSource, MouseButtonState.Pressed,
            new Point(0, 0), out _));
        Assert.True(window.TryGetDragDeviceKey(dragSource, MouseButtonState.Pressed,
            new Point(100, 100), out var draggedKey));
        Assert.Equal(childKey, draggedKey);
        InvokeStatic<object?>(typeof(MainWindow), "AnimateDropTarget", targetCard, 1.03);
        Invoke(window, "SetControllerParent", childKey, null);
        Assert.Null(Invoke<ControllerDevice?>(window, "LinkedParent", child));
        Invoke(window, "SetControllerParent", childKey, parentKey);
        var mouseUp = new System.Windows.Input.MouseButtonEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
        SetField(window, "_controllerDragStarted", true);
        Invoke(window, "ControllerCard_MouseLeftButtonUp", targetCard, mouseUp);
        var mouseDown = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
        Invoke(window, "ControllerDrag_MouseLeftButtonDown", dragSource, mouseDown);
        SetField(window, "_controllerDragStarted", false);
        Invoke(window, "ControllerCard_MouseLeftButtonUp", targetCard, mouseUp);
        Invoke(window, "ControllerCard_MouseLeftButtonUp", targetCard, mouseUp);

        var validDrop = CreateDragEvent(data, targetCard);
        validDrop.RoutedEvent = DragDrop.DragEnterEvent;
        Invoke(window, "ControllerCard_DragEnter", targetCard, validDrop);
        Assert.Equal(DragDropEffects.Move, validDrop.Effects);
        validDrop.RoutedEvent = DragDrop.DragLeaveEvent;
        Invoke(window, "ControllerCard_DragLeave", targetCard, validDrop);
        validDrop.RoutedEvent = DragDrop.DropEvent;
        Invoke(window, "ControllerCard_Drop", targetCard, validDrop);
        var invalidDrop = CreateDragEvent(new DataObject(), targetCard);
        invalidDrop.RoutedEvent = DragDrop.DragEnterEvent;
        Invoke(window, "ControllerCard_DragEnter", targetCard, invalidDrop);
        Assert.Equal(DragDropEffects.None, invalidDrop.Effects);
        validDrop.RoutedEvent = DragDrop.DropEvent;
        Invoke(window, "LeftNavSurface_Drop", window.LeftNavSurface, validDrop);
        var feedback = CreateFeedbackEvent();
        feedback.RoutedEvent = DragDrop.GiveFeedbackEvent;
        Invoke(window, "ControllerDrag_GiveFeedback", targetCard, feedback);
        Assert.True(feedback.Handled);

        foreach (var controller in all)
        {
            Invoke(window, "ShowControllerDetail", controller, true);
            Assert.Equal(controller.Name, window.DetailName.Text);
        }
        Invoke(window, "ShowOverview");
        Assert.Equal(Visibility.Visible, window.WelcomePanel.Visibility);
        Assert.Equal(all.Length, Invoke<IReadOnlyList<ControllerDevice>>(
            window, "ApplyProfiles", (object)all).Count);
        Invoke(window, "ShowControllerConnectionChanges");
        SetField(window, "_controllers", Array.Empty<ControllerDevice>());
        Invoke(window, "ShowControllerConnectionChanges");
        Invoke(window, "UpdatePollingText");
        Invoke(window, "UpdateOverlayTip");
        Invoke(window, "SetModalBackdrop", true);
        Invoke(window, "SetModalBackdrop", false);
        Invoke(window, "ToggleMaximized");
        Invoke(window, "ToggleMaximized");
        Invoke(window, "MinimizeButton_Click", window, new RoutedEventArgs());
        Invoke(window, "MaximizeButton_Click", window, new RoutedEventArgs());
        Invoke(window, "MainWindow_StateChanged", window, EventArgs.Empty);
        var titleDoubleClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
        typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!
            .GetSetMethod(nonPublic: true)!.Invoke(titleDoubleClick, [2]);
        Invoke(window, "TitleBar_MouseLeftButtonDown", window, titleDoubleClick);
        window.WindowState = WindowState.Minimized;
        Invoke(window, "MainWindow_StateChanged", window, EventArgs.Empty);
        window.WindowState = WindowState.Normal;
        var handled = false;
        Invoke(window, "WindowMessageHook", IntPtr.Zero,
            DeviceNotificationInterop.WmDeviceChange, new IntPtr(0x8000), IntPtr.Zero, handled);
        Invoke(window, "MonitoringService_ScanFailed", window,
            new ControllerScanErrorEventArgs(new IOException("expected scan failure")));
        window.Dispatcher.Invoke(() => { });
        SetField(window, "_selectedId", ControllerProfileService.DeviceKey(controllers[^1]));
        Invoke(window, "PowerOffButton_Click", window, new RoutedEventArgs());
        Invoke(window, "IdentifyButton_Click", window, new RoutedEventArgs());
        InvokeTask(window, "ApplyPendingProfileLedsAsync", TestContext.Current.CancellationToken);
        var profileController = controllers[1];
        var profileKey = ControllerProfileService.DeviceKey(profileController);
        window.ApplyProfile(profileController, new ControllerProfile(profileKey, "Renamed",
            "#445566", "PlayStation", LedColor: "#112233", LedBrightness: 1));
        Assert.Contains(window.ControllerList.Children.OfType<Border>(), card =>
            Equals(card.Tag, profileKey));
        window.ApplyProfile(profileController, new ControllerProfile(profileKey, null,
            ControllerProfile.DefaultAccentColor));
        InvokeTask(window, "PreviewLedColorAsync", controllers[0], "#112233", (byte)1);
        GetField<System.Windows.Forms.NotifyIcon>(window, "_trayIcon").Dispose();
        GetField<System.Windows.Forms.ContextMenuStrip>(window, "_trayMenu").Dispose();
        GetField<ControllerMonitoringService>(window, "_monitoringService")
            .DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static void TestWpfHelpers()
    {
        var viewer = new ScrollViewer
        {
            Width = 100,
            Height = 100,
            Content = new Border { Width = 100, Height = 1000 }
        };
        viewer.Measure(new Size(100, 100));
        viewer.Arrange(new Rect(0, 0, 100, 100));
        viewer.UpdateLayout();
        SmoothScrollBehavior.SetIsEnabled(viewer, true);
        Assert.True(SmoothScrollBehavior.GetIsEnabled(viewer));
        var wheelDown = new System.Windows.Input.MouseWheelEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, 0, -120)
        { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        InvokeStatic<object?>(typeof(SmoothScrollBehavior), "Viewer_PreviewMouseWheel", viewer,
            wheelDown);
        Assert.True(wheelDown.Handled);
        var wheelUp = new System.Windows.Input.MouseWheelEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, 0, 120)
        { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        InvokeStatic<object?>(typeof(SmoothScrollBehavior), "Viewer_PreviewMouseWheel", viewer,
            wheelUp);
        InvokeStatic<object?>(typeof(SmoothScrollBehavior), "Viewer_PreviewMouseWheel", new Border(),
            wheelUp);
        InvokeStatic<object?>(typeof(SmoothScrollBehavior), "Viewer_ScrollChanged", viewer,
            CreateScrollChangedEvent(5));
        InvokeStatic<object?>(typeof(SmoothScrollBehavior), "Viewer_ScrollChanged", new Border(),
            CreateScrollChangedEvent(0));
        SmoothScrollBehavior.SetIsEnabled(viewer, false);
        Assert.False(SmoothScrollBehavior.GetIsEnabled(viewer));
        InvokeStatic<object?>(typeof(SmoothScrollBehavior), "OnIsEnabledChanged", new Border(),
            new DependencyPropertyChangedEventArgs(SmoothScrollBehavior.IsEnabledProperty,
                false, true));
        InvokeStatic<object?>(typeof(SmoothScrollBehavior), "OnAnimatedOffsetChanged", viewer,
            new DependencyPropertyChangedEventArgs(ScrollViewer.VerticalOffsetProperty, 0d, 10d));

        var source = new Border { Width = 120, Height = 50 };
        source.Measure(new Size(120, 50));
        source.Arrange(new Rect(0, 0, 120, 50));
        var popup = new ControllerDragPopup(source, source);
        Assert.IsType<Border>(popup.Child);
        popup.MoveToCursor();
        popup.IsOpen = false;
    }

    private static IEnumerable<ControllerDevice> ControllerCases()
    {
        yield return Device("xinput", "Xbox-compatible", null, BatteryLevel.Low, false, true, false);
        yield return Device("sony", "PlayStation", 55, BatteryLevel.Medium, true, true, true);
        yield return Device("switch", "Nintendo", null, BatteryLevel.Full, false, true, false);
        yield return Device("8bitdo", "8BitDo", 75, BatteryLevel.High, true, false, false);
        yield return Device("generic", "Generic", null, BatteryLevel.Unknown, false, false, false);
    }

    private static ControllerDevice Device(string id, string kind, int? percent, BatteryLevel level,
        bool charging, bool identify, bool led) => new(id, id, $"{kind} Pad", kind, "Wireless",
        percent, level, charging, DateTime.UnixEpoch, CanIdentify: identify, CanSetLed: led,
        AccentColor: "#A99CF8", ProfileIconKind: kind);

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Invoke(object target, string name, params object?[] args) =>
        Method(target.GetType(), name).Invoke(target, args);

    private static T Invoke<T>(object target, string name, params object?[] args) =>
        (T)Method(target.GetType(), name).Invoke(target, args)!;

    private static T InvokeStatic<T>(Type type, string name, params object?[] args) =>
        (T)Method(type, name).Invoke(null, args)!;

    private static void InvokeTask(object target, string name, params object?[] args) =>
        ((Task)Method(target.GetType(), name).Invoke(target, args)!).GetAwaiter().GetResult();

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(type.FullName, name);

    private static void SetField(object target, string name, object value) =>
        (target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
         ?? throw new MissingFieldException(target.GetType().FullName, name)).SetValue(target, value);

    private static T GetField<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, name)).GetValue(target)!;

    private static bool InvokeTryParse(string input, out Color color)
    {
        object?[] args = [input, null];
        var result = InvokeStatic<bool>(typeof(LedColorWindow), "TryParseHexColor", args);
        color = (Color)args[1]!;
        return result;
    }

    private static DragEventArgs CreateDragEvent(IDataObject data, DependencyObject target)
    {
        var constructor = typeof(DragEventArgs).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(IDataObject) ? data :
            parameter.ParameterType == typeof(DragDropKeyStates) ? DragDropKeyStates.None :
            parameter.ParameterType == typeof(DragDropEffects) ? DragDropEffects.Move :
            parameter.ParameterType == typeof(DependencyObject) ? target :
            parameter.ParameterType == typeof(Point) ? new Point() :
            Activator.CreateInstance(parameter.ParameterType)).ToArray();
        return (DragEventArgs)constructor.Invoke(arguments);
    }

    private static GiveFeedbackEventArgs CreateFeedbackEvent()
    {
        var constructor = typeof(GiveFeedbackEventArgs).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(DragDropEffects) ? (object)DragDropEffects.Move :
            parameter.ParameterType == typeof(bool) ? true :
            Activator.CreateInstance(parameter.ParameterType)).ToArray();
        return (GiveFeedbackEventArgs)constructor.Invoke(arguments);
    }

    private static ScrollChangedEventArgs CreateScrollChangedEvent(double verticalChange)
    {
        var constructor = typeof(ScrollChangedEventArgs).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var doubles = 0;
        var arguments = constructor.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(double)
                ? (object)(doubles++ == 5 ? verticalChange : 0d)
                : Activator.CreateInstance(parameter.ParameterType)).ToArray();
        return (ScrollChangedEventArgs)constructor.Invoke(arguments);
    }

    private static ExitEventArgs CreateExitEvent()
    {
        var constructor = typeof(ExitEventArgs).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters().Select(parameter =>
            Activator.CreateInstance(parameter.ParameterType)).ToArray();
        return (ExitEventArgs)constructor.Invoke(arguments);
    }
}
