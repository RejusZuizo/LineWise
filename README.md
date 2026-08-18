# Linewise

[![CI](https://github.com/socom1/LineWise/actions/workflows/ci.yml/badge.svg)](https://github.com/socom1/LineWise/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Licence](https://img.shields.io/badge/licence-all%20rights%20reserved-lightgrey)

Weekly rostering for food production lines. Windows desktop app, .NET 8 and Avalonia.

![The overview screen](docs/images/roster.png)

## Why

I work in food manufacturing. The weekly line roster where I am gets built by hand off a
colour-coded spreadsheet: who's on which line, who's leading it, and a pile of rules that
only exist in one person's head. It takes hours. When someone calls in sick on a Monday
morning, most of it gets done again.

Linewise reads that same spreadsheet and builds the roster from it. It records why it put
each person where it did, so you can argue with the result instead of guessing at it.

A few things are in here only because I've seen the floor. Agency staff turn up unannounced,
so unknown names have to be addable during the import rather than blocking it. The source
sheet switched from coloured cells to written marks partway through the project, so the
importer reads both. Most factory printers are black and white. And the sheet gets read from
about four feet away by someone on their way past.

## Status

Phase 6 of 8. Not released, and nobody has used it yet.

1.0.0 is reserved for a build that has produced a roster someone actually worked to.

## Build and test

```
dotnet test
dotnet run --project src/Linewise.Desktop
```

Use `dotnet test --filter Category!=Integration` for the fast loop.

## How the engine works

Ten rules in a fixed order: locked placements, availability, mandatory preferences, leader
selection, overtime routed to whichever lines are busy, ranked preferences across everyone at
once, the fairness tie break, the skill and blocked filter, backfill, then warnings.

Greedy, not optimal. A constraint solver would produce better rosters and no explanation, and
"why is she on Ovens today" is the first thing anyone asks. Every assignment stores which rule
placed it and at what preference rank. [ADR 0001](docs/adr/0001-greedy-assignment-over-a-constraint-solver.md).

The engine does no I/O and holds no state. Same inputs, same roster, every time.

## Worth a look

Most of the code is unremarkable. The decisions are written down, and those are more
interesting:

- [ADR 0009](docs/adr/0009-overtime-follows-demand-rather-than-preference.md). Overtime
  routing sat at the wrong step. It would have been correct on paper and almost never fired.
- [ADR 0014](docs/adr/0014-availability-records-say-who-set-them.md). Marking someone absent
  turns out to touch two records, and re-importing the sheet would have undone it silently.
- [ADR 0010](docs/adr/0010-the-printed-sheet-carries-no-meaning-in-colour.md). Nothing on the
  printed roster is told apart by colour on its own.
- [ADR 0013](docs/adr/0013-the-application-ships-its-own-typeface.md). Reverses a rule from
  the design document, and says which part of it still holds.
- [data-protection.md](docs/data-protection.md). No reason for an absence is stored beyond the
  status. "Holiday" is kept, "hospital appointment" isn't.

Four bugs got past a green test suite and only showed up when I ran the app: an uninitialised
database, a week with no shifts, a main window where every command silently did nothing, and
a screen that couldn't be saved twice. Two of them have tests now. The commit messages say
what was rejected and why.

## Design

Seventeen ADRs in [docs/adr](docs/adr/), including the ones that were later reversed.
[architecture.md](docs/architecture.md) covers the layering and why `Linewise.Domain` has no
NuGet packages at all. The [technical design](docs/technical-design.md) is the original plan.
[threat-model.md](docs/threat-model.md) and [data-protection.md](docs/data-protection.md) get
updated as things change rather than sitting inside the plan.

## Known limitations

- Never used in a factory. Every rule in the engine is an assumption that happens to compile.
- The historic replay hasn't been done. Running a real month through the engine and comparing
  it against what the manager actually produced is the most useful testing left, and it needs
  data I don't have yet.
- No installer. It runs from `dotnet run`. Packaging is phase 8.
- Ships to Windows, developed on Linux. Both build and run, but the database key is weaker on
  Linux on purpose.
  [ADR 0011](docs/adr/0011-development-happens-on-linux-and-shipping-does-not.md).
- The importer's layout detection was built against spreadsheets I made up, so it finds the
  shapes I thought of. Anything stranger still needs a template set up by hand.
  [ADR 0017](docs/adr/0017-the-importer-reads-the-sheet-before-it-reads-the-template.md).
- No undo yet.
- Backups run at startup but there's no way to restore one from inside the app. The restore
  routine exists and is tested. It just has no button.

## Licence

All rights reserved, see [LICENSE](LICENSE). Not open source and not taking contributions.

There's no real employee data anywhere in this repository, including the screenshot and the
test fixtures, and the site isn't named.

Rejus Zuzevicius
