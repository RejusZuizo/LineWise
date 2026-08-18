# Linewise

[![CI](https://github.com/socom1/LineWise/actions/workflows/ci.yml/badge.svg)](https://github.com/socom1/LineWise/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Licence](https://img.shields.io/badge/licence-all%20rights%20reserved-lightgrey)

Production line rostering for food manufacturing. Windows desktop, .NET 8, Avalonia.

![The overview, leading with the lines that cannot run](docs/images/roster.png)

## Why

A production manager builds the weekly line roster by hand from a colour coded spreadsheet.
They decide who works which line, who leads it, and they honour a set of rules that exist
only in their head. The result is printed and stuck on the factory wall.

That costs hours a week, the rules are lost if that person leaves, and one sick call on a
Monday means doing it again.

Linewise reads the same spreadsheet, produces the roster, and explains every placement. The
manager stays in charge of the result.

## Status

Phase 6 of eight. Not released, and not yet used by anybody.

1.0.0 is reserved for the first build that has produced a roster somebody actually worked
to. See [Known limitations](#known-limitations).

## Build and test

```
dotnet test
dotnet run --project src/Linewise.Desktop
```

`dotnet test --filter Category!=Integration` for the fast loop.

## How it decides

Ten rules in a fixed order: locked placements, availability, mandatory preferences, leader
selection, overtime routed to the busy lines, ranked preferences across the whole workforce
at once, the fairness tie break, the absolute skill and blocked filter, backfill, then
warnings. Greedy, not optimal — every placement can be justified in one sentence, which is
worth more to a manager than an answer nobody can audit. [ADR 0001](docs/adr/0001-greedy-assignment-over-a-constraint-solver.md).

The engine is pure. No I/O, no logging, no static state, and the same inputs always produce
the same week.

## Design

The reasoning lives in [docs/adr](docs/adr/) — seventeen numbered decisions, including the
ones that were later reversed and why. Start with the
[technical design](docs/technical-design.md).

Security and data protection are living documents rather than sections of a plan:
[threat model](docs/threat-model.md), [data protection](docs/data-protection.md).

The single most important minimisation decision: no reason for an absence is stored beyond
the status. "Holiday" is recorded, "hospital appointment" is not.

## Known limitations

- **Never used in a factory.** Every rule the engine encodes is an assumption that happens
  to compile.
- **The historic replay has not been done.** Replaying a real month against the engine and
  comparing it to what the manager actually produced is the highest value testing left, and
  it needs data nobody has sent yet.
- **No installer.** Runs from `dotnet run`. Packaging is phase 8.
- **Windows is the target; Linux is where it is developed.** The whole suite passes on both,
  the application runs on both, and the database key is weaker on Linux by design.
  [ADR 0011](docs/adr/0011-development-happens-on-linux-and-shipping-does-not.md).
- **Import detection was built against invented spreadsheets.** It finds the shapes somebody
  thought to invent. A sheet nobody imagined still needs a template by hand.
  [ADR 0017](docs/adr/0017-the-importer-reads-the-sheet-before-it-reads-the-template.md).
- **No undo.** Every editing action is one way.
- **Backup runs on startup and cannot be restored from the application.** The service and the
  restore routine are tested; neither has a button.

## Licence

All rights reserved. See [LICENSE](LICENSE). Not open source, and not accepting
contributions.

No real employee data appears anywhere in this repository, including screenshots and test
fixtures. The client is not named.
