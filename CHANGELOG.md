# Changelog

Notable changes to Linewise. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions below 1.0.0 are phase completions rather than releases. 1.0.0 is
reserved for the first build that has produced a roster somebody actually worked
to.

## [Unreleased]

### Added

- `.editorconfig` pinning the style the codebase already followed, with a
  `dotnet format` check in CI.
- Vulnerability scan failing the build on a known advisory in any dependency,
  including transitive ones.
- Code coverage measured on every run and reported in the job summary. Baseline
  85.5% line, 88.8% branch.
- Print previews and coverage reports published as build artifacts, including
  from failed runs.
- `global.json` pinning the SDK feature band, so a machine with a newer SDK
  alongside does not silently build the product with it.
- `LICENSE`, `SECURITY.md`, `docs/threat-model.md`, `docs/data-protection.md`,
  and a pull request template.

### Fixed

- Line endings are now LF in the working tree on every platform. A Windows
  checkout was converting to CRLF and failing the formatting check that passed on
  Linux.
- File encoding normalised to UTF-8 without BOM, three files.

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

[Unreleased]: https://github.com/socom1/LineWise/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/socom1/LineWise/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/socom1/LineWise/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/socom1/LineWise/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/socom1/LineWise/releases/tag/v0.1.0
