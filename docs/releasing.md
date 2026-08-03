# Release guide

Controller Battery publishes a self-contained Windows x64 portable archive and a per-user
installer. Users do not need to install the .NET runtime separately.

## Release artifacts

Every release contains:

- `ControllerBattery-VERSION-win-x64-portable.zip`
- `ControllerBattery-VERSION-win-x64-Setup.exe`
- `SHA256SUMS.txt`
- GitHub build-provenance attestations

The installer writes application files to `%LOCALAPPDATA%\Programs\ControllerBattery`,
does not require elevation, and leaves settings and profiles in
`%LOCALAPPDATA%\ControllerBattery` when the application is upgraded or removed.
Uninstall removes the application's current-user startup entry so Windows is not left with
a command pointing to a deleted executable.

## Prepare a release

1. Ensure `main` is clean and its CI workflow passes.
2. Update `CHANGELOG.md` and move completed entries from `Unreleased` into the release.
3. Set `<Version>` in `src/ControllerBattery/ControllerBattery.csproj`.
4. Complete the manual UI and hardware checklist in `docs/development.md`.
5. Build and inspect the portable artifact locally:

   ```powershell
   ./scripts/Build-Release.ps1
   ```

6. If Inno Setup 6 is installed, compile `installer/ControllerBattery.iss` using the
   publish and release directories created by the script.

## Publish through GitHub

Create and push an annotated tag whose name exactly matches the project version:

```powershell
git tag -a v0.3.0 -m "Controller Battery v0.3.0"
git push origin v0.3.0
```

The `release` workflow independently restores, builds, tests, enforces coverage, publishes
the app, compiles the installer, generates checksums and attestations, and creates a draft
GitHub Release. It refuses a tag that does not match the project version.

Download both assets from the draft and test them on a clean Windows x64 machine. Verify
startup, tray restore/exit, settings persistence, controller discovery, overlay placement,
and uninstall behavior. Publish the draft only after this smoke test.

Tags and published binaries are immutable release inputs. Fix a bad release with a new
patch version instead of silently replacing an existing published asset.

## Signing

The workflow currently produces unsigned preview binaries. Do not store signing keys or
certificate passwords in the repository. Add a signing step through GitHub secrets and a
managed signing provider before the checksum and attestation steps when a trusted signing
identity is available. Sign both `ControllerBattery.exe` and the final installer.

## Local checksums

After all artifacts are present, regenerate checksums with:

```powershell
./scripts/New-ReleaseChecksums.ps1
```

Users can verify an asset with `Get-FileHash -Algorithm SHA256` and compare the result with
`SHA256SUMS.txt`.
