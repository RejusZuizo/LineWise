# Changelog

Notable changes to Linewise. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions below 1.0.0 are phase completions rather than releases. 1.0.0 is
reserved for the first build that has produced a roster somebody actually worked
to.

## [Unreleased]

Nothing yet.

## [0.5.0] - 2026-08-16

Desktop application. Phase 5.

### Added

- Avalonia desktop shell with dependency injection, Serilog to the per-user
  application data folder with a policy that keeps employee names out of log
  files, and handlers covering the interface thread, unobserved tasks and
  everything else. A stack trace never reaches the operator.
- Week grid: production lines down the side, days across the top, people in the
  cells. Leaders and operating assistants each marked by a colour, a word and a
  weight, never by colour alone.
- Warnings panel, grouped by kind and day. A week where thirty people are spare
  raises a hundred and sixty warnings; the panel shows fourteen rows and says how
  many each stands for.
- Availability import: choose a sheet, review what it says, commit. Parsing
  writes nothing, and names nobody recognises can be added as temporary staff
  without leaving the screen.
- Production line setup, with headcount and operating assistants per line.
- Generate and print from the application, with the roster stored as a draft.
- Operating assistant, a third role chosen the way a line leader is.
- Light and dark palettes, switchable, built on named tokens that carry no
  meaning of their own.
- Inter, embedded rather than fetched. ADR 0013.
- Navigation sidebar, replacing the toolbar. Both it and the warnings panel
  collapse.

### Fixed

- The application never initialised the database, so the first query failed on a
  fresh install. Every test called the initialiser itself, which is why the suite
  was green while the product was broken.
- A week with no shifts produced no days and an unexplained empty grid. Generation
  now defaults a day shift. ADR 0012.
- Names in the grid were truncated rather than wrapped.

### Notes

Found by running the application rather than by testing it: the uninitialised
database, the missing shifts, and a CRLF checkout that failed CI on Windows while
passing on Linux. None of the three was visible to a green test suite, which is
the argument for the done-when of every phase being something a person looks at.

## [0.4.0] - 2026-08-03

Printed output. Phase 4.

### Added

- Three PDF layouts through QuestPDF: the full wall sheet, one page per line for
  posting at the line itself, and an amendment slip showing only what changed
  since the last print.
- `RosterDiff`, comparing two rosters. Pure, and in the application layer,
  because what changed is worth answering on screen as well as on paper.
- `ExportSanitiser`, defusing values a spreadsheet would execute. Deliberately
  not applied to PDF output.
- Print settings persisted to the database rather than a configuration file, so
  they travel with a backup.

### Notes

Nothing in the printed output is distinguished by colour alone. A leader is
marked three ways at once: a filled block, bold type, and the word. Factory
printers are frequently monochrome, sheets get photocopied, and the source
spreadsheet already uses red and green. See ADR 0010.

## [0.3.0] - 2026-08-03

Excel availability import. Phase 3.

### Added

- Import templates holding rules that map either a text mark or a fill colour to
  an availability status. Text rules are tried first, because text survives being
  copied between workbooks and colour frequently does not. See ADR 0008.
- Theme colour resolution with tint applied, since the standard Excel colour
  picker produces theme colours.
- Fuzzy employee name matching: whitespace, case, surname order, and common
  typos, against aliases as well as full names.
- Three step pipeline — parse, review, commit — with no database write before
  explicit confirmation. Raw file bytes stored with the committed import so it
  can be re-run after a mapping fix.
- Archive safety limits on the `.xlsx` package: uncompressed size and entry
  count caps, structural validation, no formula evaluation.
- Overtime routed to lines whose demand is raised for that day, ahead of ranked
  preferences rather than at backfill. See ADR 0009.
- `LineDemand`, letting a line's headcount be raised for a single day.
- `Holiday` as a status distinct from a blank cell.

## [0.2.0] - 2026-08-03

Persistence. Phase 2.

### Added

- EF Core over SQLite, encrypted at rest with SQLCipher. The key derives from a
  machine and user scoped secret in Windows DPAPI and is never written to
  configuration.
- Hash chained, append only audit log. Each entry carries the SHA-256 hash of the
  previous one, and a verification routine reports the first broken link.
- Backup service using the SQLite backup API rather than a file copy, with ten
  rolling encrypted copies and a tested restore path.
- Repository interfaces in the application layer, implementations in
  infrastructure. The engine still knows nothing about EF Core.
- `RosterVersion`, draft or published, incrementing on publish.

### Notes

`Linewise.Infrastructure` targets `net8.0-windows`, because DPAPI is a Windows
API and saying so is more honest than suppressing the platform analyser. See
ADR 0006.

## [0.1.0] - 2026-07-31

Domain model and assignment engine. Phase 1.

### Added

- Domain entities with zero package dependencies: employees, production lines,
  shifts, skills, availability, line preferences, leader eligibility,
  assignments.
- Assignment engine generating a full week per call, because fairness is not
  meaningful within a single day. Greedy rather than a constraint solver; see
  ADR 0001.
- Hard constraints that are never violated: locked assignments, availability,
  blocked lines, required skills.
- Warnings for every understaffed line, every line without a leader, every
  available employee left unassigned, and every unsatisfiable configuration. The
  engine never throws for a business rule failure.
- An explanation on every assignment recording which rule placed it and at what
  preference rank.
- Rule validator reporting impossible configurations before generation is
  attempted.
- Injectable tie break strategy, defaulting to fairness across a rolling history
  window.
- Console harness, so the engine can be watched working before there is a window.
  Deleted in phase 5.

[Unreleased]: https://github.com/socom1/LineWise/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/socom1/LineWise/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/socom1/LineWise/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/socom1/LineWise/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/socom1/LineWise/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/socom1/LineWise/releases/tag/v0.1.0
