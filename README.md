# Universal Controller Battery Indicator

A native .NET 10 WPF prototype for viewing controller battery levels in one place.

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
From PowerShell:

```powershell
dotnet run
```

Press **Ctrl+Alt+B** while the app is running to toggle the compact battery overlay
above the Windows taskbar. The shortcut is system-wide and can be changed under
**Settings** by pressing the desired modifier-and-key combination. It is saved under
the current user's local application data and takes effect immediately.
The overlay can be placed in any corner of the primary screen; bottom right is the
default.

Controller polling defaults to every **15 seconds**. It can be changed under
**Settings** to any value from 5 to 300 seconds and takes effect immediately.

Select a connected controller and choose **Edit profile** to give it a custom
name or color. Profiles are matched to the provider's stable device identifier,
persist between launches, and are reflected in both the dashboard and overlay.

Build and publish with:

```powershell
dotnet build
dotnet publish -c Release -r win-x64 --self-contained true
```

## Architecture

- `ControllerBattery.csproj` configures the .NET 10 WPF application.
- `src/MainWindow.xaml` contains the native Windows interface.
- `src/MainWindow.xaml.cs` owns presentation behavior.
- `src/OverlayWindow.xaml` contains the non-activating, always-on-top quick view.
- `src/SettingsWindow.xaml` captures and updates the global overlay shortcut.
- `src/ProfileWindow.xaml` edits controller names and profile colors.
- `src/Models` contains the normalized controller model.
- `src/Providers` contains the provider contract and current implementations.
- `docs/architecture.md` defines how additional controller backends plug in.

The first real provider detects up to four XInput controllers, including adapters
that emulate Xbox controllers. XInput only reports coarse battery levels. Some USB
adapters report as wired and do not forward the paired controller's battery; those
devices still appear, with their battery marked unavailable.

The Sony HID provider detects standard DualSense and DualSense Edge controllers
connected directly over USB or Bluetooth. It reads the native full input report for
battery level and charging state. A controller hidden behind an emulating adapter is
only visible through whichever protocol that adapter exposes.

Additional providers can cover Windows.Gaming.Input, Bluetooth/HID, and other
controller-specific protocols. Each adapter returns the same `ControllerDevice`
model, keeping the UI independent of device family.

Battery presentation prefers a percentage. Protocols with approximately ten or more
battery steps are normalized to percentages; lower-resolution protocols fall back to
Empty, Low, Medium, High, or Full.

The 8BitDo provider discovers native 8BitDo HID gamepads over Bluetooth, USB, and
2.4 GHz receiver modes. Receivers operating as XInput remain owned by the XInput
provider to prevent duplicate entries. Battery is shown only when the active public
protocol supplies a verified value; most XInput receiver modes do not.

The Nintendo HID provider supports the official Switch Pro Controller (`057E:2009`)
over direct Bluetooth and USB connections. It performs the wired Nintendo handshake
and requests full report mode when possible, then reads the native five battery bands
and charging bit. Those bands remain categorical rather than made-up percentages.
Controllers hidden behind an emulating adapter are limited to whatever it exposes.

## Turning controllers off

The details view shows **Turn off controller** only when the active transport has a
verified shutdown command. Direct Bluetooth Switch Pro and DualSense controllers are
currently supported. USB devices remain powered by their cable, while XInput and
8BitDo receiver modes do not expose a stable, documented per-device shutdown command.
