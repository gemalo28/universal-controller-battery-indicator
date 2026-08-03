# Contributing to Controller Battery

Thanks for helping improve Controller Battery. Bug reports, controller compatibility
results, documentation improvements, and focused pull requests are welcome.

## Before contributing

- Search existing issues before opening a duplicate.
- Do not post controller serial numbers, Bluetooth addresses, personal device paths, or
  unreviewed diagnostics publicly.
- Use the security-reporting process in [SECURITY.md](SECURITY.md) for vulnerabilities.
- Read the [developer guidelines](docs/development.md) and
  [architecture documentation](docs/architecture.md) before changing runtime boundaries.

## Development workflow

1. Fork the repository and create a focused branch.
2. Make the change with tests for testable behavior.
3. Run `dotnet test ControllerBattery.sln -c Release`.
4. Run the coverage gate documented in `docs/development.md`.
5. Manually validate affected controller, tray, overlay, fullscreen, or DPI behavior when
   hardware or presentation code changes.
6. Open a pull request describing the behavior, tests, and any manual validation.

The CI line-coverage requirement is 90%. Do not exclude handwritten application code to
meet the threshold.

## Controller reports

When reporting compatibility, include the controller model, connection method, Windows
version, and whether Steam Input, DS4Windows, BetterJoy, HidHide, or a similar translation
layer is active. Remove personal identifiers from logs and diagnostic captures.

By submitting a contribution, you agree that it may be distributed under the project's
[MIT License](LICENSE).
