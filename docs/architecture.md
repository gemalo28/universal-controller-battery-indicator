# Provider architecture

The application treats a controller as a normalized domain object, not as an Xbox,
PlayStation, Nintendo, or vendor-specific UI case.

## Layers

1. **Protocol providers** discover devices through one API or protocol. Examples are
   XInput, Windows.Gaming.Input, raw HID, Bluetooth LE, and vendor SDKs.
2. **Composite provider** runs every enabled provider independently. One unavailable
   or faulty backend cannot suppress results from the others.
3. **Reconciliation** owns duplicate matching when the same physical controller is
   visible through multiple APIs. Provider-scoped IDs are safe today; stable hardware
   IDs will enable cross-provider merging later.
4. **Normalized model** carries display and battery observations to the UI. The UI
   never calls native controller APIs or branches on a controller brand.
5. **Presentation surfaces** share the normalized observations. The main window owns
   scanning, while the global-hotkey overlay renders the latest snapshot and requests
   a background refresh when opened.

## Adding a backend

Implement `IControllerProvider` under `src/Providers`, translate native results into
`ControllerDevice`, and register the provider in `CreateHardwareProvider`. Keep native
interop and protocol quirks inside that provider.

Battery data may be exact, coarse, or unavailable depending on the transport. Do not
invent a percentage when a device or adapter does not expose one; return `null` with a
useful `BatteryNote`.

Optional actions are capability-based too. A provider implements
`IPowerOffControllerProvider` and sets `CanPowerOff` only for a device and transport
it can safely address. The composite routes the request back to the owning provider;
the UI contains no vendor-specific shutdown logic.

The normalized model preserves two displayable outcomes: a percentage or a coarse
Empty/Low/Medium/High/Full level. Protocols with approximately ten or more battery steps
may normalize those steps to a displayed percentage. Lower-resolution protocols
remain categorical and are never converted into percentages.

## Planned providers

- XInput (implemented): Xbox-compatible devices and adapters.
- Windows.Gaming.Input: broader Windows gamepad discovery.
- Raw HID (DualSense and Switch Pro implemented): native Sony and Nintendo USB and
  Bluetooth reports; future HID providers will cover additional controller families.
- Bluetooth: transport metadata and supported battery services.
- Vendor adapters: device-specific reports when generic APIs hide battery state.

## 8BitDo transport behavior

The 8BitDo HID provider discovers native gamepad/joystick collections under vendor
ID `2DC8`. It deliberately ignores Windows `IG_` companion collections because those
belong to an XInput device already discovered by the XInput provider.

An 8BitDo 2.4 GHz receiver can therefore be discovered in either native HID/DInput
mode or XInput mode. Battery availability is a separate capability: many receiver
and controller combinations do not forward battery data through the public input
report. The provider reports battery as unavailable until a model-specific, verified
protocol parser exists. Bluetooth is not assumed to expose battery merely because
the transport is wireless.

Enhanced 8BitDo HID reports use IDs `01`, `03`, or `04`. For compatible firmware,
byte 14 contains an exact percentage in its low seven bits and the charging flag in
its high bit. XInput-style `IG_` collections are ignored unless they emit this
verified enhanced format, preventing duplicate controller entries.
