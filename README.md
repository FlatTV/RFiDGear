![](docs/assets/img/logoRG.png) 

# RFiDGear - .net 8 Mifare encoding tool.

Support for batch processing.

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/ac98d255ca38466bb5803f9e2e4a11ae)](https://www.codacy.com/app/c3rebro/rfidgear)
![](https://messgeraetetechnik-hansen.de/rfidgear/mainWnd.jpg) 

### [Info](https://c3rebro.github.io/RFiDGear/) | [Download](https://github.com/c3rebro/RFiDGear/releases) | [Report Bugs](https://github.com/c3rebro/RFiDGear/issues)

Requirements:

* minimum Microsoft Windows 7 32/64bit (or later)
* Elatec Reader TWN4 Multi or
* Elatec Reader TWN4 Legic 45 or
* PC/SC compatible Reader

In case of PCSC Provider: a PCSC compatibile Reader:
(Examples - others are reported to work)
* Omnikey 5321
* Omnikey 5422
* Sciel SCL3711
* ACR 122U
* Elatec Reader TWN4 MultiTech using CCID Firmware

## System requirements

The original German help includes a concise hardware and OS checklist:

* A PC that meets at least the Windows 7 (32/64-bit) minimum specifications
* Microsoft Windows 7 or newer
* At least one PC/SC-compatible encoder—tested examples include HID/Omnikey 5x21 devices, Sciel SCL3711, and ACR 122U
* Optional: the LibLogicalAccess provider stack supplied with the installer

## Task model and supported operations

RFiDGear organizes programming steps as sequential “tasks.” Each task has an index that drives execution order, a user-defined label, and an internal error code captured during execution. Tasks can also depend on the outcome of an earlier task (index + expected error code) before they run. They can be executed individually, as a batch, or automatically when a new chip is detected.

Task types include:

* Device-independent helpers
  * Populate a PDF form with static text or variable placeholders
  * Simple logic building blocks for if/then checks
* Chip-generic checks
  * Validate a tag’s UID
  * Verify the detected chip type
* MIFARE Classic
  * Read data blocks
  * Write data blocks
  * Inspect whether a sector is still unused
* MIFARE DESFire
  * Check for application existence
  * PICC-level operations: change the master key or format the card
  * Application-level operations: create apps, change keys, delete apps, authenticate
  * File-level operations: create, write, read, and delete files

## Summary of changes in this FlatTV fork (bam5br-Edition)

This section summarizes the main differences introduced in the FlatTV "bam5br-Edition" fork compared to the upstream c3rebro/RFiDGear master branch. It focuses on notable new features, robustness and bug fixes, user-interface improvements, and packaging changes.

1) New features and UX improvements
- [MIFARE DESFire] Batch processing enhancements: line-by-line batch input support, an optional per-run confirmation dialog that lets you scroll forward/back through input records before writing, and the ability to consume the next input line automatically for repeated/automated runs.
- [MIFARE DESFire] A new "Read Files (with Key)" dialog and view-model that lets a user authenticate to a DESFire application with a supplied key to read its file list and file settings when free listing is not permitted.
- Project/unsaved-changes tracking: the main window title now shows the loaded project and a trailing "*" when there are unsaved changes; there is also a prompt on close to save/discard/cancel.
- Small UX improvements: auto-scrolling TreeView behavior for the most recently added items; richer custom dialog navigation (Previous/Next) for multi-step confirmations.

2) MIFARE DESFire and reader-provider robustness & bug fixes
- Key handling fixes: key-length and formatting are now type-aware (DES / 3K3DES / AES) so AES and 3K3DES keys are no longer truncated or rejected by helpers that previously assumed a fixed hex length.
- Improved authentication and session handling: reader disconnect/reconnect is used in places where DESFire EV2/EV3 cards require a fresh RF session (to avoid "status does not allow the requested command" during re-authentication). Several methods now attempt a conservative retry strategy and verify outcomes when exceptions are thrown.
- Better error diagnostics: providers now capture native exception text (LastNativeErrorMessage) and surface more informative error classification (AuthFailure, TransportError, ProtocolConstraint) instead of generic failures.
- More robust DeleteApplication / ChangeKey / GetFileList / GetFileSettings implementations with fallbacks and verification steps to reduce silent failures.

3) Task execution, validation and automation
- Many task view-models and execution paths now perform stricter validation (e.g., AppID parsing) and use type-aware key validation before attempting operations.
- Task execution improvements for automation: confirmation dialogs used during unattended runs are wired to the task execution service so they can be shown properly (or deferred) during automated loops; the task watchdog can be suspended while waiting for the user's confirmation to avoid spurious timeouts.
- Write/read flows extended with options to append vs overwrite read-output files and to consume input data lines incrementally during repeated executions.

