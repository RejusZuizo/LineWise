# 6. The infrastructure layer targets Windows

Date: 2026-08-03

Status: Accepted

## Context

The database is encrypted at rest, and the key has to live somewhere that is not a
configuration file. DPAPI is the right answer on a single unattended office machine: the
operating system scopes the secret to this Windows account on this machine, and nothing
has to be typed in at startup. DPAPI is also a Windows API. With warnings as errors, the
platform compatibility analyser turns every call to it into a build failure in a project
that claims to target plain `net8.0`. The options were to suppress the analyser, to
annotate a chain of types as Windows-supported, or to say what is already true.

## Decision

`Linewise.Infrastructure` targets `net8.0-windows`, and `Linewise.Tests` follows it because
it references Infrastructure. `Linewise.Domain` and `Linewise.Application` stay on plain
`net8.0` and gain nothing platform specific, so the engine and its rules remain portable
and testable anywhere. Suppressing the analyser was rejected: it would have hidden a real
constraint rather than recording it.

## Consequences

The build tells the truth about where this product runs, and no suppression has to be
explained to a reviewer. The layering also stays honest: the platform dependency stops at
the boundary where the platform is actually used, and the engine can still be lifted into
any host. The sharp edge is inherited from DPAPI rather than from this decision, but it is
worth writing down here too. The key is scoped to one Windows profile on one machine, and
backups are encrypted with that same key, so losing the profile loses the database and the
backups together. That is why the restore procedure is documented and covered by tests
rather than assumed to work.

## Addendum, 5 August 2026

Extended by ADR 0011, not reversed.

One thing this record left implicit, and which was later read the wrong way round: the
target framework here is a statement to the compiler about what may be called, not a claim
that the assembly cannot execute elsewhere. `net8.0-windows` without `UseWPF` or
`UseWindowsForms` builds and runs anywhere `net8.0` does. The Windows dependency is DPAPI
itself, and it is the only one.

The practical consequence, which took a wrong assumption to discover: the whole solution
builds and the entire test suite passes on Linux, unchanged. ADR 0011 covers what that
means for running the application there.
