# Architecture

Trainer Studio separates policy-free scanning logic from Windows process access
and presentation.

## Projects

- `TrainerStudio.Core`: value codecs, comparisons, scan candidates, pointer-path
  resolution, sessions, and saved project documents. This project has no UI or
  Win32 dependency.
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

## Pointer-path lifecycle

1. Begin with a user-confirmed value address.
2. Search readable memory for aligned 64-bit values that point at or within the
   configured positive offset of that address.
3. Treat each pointer storage address as the target of the next backward level.
4. Repeat within bounded depth and frontier limits.
5. Prefer roots stored inside the target's main module and express them as module
   offsets. Retain only page-aligned absolute roots as experimental alternatives.
6. Persist the full root and offset sequence with the discovery.
7. Resolve by dereferencing the root and applying each offset in order.

The default search is capped at three levels, `0x1000` maximum offset, 50,000
links per level, 25,000 frontier nodes, and 200 saved paths. Reaching a limit is
reported as truncation rather than silently claiming the search was exhaustive.

## Durable discoveries

Project format 3 assigns stable IDs to projects, discoveries, and pointer paths. Each discovery
stores its value type, last observation, notes, and either an absolute address or
an offset from the target's main module, plus its bounded pointer-path candidates.
Confirmation records include a unique attachment-session ID and a lightweight
executable identity.

Reliability is derived from confirmation evidence. Repeated confirmations in one
attachment can become session-stable. Automatic resolution across distinct
attachments is required for restart-stable. Automatic resolution across at least
three attachments and two executable identities is required for update-stable.
A manual rebind remains recorded evidence, but it cannot prove stable resolution.

## Deliberate milestone boundaries

Instruction tracing requires debug-event handling or a dedicated instrumentation
backend. It remains outside this milestone and is not represented as complete.
