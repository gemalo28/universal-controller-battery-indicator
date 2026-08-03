# Controller Battery

Controller Battery is a Windows app for viewing controller battery levels, identifying
controllers, and keeping battery information visible while you play. It supports native
controller profiles, an in-game overlay, background tray operation, and configurable
notifications.

> Controller Battery requires Windows 10 or Windows 11. Battery detail and available
> actions depend on what each controller, adapter, and driver exposes to Windows.

## Highlights

- Monitors Xbox/XInput, DualSense, Switch Pro, and supported 8BitDo controllers.
- Shows battery level, charging state, connection type, and last update time.
- Detects most controller connections and disconnections immediately, with polling as
  a fallback.
- Provides a global battery overlay with configurable shortcut and screen corner.
- Runs in the system tray and can start quietly with Windows.
- Shows optional connection, disconnection, and low-battery notifications.
- Supports custom controller names, colors, icons, and persistent profiles.
- Groups physical controllers with virtual outputs created by tools such as DS4Windows.
- Supports identification, Bluetooth power-off, and DualSense LED control when the
  active controller provider supports those actions.

## Getting started

1. Start Controller Battery.
2. Connect or wake a supported controller.
3. Select the controller in the left sidebar to view its details and available actions.
4. Open **Settings** to choose your overlay shortcut, notification preferences, polling
   interval, and startup behavior.

The default overlay shortcut is `Ctrl+Alt+B`. Press it again to close the overlay.

Minimizing the main window sends the app to the notification area so monitoring can
continue in the background. Double-click the tray icon, or choose **Open Controller
Battery** from its menu, to restore the window. Choose **Exit** from the tray menu or use
the main window's close button to stop the app completely.

## Dashboard

The sidebar contains one card for each currently available controller. A card shows the
controller name, connection, icon, and battery status. Select a card for a larger battery
view and any actions supported by that device.

Use **Refresh** when you want to request a scan manually. The app also:

- Refreshes at the interval selected in Settings.
- Requests an immediate refresh for Windows HID and XInput/XUSB arrival or removal events.
- Consolidates the multiple device events Windows may emit for a single connection.

Polling remains enabled because some Bluetooth drivers, virtual controllers, and adapters
do not send reliable device-change events.

## Battery overlay

The overlay presents connected controllers without taking focus away from a game or other
application. It includes each controller's profile icon, profile color, connection, and
battery reading.

In **Settings**, you can change:

- The global keyboard shortcut.
- The overlay corner: top-left, top-right, bottom-left, or bottom-right.
- The controller polling interval.

When a fullscreen application is detected, the overlay is positioned on that application's
monitor. Placement is recalculated after layout and per-monitor DPI changes so it remains
anchored to the intended corner. Borderless or windowed fullscreen is the most compatible
display mode; an exclusive-fullscreen game may cover ordinary Windows overlays.

## Notifications

Controller Battery provides two independently configurable notification types:

- **Connect/disconnect notifications** show when a controller becomes available or is
  removed. The toast uses the controller's configured profile icon and name. It appears in
  the bottom-left corner of the monitor containing a fullscreen application, or on the
  active monitor when no fullscreen application is detected.
- **Low-battery notifications** appear when a controller first enters a low or empty
  battery state. Supported controllers also receive a short attention rumble.

Both notification types can be disabled under **Settings > Notifications**. State continues
to be tracked while a notification type is disabled, so turning it back on does not replay
old events. The initial startup scan does not produce connection notifications.

## Settings reference

| Setting | What it does |
| --- | --- |
| Quick-view shortcut | Sets the global shortcut that opens and closes the overlay. |
| Overlay location | Selects the overlay and low-battery alert corner. |
| Controller polling | Sets the fallback refresh interval from 5 to 300 seconds. |
| Start with Windows | Starts the app in the tray when the current Windows user signs in. |
| Connect/disconnect notifications | Enables or disables controller status toasts. |
| Low-battery notifications | Enables or disables low-battery alerts and their attention action. |
| Test low-battery notification | Previews the low-battery alert and its placement. |
| Capture controller diagnostics | Saves controller metadata and raw HID samples for troubleshooting. |

