# Changelog

## 0.3.0-source — 2026-07-23

- Added a bounded, cancellable x64 pointer-path scanner with three-level,
  `0x1000`-offset defaults and explicit safety caps.
- Added main-module-relative pointer roots and experimental page-aligned
  absolute roots.
- Added deterministic multi-level path resolution and overflow checks.
- Upgraded the project document to format 3 with persisted pointer paths and
  per-path resolution evidence.
- Added **Find pointer paths** and **Resolve saved path** workflows.
- Added a controlled two-level pointer fixture for the Credits value.
- Expanded the dependency-free core test runner from 10 to 12 tests.
- Added a live Windows integration gate that discovers, resolves, and writes
  through a real two-level path in an attached process.

## 0.2.3-source — 2026-07-23

- Made value-type and comparison selections readable by explicitly styling the
  text WPF generates inside combo boxes.
- Added distinct readable colors for normal, focused, hovered, selected, and
  disabled combo-box states.

## 0.2.2-source — 2026-07-23

- Corrected the progress-bar binding so the UI only reads the view model's
  privately-set scan progress instead of trying to write back to it.
- Strengthened the Windows launch test to require successful workspace
  initialization in the diagnostic log, preventing an initialization-error
  dialog from being mistaken for a successful launch.

## 0.2.1-source — 2026-07-22

- Added startup and unhandled-exception diagnostics under the user's local app-data folder.
- Replaced silent pre-window failures with an error message containing the diagnostic-log path.
- Deferred initial process discovery until after the main window is visible.
- Moved initial process discovery off the UI thread.
- Added a Windows CI smoke test that launches both published executables and verifies that
  neither exits during startup.

## 0.2.0-source — 2026-07-21

- Added stable project and discovery IDs with migration from the 0.1 project format.
- Added discovery notes, a saved-discovery browser, and editable discovery details.
- Added absolute and main-module-relative address records.
- Added per-attachment validation history tied to executable identity.
- Added evidence-based experimental, session-stable, restart-stable, and
  update-stable reliability evaluation.
- Added honest manual-rebind handling when a saved address moves.
- Replaced direct project writes with temporary-file replacement to reduce the
  chance of corrupting an existing project during a failed save.
- Expanded the dependency-free core test runner from 5 to 10 tests.
- Added a self-contained Windows x64 test bundle to successful CI runs.

## 0.1.0-source — 2026-07-21

- Added the Windows 11 x64 WPF application shell.
- Added x64 process discovery and attachment.
- Added exact Int32, Float32, and Float64 first scans.
- Added exact, changed, unchanged, increased, and decreased comparative scans.
- Added direct value editing and JSON project persistence.
- Added a controlled x64 test game with stable native gameplay values.
- Added a dependency-free core test runner and Windows CI workflow.

This is a source milestone. It has not yet passed the Windows build and hands-on
memory scan gates described in `docs/TESTING.md`.
