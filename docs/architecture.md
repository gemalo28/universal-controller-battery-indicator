# Application architecture

Controller Battery treats every controller as a normalized domain object. Protocol
details stay inside providers; presentation code consumes one shared model regardless
of whether a device is exposed through XInput, Sony HID, Nintendo HID, or 8BitDo HID.

## Runtime composition

`MainWindow.CreateHardwareProvider` constructs a `CompositeControllerProvider` from:

- `XInputControllerProvider`
- `DualSenseHidProvider`
- `EightBitDoHidProvider`
- `NintendoSwitchProHidProvider`

The composite scans all providers concurrently and isolates ordinary backend failures.
A busy or unsupported HID backend therefore does not prevent controllers owned by the
other providers from appearing. Cancellation requested by the application is still
propagated.

Composite identity is provider-scoped:

```text
{ProviderId}:{ControllerDevice.Id}
```

This key is also used for selection, profiles, LED state, notification state, and
explicit virtual-device relationships.

## Controller data flow

1. A `DispatcherTimer` in `MainWindow` starts a scan every configured polling interval.
   The default is 30 seconds; Settings permits 5–300 seconds.
2. Each provider translates its native observation into `ControllerDevice`.
3. `CompositeControllerProvider` combines provider results and removes duplicate
   observations that have the same provider-scoped key.
4. `MainWindow.ApplyProfiles` overlays presentation-only profile data such as custom
   name, accent, and icon onto the detected snapshot.
5. Pending DualSense LED profiles and low-battery transitions are processed.
6. The navigation, selected detail view, counters, and visible overlay are rendered
   from the same refreshed snapshot.

The overlay does not own a separate polling loop. A visible `OverlayWindow` receives
every successful main polling snapshot, and opening it also requests a fresh provider
scan. This keeps the dashboard and overlay consistent.

## Normalized model

`ControllerDevice` contains:

- Provider and device identity.
- Display name and controller family.
- Connection/transport text.
- Optional percentage and normalized `BatteryLevel`.
- Charging state, update time, and an explanatory battery note.
- Optional stable hardware ID.
- Capability flags for shutdown, identification, and LED control.
- Profile-projected accent color and icon kind.

Battery presentation preserves two valid outcomes:

- `BatteryPercent` when a protocol has enough verified resolution.
- `BatteryLevel` (`Empty`, `Low`, `Medium`, `High`, `Full`, or `Unknown`) when it does
  not.

Providers must not invent a percentage for an adapter that exposes no battery data.
Return `null`, `Unknown`, and a useful `BatteryNote` instead.

DualSense battery percentages are midpoint estimates from coarse native firmware
bands. XInput exposes only categorical levels. Switch Pro stays categorical because
its native report has five battery bands.

## Provider contracts and optional capabilities

Every backend implements:

```csharp
IControllerProvider
```

Optional behavior is expressed through provider interfaces and per-device flags:

- `IPowerOffControllerProvider` with `ControllerDevice.CanPowerOff`
- `IAttentionPulseControllerProvider` with `ControllerDevice.CanIdentify`
- `IControllerLedProvider` with `ControllerDevice.CanSetLed`

The composite routes each operation to the provider whose `Id` matches the device's
`ProviderId`. The UI tests capabilities rather than branching on vendor names.

Identification and low-battery attention use the shared constants in
`ControllerIdentification`: a 450 ms pulse with 50 ms keepalive writes. Providers own
their transport-specific rumble packets and timeout behavior.

Power-off is exposed only when a transport has a verified implementation. Bluetooth
DualSense and Switch Pro devices disconnect through `WindowsBluetooth`; USB devices
remain powered by their cable.

## Provider-specific behavior

### XInput

`XInputControllerProvider` enumerates user indexes 0–3, reads XInput's battery type and
four-level battery state, and sends identification rumble through `XInputSetState`.
XInput provides no stable hardware identity, numeric percentage, or relationship to a
physical device behind a virtual controller.

Wired adapters and virtual XInput devices commonly report no battery. They remain
visible with a descriptive unavailable state.

### DualSense and DualSense Edge

`DualSenseHidProvider` supports Sony product IDs `054C:0CE6` and `054C:0DF2` over USB
and Bluetooth. It:

- Reads native full input reports for battery and charging state.
- Requests the Bluetooth calibration feature when necessary to enable full reports.
- Writes transport-specific rumble and lightbar output reports.
- Adds the required Bluetooth output tag and CRC32.
- Disconnects Bluetooth controllers on power-off.

LED color is represented as a base `#RRGGBB` value plus an intensity selection.
Brightness is applied by scaling RGB channels because the separate DualSense intensity
byte is not consistently honored by controller firmware. The provider applies 100%,
60%, or 30% scaling for Bright, Medium, and Dim.

Disabling custom LED control restores an explicit standard blue lightbar. This is
intentional: releasing LED control on Windows caused tested controllers to turn the
lightbar off rather than replay their firmware startup state.

LED output is best effort. DS4Windows, Steam Input, and games can continuously send
their own output reports and overwrite the app's color.

### Switch Pro

`NintendoSwitchProHidProvider` supports the official `057E:2009` controller over USB
and Bluetooth. It performs the wired handshake, requests full report mode, parses the
native five-band battery/charging state, sends identification rumble, and supports
Bluetooth power-off.

