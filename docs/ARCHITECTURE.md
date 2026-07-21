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

## Durable discoveries

Project format 2 assigns stable IDs to projects and discoveries. Each discovery
stores its value type, last observation, notes, and either an absolute address or
an offset from the target's main module. Confirmation records include a unique
attachment-session ID and a lightweight executable identity.

Reliability is derived from confirmation evidence. Repeated confirmations in one
attachment can become session-stable. Automatic resolution across distinct
attachments is required for restart-stable. Automatic resolution across at least
three attachments and two executable identities is required for update-stable.
A manual rebind remains recorded evidence, but it cannot prove stable resolution.

## Deliberate milestone boundaries

Instruction tracing requires debug-event handling or a dedicated instrumentation
backend. Pointer scanning requires an indexed address graph. Neither belongs in
the basic scanner and neither is faked in the current interface.