Starting with Windows is registered only for the current user and does not require
administrator privileges.

## Controller profiles

Open a controller and choose **Edit profile** to customize it. Profiles persist between
app launches and can contain:

- A custom display name.
- An accent color used by the dashboard, overlay, and controller icon.
- An automatic or explicit Xbox, PlayStation, Nintendo, 8BitDo, or generic icon.
- Supported DualSense LED settings.
- A relationship to a physical controller when the device is a virtual output.

### DualSense lighting

Directly connected DualSense and DualSense Edge controllers can use an independent LED
color or synchronize their LED with the profile accent. The color editor supports suggested
colors, hexadecimal input, RGB sliders, live preview, and Bright, Medium, and Dim intensity.

LED control is best effort. Steam Input, DS4Windows, a game, or another application may
continuously overwrite the lightbar output.

## Grouping virtual controllers

Translation software can expose both a physical controller and an additional XInput game
controller. Controller Battery does not automatically assume which two devices belong
together because XInput does not provide a reliable physical parent identity.

To group devices:

1. Find the virtual XInput controller in the left sidebar.
2. Drag it onto its physical controller.
3. Confirm that it appears as a nested **Game output** row.

The grouped output is hidden as a duplicate in the overlay and device count. Connection
toasts use the main physical controller's name and icon. Drag the nested output onto empty
sidebar space to ungroup it. If the physical controller is absent, its virtual output is
temporarily shown as a standalone controller.

## Controller support

| Controller/provider | Connections | Battery information | Available actions |
| --- | --- | --- | --- |
| Xbox-compatible/XInput | XInput, wireless/USB adapters, virtual XInput | Empty, Low, Medium, or Full when exposed; wired and virtual devices often expose no battery | Identify/rumble |
| DualSense and DualSense Edge | Direct USB or Bluetooth HID | Estimated percentage and charging state | Identify, Bluetooth power-off, profile LED control |
| Official Switch Pro Controller | Direct USB or Bluetooth HID | Native five-level battery category and charging state | Identify, Bluetooth power-off |
| Native 8BitDo HID controllers | Bluetooth, USB, or 2.4 GHz modes | Percentage only when the active protocol exposes a verified value | Varies by protocol |

Adapters and translation layers can hide the physical controller or omit its battery data.
Controller Battery reports only what the exposed protocol provides; it does not invent a
percentage when telemetry is unavailable. DualSense percentages are estimates derived from
coarse firmware battery bands rather than one-percent measurements.

## Troubleshooting

### A controller does not appear

- Wake or reconnect it, then select **Refresh**.
- Confirm that Windows recognizes it in game-controller or Bluetooth settings.
- Close software that may have exclusive access to its HID interface.
- If using HidHide, confirm that Controller Battery is allowed to see the physical device.

### Battery status is unavailable

Many wired controllers, XInput adapters, virtual outputs, and wireless receivers do not
forward battery telemetry. This is a limitation of the exposed device protocol rather than
a polling failure.

### The overlay does not appear above a game

Try borderless or windowed fullscreen. Exclusive-fullscreen applications may own the final
display surface and cover ordinary Windows topmost windows.

### A custom LED color does not stay applied

Steam Input, DS4Windows, games, and other controller tools may also write lighting packets.
Whichever application writes most recently controls the visible lightbar.

## Diagnostics and local data

Settings, profiles, logs, and diagnostics are stored under:

```text
%LOCALAPPDATA%\ControllerBattery
```

Diagnostic captures can contain device paths, hardware identifiers, serial numbers, and raw
controller reports. Review a capture before sharing it publicly.

## For developers

See the [Developer guidelines](docs/development.md) for source setup, watch mode, builds,
tests, project boundaries, contribution rules, and hardware validation. The deeper runtime
design is documented in [Application architecture](docs/architecture.md).

Controller Battery is an independent project and is not affiliated with Sony, Microsoft,
Nintendo, 8BitDo, Valve, DS4Windows, BetterJoy, or other controller manufacturers and
software vendors. Product names and trademarks belong to their respective owners.
