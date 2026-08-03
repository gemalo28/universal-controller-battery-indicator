# Changelog

Notable changes to Controller Battery are documented here. The project follows semantic
versioning while it approaches its first stable release.

## [Unreleased]

### Added

- Automated GitHub release packaging for portable and per-user installer downloads.
- SHA-256 checksums and GitHub build-provenance attestations for release artifacts.

## [0.3.0]

### Added

- Xbox/XInput, DualSense, Switch Pro, and supported 8BitDo battery monitoring.
- Fullscreen-aware overlay and controller connection/disconnection notifications.
- Low-battery alerts with optional attention rumble.
- System-tray background operation and optional start with Windows.
- Controller profiles, custom icons and colors, virtual-output grouping, and DualSense
  LED controls.
- Immediate Windows device-change refresh with polling fallback.

### Changed

- Expanded user and developer documentation.
- Added a mandatory 90% line-coverage gate with parameterized provider and WPF integration
  tests.

[Unreleased]: https://github.com/gemalo28/universal-controller-battery-indicator/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/gemalo28/universal-controller-battery-indicator/releases/tag/v0.3.0
