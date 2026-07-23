# Windows test checklist

## Build gate

Run from PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build-and-test.ps1
```

Expected result: the solution builds with no warnings or errors and the console
reports `12/12 tests passed`.

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

## Discovery evidence gate

1. Add notes while saving the Credits result as a discovery.
2. Select the saved discovery and the corresponding scan result.
3. Choose **Confirm result**. The app should report that the saved address resolved
   automatically and show two confirmations in one attachment.
4. The reliability should change to **Session stable**.
5. Save, close, reopen the project, and confirm that the name, notes, address
   description, validation count, and reliability are preserved.
6. While the saved Credits discovery and confirmed scan result are selected,
   choose **Find pointer paths**. At least one bounded path must be saved.
7. Save the project and close the test game.

## Pointer restart gate

1. Reopen the test game. Its values and intermediate pointer node should receive
   new heap addresses.
2. Reattach Trainer Studio to the new test-game process.
3. Open the saved project if it is not already open and select Credits.
4. Choose **Resolve saved path** without running a value scan.
5. The results table must show the current Credits value at the newly resolved
   address.
6. Write `9100`; the test game must immediately display `9100`.
7. Choose **Confirm result** only after visually confirming the value.
8. The discovery should become **Restart stable** once it has automatic
   confirmations in two distinct attachment sessions.
9. Save, reopen the project, and verify the pointer-path count and reliability are
   preserved.

## Comparison gate

Repeat with Health (`100`) and use the damage button:

- `Decreased` retains the health candidate after damage.
- `Increased` does not retain it after damage.
- `Changed` retains it after damage.
- `Unchanged` retains it when no health-changing action occurred.

## Float gate

Scan Jump Height as `Float32`, starting at `12.25`. Choose **Increase jump by
1.25**, then narrow using exact `13.5` or the `Increased` comparison.

## Pointer scan limits

The first pointer milestone intentionally searches aligned 64-bit pointers with
positive offsets only. It does not yet support negative offsets, pointer maps
captured to disk, multiple module roots, custom depth/offset controls, or
instruction-derived roots.
