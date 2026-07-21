# Architecture

Trainer Studio separates policy-free scanning logic from Windows process access
and presentation.

## Projects

- `TrainerStudio.Core`: value codecs, comparisons, scan candidates, sessions,
  and saved project documents. This project has no UI or Win32 dependency.
- `TrainerStudio.Windows`: process discovery, x64 validation, memory-region
  enumeration, reads, writes, and the comparative scan coordinator.
- `TrainerStudio.App`: WPF shell and application state.
- `TrainerStudio.TestGame`: a controlled process with stable native allocations
  representing common gameplay values.

## Scan lifecycle

1. Open a process with query, read, write, and operation rights.
2. Reject non-x64 targets because this milestone is x64-only.
3. Enumerate committed, readable memory regions with `VirtualQueryEx`.
4. Read regions in bounded chunks and locate byte representations of the
   requested value.
5. Store addresses and their last values in a `ScanSession`.
6. On the next scan, read only existing candidates and apply the selected
   comparison.

Results are capped to prevent an accidental initial scan for a very common value
from exhausting desktop memory. Failed or partially unreadable pages are skipped.

## Deliberate milestone boundaries

Instruction tracing requires debug-event handling or a dedicated instrumentation
backend. Pointer scanning requires an indexed address graph. Neither belongs in
the basic scanner and neither is faked in the current interface.
