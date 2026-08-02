# Controller Battery

A native Windows dashboard for monitoring controller batteries, identifying devices,
creating controller profiles, and checking charge levels from an in-game overlay.
Built with WPF and .NET 10.

## Features

- Displays connected controllers in a scrollable dashboard with battery level,
  charging state, connection type, and last-update time.
- Shows percentage estimates when the controller protocol provides enough battery
  detail and honest categorical levels when it does not.
- Polls automatically every 30 seconds by default, with a configurable interval from
  5 to 300 seconds and a manual Refresh button.
- Lets a selected controller be deselected to return to the splash screen.
- Provides a global, non-activating overlay with controller icons, profile colors,
  battery levels, and automatic polling updates.
- Supports a configurable overlay shortcut and placement in any corner of the primary
  display. The default shortcut is `Ctrl+Alt+B`.
- Shows the current overlay shortcut in the splash-screen tip.
- Displays low-battery notifications and sends a 450 ms attention rumble when the
  active provider supports it.
- Identifies supported controllers with a standardized 450 ms rumble pulse.
- Turns off supported Bluetooth controllers from the details view.
- Includes scrollable Settings, Profile, and About windows with consistent native UI,
  custom window chrome, styled scrollbars, and a build version in About.
- Includes release-build controller diagnostics for capturing raw HID reports and
  descriptors from any input device.

## Controller profiles

Profiles are stored per stable controller identifier and persist between launches.
Each controller can have:

- A custom display name.
- A profile accent color used by the dashboard, navigation icon, and overlay.
- A custom controller-family icon: automatic, Xbox, PlayStation, Nintendo, 8BitDo,
  or generic.
- DualSense LED lighting settings when native LED control is available.

### DualSense LED lighting

Directly connected DualSense and DualSense Edge controllers support:

- An independent custom LED color.
- Syncing the LED with the profile accent color.
- Saturated suggested colors.
- Exact `#RRGGBB`, `RRGGBB`, or shorthand `#RGB` input.
- Fine RGB sliders with live controller preview.
- Bright, Medium, and Dim intensity settings.
- Automatic reapplication after reconnecting.
- Restoring the standard blue lightbar when custom lighting is disabled and saved.

LED output is best effort. Steam Input, DS4Windows, games, or another application that
continuously owns controller output can overwrite the app's color.

## Grouping virtual controllers

Translation software such as DS4Windows can expose both a physical controller and a
virtual XInput controller. The app does not guess which devices belong together.

To group them, drag the XInput tile onto its physical controller in the left navigation.
The virtual output is then shown as a nested `Game output` row and hidden as a duplicate
in the overlay. The association persists between launches. Drag the nested row onto an
empty part of the left navigation to ungroup it. If its physical parent disconnects,
the virtual controller automatically returns as a standalone tile.

## Supported controllers

| Controller/provider | Connections | Battery detail | Available actions |
| --- | --- | --- | --- |
| Xbox-compatible/XInput | XInput, wireless adapters, USB adapters, virtual XInput | Empty, Low, Medium, or Full when XInput exposes it; many wired and virtual devices expose no battery | Identify/rumble |
| DualSense and DualSense Edge | Direct USB or Bluetooth HID | Estimated percentage and charging state from the native report | Identify, Bluetooth power-off, profile LED control |
| Official Switch Pro Controller | Direct USB or Bluetooth HID | Native five-level battery category and charging state | Identify, Bluetooth power-off |
| Native 8BitDo HID controllers | Bluetooth, USB, or 2.4 GHz modes | Percentage only when the active public protocol exposes a verified value | Monitoring varies by protocol |

Adapters and translation layers can hide the physical controller or omit its battery
telemetry. In those cases the app can only display what the exposed protocol reports.
An 8BitDo receiver operating as XInput, for example, commonly appears wired and does
not expose the paired controller's battery.

DualSense percentages are estimates from coarse firmware battery bands rather than
one-percent measurements. The app uses the midpoint interpretation also used by the
Sony-authored Linux PlayStation HID driver.

## Overlay notes

The overlay is non-activating and placed above ordinary desktop and borderless-window
applications. Exclusive fullscreen games control the final display
surface and may prevent normal desktop overlays from appearing. Borderless/windowed
fullscreen is the most compatible mode.

## Diagnostics

Controller diagnostics are available at the bottom of Settings in release builds.
The capture records device metadata, report sizes, raw input reports, and report
descriptor information where Windows and the device permit it. Files are written to:

```text
%LOCALAPPDATA%\ControllerBattery\diagnostics
```

Diagnostics can include hardware identifiers and raw controller data. Review a capture
before sharing it publicly.

## Run from source

Requirements:

- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

From PowerShell:

```powershell
dotnet run --project src/ControllerBattery/ControllerBattery.csproj
```

Build and publish with:

```powershell
dotnet build ControllerBattery.sln -c Release
dotnet publish src/ControllerBattery/ControllerBattery.csproj -c Release -r win-x64 --self-contained true
```

Run tests and collect coverage with:

```powershell
dotnet test ControllerBattery.sln
dotnet test ControllerBattery.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

For an optional local HTML report, install ReportGenerator and point it at the generated
Cobertura file:

```powershell
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

## Local data

Settings and controller profiles are stored under:

```text
%LOCALAPPDATA%\ControllerBattery
```

Saved settings include the overlay shortcut, overlay position, and polling interval.
Profile data includes names, colors, icons, LED preferences, and explicit virtual-device
grouping relationships.

## Repository structure and testing

- `src/ControllerBattery` is the WPF production project. `Views` owns windows and visual
  code-behind; `Models` owns normalized records; `Providers/Abstractions` owns capability
  contracts; `Providers` owns protocols; `Services` owns monitoring and business logic;
  `Interop` owns native helpers; and `Behaviors` owns reusable WPF behavior.
- `src/ControllerBattery/Assets` contains all original icons and source artwork. Runtime icons
  remain WPF `Resource` items and use `/Assets/...` pack paths; source artwork remains `None`.
- `tests/ControllerBattery.Tests` mirrors production concerns. `Fixtures` holds sanitized samples,
  `Fakes` holds controllable infrastructure, and the other folders hold focused tests.
- `docs/architecture.md` describes runtime boundaries and the coverage roadmap.

The post-refactor baseline is **12.01% line coverage (258/2148 lines)**. Only generated XAML and
compiler output are excluded; code-behind, providers, services, models, persistence, and parsing
are not broadly excluded. The long-term target is **90%**. New business logic, protocol parsing,
migrations, and bug fixes should include meaningful tests.

The next coverage areas are provider output generation, action coordination, persistence fault
injection, presentation-state projection, and isolated WPF behavior. Hardware enumeration,
Bluetooth disconnect, physical rumble/LED behavior, and overlay rendering over games require
manual Windows/controller validation.

Controller Battery is an independent project and is not affiliated with Sony,
Microsoft, Nintendo, 8BitDo, Valve, DS4Windows, or other controller manufacturers and
software vendors. Product names and trademarks belong to their respective owners.