### 8BitDo

`EightBitDoHidProvider` discovers native gamepad/joystick collections under vendor ID
`2DC8`. It deliberately avoids ordinary Windows `IG_` companion collections because
those belong to an XInput device already discovered by the XInput provider.

Enhanced 8BitDo reports with IDs `01`, `03`, or `04` can expose an exact percentage in
the low seven bits of byte 14 and charging in its high bit. An `IG_` collection is kept
only when it actually emits this verified enhanced format.

An 8BitDo receiver can operate as native HID/DInput or XInput. Many receiver and
controller combinations do not forward battery telemetry, so wireless transport alone
is never treated as proof that battery information is available.

## Profiles and persistence

`ControllerProfileStore` serializes profiles to:

```text
%LOCALAPPDATA%\ControllerBattery\controller-profiles.json
```

`ControllerProfile` stores:

- Custom name
- Accent color
- Controller-family icon override
- Independent LED color
- LED brightness
- Profile-color LED synchronization
- Explicit virtual-controller parent key

Loaded values are normalized before use: names are trimmed and limited, colors must be
valid six-digit hexadecimal values, icon kinds are allow-listed, brightness is clamped,
self-parenting is rejected, and the legacy `UseAccentForLed` representation is migrated.

Profile values do not mutate provider observations. `ApplyProfiles` creates projected
`ControllerDevice` records for presentation while `_detectedControllers` retains the
raw provider snapshot.

## Virtual-controller grouping

Automatic physical-to-XInput matching is intentionally avoided. XInput provides no
parent hardware identifier, and battery availability is not a safe proxy: a real wired
Xbox controller can look like a battery-less virtual device.

The user explicitly associates devices by dragging an XInput tile onto a physical tile.
The relationship is persisted as `ControllerProfile.ParentDeviceKey`. Presentation then:

- Omits a linked child from the top-level navigation.
- Renders it as a clickable `Game output` row beneath the connected parent.
- Omits the duplicate child from the overlay and displayed device count.
- Restores the child as a standalone tile whenever its saved parent is absent.

Dragging the nested output onto unused navigation space clears the relationship. The
drag visual is a transparent top-level `ControllerDragPopup`, rather than an adorner,
so it remains visible during WPF's native OLE drag loop. It is offset from the pointer
to keep the underlying drop target's HWND reachable.

## Settings, hotkey, and display placement

`AppSettingsStore` persists settings to:

```text
%LOCALAPPDATA%\ControllerBattery\settings.json
```

`MainWindow` registers the global overlay shortcut with `RegisterHotKey` and rejects a
new shortcut if another application owns it. Changes to the shortcut, corner, or
polling interval apply immediately.

`DisplayPlacementService` places both the overlay and low-battery notification in the
selected corner of the primary work area and restores topmost placement. The overlay
is non-activating and uses tool-window/no-activate extended styles. Exclusive fullscreen
games can still own the final display surface and cover ordinary desktop overlays.

## Notifications

`MainWindow` tracks controller keys currently in a low state. A notification is shown
only on transition into `Empty` or `Low`, preventing a popup on every poll. The state is
removed when the controller recovers or disconnects, allowing a future low transition
to notify again.

`LowBatteryNotificationWindow` automatically dismisses after eight seconds. Its corner
matches the overlay setting, and Settings can show a test notification.

## Diagnostics

`ControllerDiagnosticsService` is a release-accessible, provider-independent capture
tool exposed at the bottom of Settings. It enumerates HID input devices and records
metadata, report sizes, raw reports, and report descriptors where available. Captures
are written to:

```text
%LOCALAPPDATA%\ControllerBattery\diagnostics
```

Descriptor reconstruction and device access are best effort. Failures are recorded
instead of aborting the entire capture.

## Presentation structure

- `MainWindow` owns scanning, selection, profile projection, grouping, notifications,
  LED reapplication, the hotkey, and overlay lifetime.
- `OverlayWindow` renders the current normalized snapshot without activating.
- `ProfileWindow` edits identity, appearance, and capability-gated LED preferences.
- `LedColorWindow` provides suggested colors, editable hexadecimal input, RGB sliders,
  live HID preview, and OK/Cancel restoration behavior.
- `SettingsWindow` edits hotkey, placement, polling, notification testing, and diagnostics.
- `AboutWindow` reports the application version, supported providers, and notices.
- `SmoothScrollBehavior` provides shared scroll behavior for scrollable windows.
- `DisplayPlacementService` centralizes screen-corner positioning.

## Adding a provider

1. Implement `IControllerProvider` under `src/Providers`.
2. Keep native interop, discovery, report parsing, timeouts, and protocol quirks inside
   the provider.
3. Return stable IDs whenever the transport exposes them.
4. Populate both percentage and categorical fields honestly; do not infer unavailable
   battery telemetry from connection type.
5. Implement only the optional capability interfaces the backend can safely support,
   and set matching `ControllerDevice` capability flags per transport.
6. Make output operations cancellation-aware and ensure rumble is stopped in `finally`.
7. Register the provider in `MainWindow.CreateHardwareProvider`.
8. Verify that backend failure does not suppress other provider results and that the
   same physical collection is not emitted twice by that provider.

Cross-provider automatic merging should not be added without a verified shared hardware
identity. Prefer an explicit user relationship when APIs expose only transient indexes.
