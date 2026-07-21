# Windows test checklist

## Build gate

Run from PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build-and-test.ps1
```

Expected result: the solution builds with no warnings or errors and the console
reports `5/5 tests passed`.

## Controlled scan gate

1. Start the test game.
2. Start Trainer Studio.
3. Refresh processes, select `TrainerStudio.TestGame`, and attach.
4. With `Int32` selected, scan for the exact Credits value `2500`.
5. Earn 125 credits in the test game.
6. Set comparison to `Exact`, enter `2625`, and run Next scan.
7. Repeat the earn-and-next-scan sequence until one address remains.
8. Compare the remaining address with the native test-block address displayed in
   the test game. The Credits field begins 8 bytes after that base address.
9. Write `9000` from Trainer Studio. The test game must display `9000` without a
   restart.
10. Add the result as a discovery and save the project JSON.

## Comparison gate

Repeat with Health (`100`) and use the damage button:

- `Decreased` retains the health candidate after damage.
- `Increased` does not retain it after damage.
- `Changed` retains it after damage.
- `Unchanged` retains it when no health-changing action occurred.

## Float gate

Scan Jump Height as `Float32`, starting at `12.25`. Choose **Increase jump by
1.25**, then narrow using exact `13.5` or the `Increased` comparison.

## Restart limitation

Restarting the test game intentionally changes the native block address. A saved
raw address should be treated as invalid after restart. Pointer discovery is the
next milestone that will solve this; the current application must not present a
raw address as restart-stable.
