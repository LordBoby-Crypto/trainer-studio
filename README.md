# Trainer Studio

Trainer Studio is a Windows 11 x64 desktop application for creating personal,
single-player game trainers for games you own and are authorized to modify.

The current source milestones focus on the scanning and evidence foundation:

- enumerate and attach to x64 processes;
- scan readable process memory for exact `Int32`, `Float32`, and `Float64` values;
- narrow results by exact, changed, unchanged, increased, or decreased state;
- edit a confirmed value;
- save labeled discoveries and notes in a portable, migration-aware project file;
- record absolute or main-module-relative addresses;
- find bounded 64-bit pointer paths leading to a confirmed address;
- save and resolve pointer paths without repeating a value scan;
- confirm discoveries across attachments and calculate reliability from evidence;
- exercise the workflow against an included, harmless x64 test game.

Trainer Studio does not currently trace instructions, generate injections,
repair signatures, or export standalone trainers. Pointer-path scanning is an
early bounded implementation rather than a universal pointer-map engine.

## Requirements

- Windows 11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 is optional; the `dotnet` CLI is sufficient.

## Build and run

```powershell
dotnet restore .\TrainerStudio.sln
dotnet build .\TrainerStudio.sln -c Release -p:Platform=x64
dotnet run --project .\src\TrainerStudio.TestGame\TrainerStudio.TestGame.csproj
dotnet run --project .\src\TrainerStudio.App\TrainerStudio.App.csproj
```

Run the tests with:

```powershell
dotnet run --project .\tests\TrainerStudio.Core.Tests\TrainerStudio.Core.Tests.csproj -c Release -p:Platform=x64
```

Every successful GitHub Actions run also produces a self-contained
`TrainerStudio-windows-x64` test bundle. It contains Trainer Studio, the controlled
test game, and the testing checklist; the bundle does not require the .NET SDK.
The workflow launches both published applications and verifies that they remain
running before it uploads the bundle.

If Trainer Studio encounters a startup error, it displays the failure and writes
a diagnostic file to `%LOCALAPPDATA%\Trainer Studio\Logs\startup.log`. Startup
logging is best-effort and falls back to `%TEMP%\TrainerStudio-startup.log` if the
local app-data directory is unavailable.

## First real test

1. Launch `Trainer Studio Test Game`.
2. Note the displayed Credits value (starts at `2500`).
3. Launch Trainer Studio and choose **Refresh processes**.
4. Select `TrainerStudio.TestGame`, then choose **Attach**.
5. Select `Int32`, enter `2500`, and choose **First scan**.
6. In the test game, choose **Earn 125 credits**.
7. Enter `2625`, select `Exact`, and choose **Next scan**.
8. Repeat once if multiple candidates remain.
9. Select a result, enter a new value, and choose **Write value**.
10. Save it as a discovery, select the saved discovery, and choose **Confirm result**
    while the corresponding scan result is selected.
11. Choose **Find pointer paths** while the saved discovery and confirmed result
    are selected.
12. Save the project, restart the test game, reattach, and choose
    **Resolve saved path**. Credits should be recovered without a new value scan.
13. Confirm the displayed value before choosing **Confirm result**. Two
    automatically resolved attachment sessions are required for **Restart stable**.

The default pointer search follows aligned 64-bit pointers for up to three levels
with positive offsets no larger than `0x1000`. Main-module roots are preferred.
Page-aligned absolute roots are retained as experimental candidates and must
survive a real restart test before they contribute reliability evidence.

Keep this project limited to offline, authorized targets. Do not use it to evade
anti-cheat, tamper with protected multiplayer software, or modify software you do
not have permission to analyze.

## Repository layout

```text
src/TrainerStudio.Core       Shared scan and project models
src/TrainerStudio.Windows    Windows process-memory implementation
src/TrainerStudio.App        WPF desktop application
src/TrainerStudio.TestGame   Controlled x64 scan target
tests/TrainerStudio.Core.Tests Dependency-free test runner
docs                         Architecture and roadmap
```

## License

Copyright retained by the repository owner. No redistribution license has been
selected yet.
