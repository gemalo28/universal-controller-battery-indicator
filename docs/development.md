# Developer guidelines

This guide covers local development and contribution expectations for Controller Battery.
For runtime design and provider-specific details, also read
[Application architecture](architecture.md).

## Prerequisites

- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PowerShell, Windows Terminal, or another shell capable of running the .NET CLI
- Physical controllers for provider/output validation when changing hardware behavior

The production application targets `net10.0-windows`, uses WPF, and calls Windows APIs.
Development and hardware validation therefore need a Windows environment.

## Run locally

From the repository root:

```powershell
dotnet restore ControllerBattery.sln
dotnet run --project src/ControllerBattery/ControllerBattery.csproj
```

Watch mode rebuilds and restarts after source changes:

```powershell
dotnet watch --project ./src/ControllerBattery/ControllerBattery.csproj run
```

Use forward slashes in Git Bash or similar shells. Backslashes can be interpreted as escape
characters and turn the project path into an invalid filename.

## Build, test, and publish

```powershell
dotnet build ControllerBattery.sln -c Release
dotnet test ControllerBattery.sln -c Release
dotnet publish src/ControllerBattery/ControllerBattery.csproj -c Release -r win-x64 --self-contained true
```

Collect coverage with the checked-in settings:

```powershell
dotnet test ControllerBattery.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

For an optional local HTML report:

```powershell
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

Do not commit build, publish, coverage-output, or user-data directories.

## Repository layout

```text
src/ControllerBattery/
  Assets/                  Runtime icons and source artwork
  Behaviors/               Reusable WPF behaviors
  Interop/                 Windows native API boundaries
  Models/                  Normalized records and enums
  Providers/               XInput and HID protocol implementations
  Providers/Abstractions/  Provider and optional-capability contracts
  Services/                Monitoring, persistence, transitions, and actions
  Views/                   WPF windows and presentation code-behind
tests/ControllerBattery.Tests/
  Fakes/                   Deterministic provider, clock, timer, and filesystem fakes
  Fixtures/                Sanitized protocol samples
  Models, Providers, ...   Tests mirroring production concerns
docs/                      User-linked technical documentation
```

## Design boundaries

- Providers translate platform/protocol observations into `ControllerDevice`. Keep report
  parsing, transport quirks, timeouts, and native device access inside the provider.
- Models must not depend on WPF views or hardware implementations.
- Services own testable policy such as monitoring, transition tracking, persistence,
  profile projection, and capability routing.
- Interop classes should expose small managed operations instead of leaking native structs
  and constants throughout presentation code.
- Views own rendering, window lifetime, gestures, dialogs, animations, and dispatcher
  boundaries.
- Optional behavior is capability-driven. Check `CanIdentify`, `CanPowerOff`, and
  `CanSetLed`; do not branch UI behavior only on vendor names.
- Do not merge observations across providers without a verified shared identity. XInput
  indexes are transient and cannot reliably identify their physical parent.

## Coding guidelines

- Keep nullable reference types enabled and address warnings instead of suppressing them
  broadly.
- Make asynchronous hardware and monitoring operations cancellation-aware.
- Stop rumble and other temporary output in `finally` paths where applicable.
- Preserve partial-provider isolation: one unavailable backend must not hide successful
  results from other providers.
- Report battery precision honestly. Use `BatteryLevel.Unknown` and a useful note when a
  protocol does not expose verified telemetry.
- Marshal scan events to the WPF dispatcher before changing windows or controls.
- Treat device-arrival events as a fast refresh trigger, not a replacement for polling.
- Debounce Windows Plug-and-Play events because one controller can expose several device
  interfaces.
- Reuse application-level styles and existing controller icon/profile projection rather
  than introducing window-specific visual variants.
- Use `DisplayPlacementService` for overlay/toast positioning so fullscreen-monitor and
  per-monitor-DPI handling remain consistent.

## Settings and persistence compatibility

User data lives in `%LOCALAPPDATA%\ControllerBattery`:

- `settings.json` stores the overlay shortcut/position, polling interval, Windows startup
  preference, and notification preferences.
- `controller-profiles.json` stores names, colors, icon overrides, LED preferences, and
  virtual-device parent relationships.

When extending `AppSettings`, add new positional-record values at the end and provide a
default so older JSON remains loadable. Normalize untrusted loaded values in the relevant
service. Persistence writes should remain atomic and retain recovery behavior.

Windows startup uses the current user's `Run` registry key and launches with the
`--background` argument. Keep startup registration per-user and non-elevated.

## Adding or changing a provider

1. Implement `IControllerProvider` under `Providers`.
2. Return a stable device ID whenever the transport exposes one.
3. Populate connection and battery fields only from verified data.
4. Add only the optional provider interfaces the transport safely supports.
5. Set matching capability flags on each returned `ControllerDevice`.
6. Make I/O bounded, cancellation-aware, and tolerant of another app owning output.
7. Register the provider in `ControllerProviderFactory`.
8. Add parsing and behavior tests using sanitized or specification-derived fixtures.
9. Verify that an ordinary provider failure remains isolated by the composite provider.

Never commit serial numbers, Bluetooth addresses, personal device paths, or captures from a
user's machine as fixtures.

## Testing expectations

Run the full test suite before handing off a change:

```powershell
dotnet test ControllerBattery.sln -c Release
```

New business logic, protocol parsing, migrations, persistence behavior, and bug fixes should
include focused tests. Prefer deterministic fakes over real time, hardware enumeration, or
starting WPF windows in unit tests.

Automated tests do not replace manual Windows validation for:

- Physical connection and disconnection events
- Bluetooth pairing and power-off
- Rumble and attention pulses
- DualSense LED output and conflicts with other controller software
- Tray minimize/restore and start-with-Windows behavior
- Global hotkey registration
- Multiple monitors, mixed DPI scaling, fullscreen detection, overlays, and toasts
- Drag-and-drop grouping of virtual outputs

## Manual UI checklist

For presentation or lifecycle changes, verify at least the affected items:

- Main window opens, minimizes to the tray, restores, and exits cleanly.
- Background startup creates no persistent taskbar window.
- Settings save and survive restart; Cancel leaves previous values unchanged.
- Overlay opens and closes from the configured global shortcut.
- Overlay and toasts anchor to the correct monitor/corner at 100% and mixed DPI scales.
- Connection notifications do not appear on the initial scan.
- Disabled notification types remain silent and do not replay stale transitions.
- Grouped outputs use the main controller's name and icon in presentation/notifications.
- Polling still detects changes when Windows device notifications are unavailable.

## Pull request checklist

- Keep the change focused and preserve unrelated working-tree edits.
- Update the user manual when behavior or settings change.
- Update architecture documentation when ownership or data flow changes.
- Add or update tests for testable behavior.
- Run a Release build/test pass and check the diff for accidental generated files or
  sensitive hardware data.
- Describe any manual controller, fullscreen, DPI, tray, or Bluetooth validation performed.