4) Installer, build and resource changes
- Installer bundle updated to include the Windows Desktop Runtime (windowsdesktop-runtime) for .NET 8 WPF applications instead of the aspnetcore runtime.

5) Misc / internal improvements
- Many small fixes in Views and ViewModels (XAML tweaks, safer property handling, stricter parsing) and new utility classes added (e.g., TreeView auto-scroll behavior).
- AccessRights parsing and file-size decoding fixes to correctly present DESFire file metadata.

## Main window quick start

The main window exposes keyboard-driven file and options commands described in the localized help:

* **Open project** (`Alt` + `D`, `P`): restore a previously saved project
* **Save** (`Alt` + `D`, `S`): persist the current task definitions as the default database
* **Save as…** (`Alt` + `D`, `U`): store multiple projects under distinct paths
* **Language** (`Alt` + `O`, `L`): change the UI language (requires restart)
* **Load last project on startup** (`Alt` + `O`, `P`): automatically reopen the default project saved via the file menu

## Codebase overview

RFiDGear is a WPF desktop app structured around the MVVM pattern. Application resources in `App.xaml` register view-model-to-view mappings for dialogs and chip setup screens so XAML can instantiate the correct views dynamically, while Serilog logging is configured in `App.xaml.cs` to capture unhandled exceptions to rolling log files.

Managed Extensibility Framework (MEF) composition is used to discover view models. The `ViewModelLocator` exposes exports tagged as `"ViewModel"` as dynamic properties for XAML bindings, caching the composed instances to avoid repeated resolution.

Startup flows are coordinated by `AppStartupInitializer`, which establishes a single-instance mutex, prepares the Windows event log source, and captures command-line arguments in an `AppStartupContext` that seeds initialization in the main window view model.

`MainWindowViewModel` orchestrates settings bootstrapping, update notifications, context menu construction, reader monitoring, and project initialization. During `InitializeAsync`, it loads persisted defaults, configures timers for chip reads and task timeouts, and wires task execution callbacks that keep UI bindings in sync with reader state and task progress.

Task execution flows live in `Services/TaskExecution/TaskExecutionService.cs`. Adapters around `DispatcherTimer` manage device discovery, chip hydration, selection synchronization, and the task loop, while structured Serilog logging (via `NullTaskExecutionLogger`) captures JSON details for each stage.

### Configuring runtime defaults

RFiDGear writes a `runtime-defaults.json` file to `%LocalAppData%\RFiDGear` the first time it starts. You can edit this file with any text editor to control the initial values used for reader selection, language, auto-update behavior, and the default MIFARE keys that seed new `settings.xml` files—no code changes or recompilation required.

See `runtime-defaults.sample.json` for a complete set of defaults that mirrors the built-in configuration, including reader options, auto-update flags, COM port settings, default MIFARE keys, and the pre-populated quick-check key list. Copy this file to `%LocalAppData%\RFiDGear` and rename it to `runtime-defaults.json` to start from the sample.

## Known Limitations

### PC/SC provider (LibLogicalAccess) is not available in Remote Desktop sessions

The Windows Smart Card service (`SCardSvr`) is disrupted when you connect to a machine via Remote Desktop (RDP). Windows RDP injects virtual smart card readers into the remote machine's PC/SC stack, which causes `SCardSvr` to stop and restart. Any call to `SCardEstablishContext` — which LibLogicalAccess performs on startup — will fail with `SCARD_E_NO_SERVICE (0x8010001D)` for the duration of the RDP session.

**Effect in RFiDGear:** If the PC/SC provider is selected and the app is running inside an RDP session, it will automatically fall back to *No Reader* and display a warning in the reader settings and status bar. The restriction is lifted only by closing the RDP session.

**Elatec TWN4 readers are not affected** — they communicate over USB CDC (serial) and do not rely on the Smart Card service.

**Workaround (administrator-controlled):** To prevent RDP from redirecting smart cards to the remote machine, set the following registry value on the remote machine:

```
HKLM\SOFTWARE\Policies\Microsoft\Windows NT\Terminal Services
  fEnableSmartCard = 0  (DWORD)
```

This stops RDP from injecting virtual readers and allows `SCardSvr` to remain stable, restoring PC/SC functionality under RDP.

See [issue #203](https://github.com/c3rebro/RFiDGear/issues/203) for technical details and current status.
