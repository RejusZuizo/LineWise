# Linewise: Technical Design and Delivery Plan

| | |
|---|---|
| **Document type** | Technical design and delivery plan |
| **Product** | Linewise, production line rostering for food manufacturing |
| **Status** | Draft, pre-implementation |
| **Version** | 1.0 |
| **Last updated** | 29 July 2026 |

## 1. Problem statement

A production manager builds the weekly line roster by hand from a colour coded Excel
sheet showing who is working which days. They decide who works each production line,
who leads each line, and they honour a set of rules that currently exist only in
their head. The result is printed and posted on the factory wall.

The manual process costs several hours a week, the rules are undocumented and lost if
that person leaves, and last minute absences force the whole thing to be redone.

Linewise automates the assignment while keeping the manager in control of the result.

## 2. Scope

### In scope

Import of the weekly availability sheet, automated assignment of workers and line
leaders, rule and preference management, manual override, absence handling, and
printed output suitable for wall posting.

### Out of scope

Payroll integration, holiday request workflows, worker self service, shift swapping,
time and attendance, mobile applications, and multi site deployment.

### Explicit non-goals

**Not a web application.** Native Windows desktop.

**Not multi user.** One installation on one machine, used by the production manager.
Supervisors and workers consume the printed output. This removes the need for
authentication, authorisation, and network data access entirely.

**Not a scheduling optimiser.** The engine produces a good assignment quickly and
explains itself. It does not attempt mathematical optimality.

## 3. Architecture

### 3.1 Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Platform | Windows desktop | Single operator, output is paper, no server to maintain |
| UI framework | Avalonia with FluentAvalonia | Familiar to the developer, native feel, matches the Office visual language the user already knows |
| Persistence | SQLite via EF Core | Zero installation, single file backup, well within capacity at roughly 400k rows over a decade |
| Excel | ClosedXML | MIT licensed. EPPlus is commercially licensed and unsuitable |
| PDF | QuestPDF | Identical output regardless of printer, previewable, emailable |
| Packaging | Velopack | Real installer plus auto update, MIT licensed |

### 3.2 Solution layout

```
Linewise.Domain          entities and business rules. ZERO NuGet dependencies.
Linewise.Application     engine, use cases, service interfaces. References Domain.
Linewise.Infrastructure  EF Core, ClosedXML, QuestPDF, file system, crypto.
Linewise.Desktop         Avalonia views and ViewModels.
Linewise.Tests           xUnit.
```

Dependencies point inwards. Nothing in Domain or Application may reference Avalonia,
EF Core, ClosedXML, QuestPDF, or the file system.

### 3.3 Capacity assumptions

Sizing target is 150 employees across 8 production lines and 2 shifts. Roughly 150
assignment rows per day, 39,000 per year, under 400,000 after a decade. Data volume
is not an architectural factor.

### 3.4 Deferred: multi machine access

Explicitly deferred, not designed for. If it is ever required, SQLite must not be
placed on a network share, which causes silent corruption. The path would be SQL
Server Express with Windows integrated authentication, which requires no stored
credentials. Keeping all data access behind repository interfaces and writing no
provider specific SQL keeps that door open at low cost.

## 4. Security architecture

The threat model is shaped by the deployment: a single unattended office PC in a
factory, holding the personal data of every employee on site.

### 4.1 Shape of the problem

The interesting surface is local. There is no login to bypass and no API to abuse.
What matters is the machine, the files the application writes, and the files it
reads from elsewhere.

Eleven threats are tracked, from physical access to an unattended machine through
to a development key provider reaching a release build. Four controls carry most
of the weight: encryption at rest keyed from DPAPI, a hash chained audit log,
treating the imported spreadsheet as a trust boundary, and a locked dependency
graph scanned on every build.

### 4.2 Where this is written down

The threat table, the controls in detail, and the state of each now live in
[threat-model.md](threat-model.md), which is a living document rather than a
section of a plan. The data protection position — what is held, what is
deliberately not held, retention, erasure, and the rule against real employee data
anywhere in the repository — lives in [data-protection.md](data-protection.md).

Both were extracted from this document on 5 August 2026. Keeping them here meant
security decisions ageing at the speed of a design document, which is to say not
at all.

## 5. Non-functional targets

State them so they can be tested.

| Target | Value |
|---|---|
| Employees supported | 150 |
| Production lines | 8 |
| Roster generation | under 2 seconds for a full week |
| Excel import, parse and review | under 10 seconds |
| Cold start to usable window | under 3 seconds |
| Data loss on crash | zero, draft autosaved continuously |
| Backup retention | 10 rolling copies, restore procedure tested |
| Unhandled exceptions surfaced to the user | zero |

## 6. Version control

The commit history is part of what a reviewer reads. Set this up before phase 1.

- **GitHub Flow.** `main` plus short lived branches, one per phase, named
  `feature/phase-3-excel-import`.
- **Pull requests, even solo.** Forces a self review of the diff, gives CI somewhere
  to run, and leaves a visible record of how the work was planned.
- **Squash merge**, so `main` reads as one clean commit per feature.
- **Branch protection on `main`:** CI must pass, no direct pushes.
- **Conventional Commits** (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`)
  so the changelog is generated rather than written.
- **Semantic versioning.** Tag releases `v1.2.0`, attach the installer to a GitHub
  Release.
- **GPG signed commits.**
- **.gitignore** covering .NET output folders, `*.db`, `*.db-shm`, `*.db-wal`, and
  any `.xlsx` outside a test fixtures folder.
- **.gitattributes** for consistent line endings.

## 7. Engineering standards

### Stack

.NET 8, C# 12, Avalonia with FluentAvalonia, CommunityToolkit.Mvvm, EF Core with SQLite
plus SQLCipher, ClosedXML (not EPPlus, which is commercially licensed), QuestPDF, Serilog,
xUnit, Velopack.

### Rules that apply to every phase

- Nullable reference types on, warnings as errors.
- Everything behind an interface registered in DI. No `new` on a service inside a
  ViewModel.
- All I/O async. The UI thread never blocks.
- All user facing strings in resource files from the outset, even though only English
  ships. Retrofitting localisation is expensive; the factory floor is multilingual.
- No real employee names anywhere, including tests and fixtures. Personal data under
  GDPR. Use obviously fictional names.
- Prefer clear code over clever code. This will be read by other engineers.
- Any decision this document did not settle is recorded as a numbered ADR in `docs/adr/`.

## 8. Delivery phases

Work through these in order. Each assumes the previous is complete and its tests pass.

### Phase 1: Domain model and assignment engine

Scope: `Linewise.Domain`, `Linewise.Application` and `Linewise.Tests` only.

#### Domain model

| Type | Fields |
|---|---|
| Employee | Id, FullName, Aliases (for matching imported names), IsActive, IsTemporary, Skills |
| ProductionLine | Id, Name, DisplayOrder, RequiredHeadcount, RequiredSkills, AccentColour |
| Skill | Id, Name. A hard eligibility filter |
| Shift | Id, Date, Name (Day, Night) |
| Availability | EmployeeId, Date, Status (Working, Off, Overtime, Holiday) |
| LineDemand | LineId, Date, RequiredHeadcount. Overrides the line's standard headcount for one day |
| LinePreference | EmployeeId, LineId, Rank (1 = first choice), Type (Mandatory, Preferred, Blocked) |
| LeaderEligibility | EmployeeId, LineId |
| Assignment | Date, ShiftId, LineId, EmployeeId, Role (Worker, LineLeader), IsLocked, Source (Auto, Manual) |
| RosterDay | Assignments plus warnings for one date and shift |
| RosterWeek | A set of roster days generated together |

Leadership is assigned per day rather than held as a fixed attribute, and a leader counts
toward line headcount like anybody else. Enums for all status types, never magic strings.

#### Assignment engine

Generates a full week per call, never a single day. Fairness is only meaningful across a
week; per day generation lets one person take the worst line five days running with every
individual day looking correct.

Rules apply in this exact order:

1. Locked assignments are placed first and never moved.
2. Exclude anyone whose status that day is not Working or Overtime.
3. Place mandatory preferences. A mandatory preference may only be broken when that
   employee's status is Overtime. If impossible, record a warning rather than throwing.
4. Assign one line leader per line from LeaderEligibility, preferring whoever has led
   least recently.
5. Place anyone whose status is Overtime onto a line running above its standard headcount.
   Overtime is worked because a line has more product to get out, so that is where the
   extra pair of hands belongs, and it has to happen before ranked preferences are filled
   rather than after. An overtime worker with a first choice elsewhere would otherwise be
   placed on it at step 6 and never be available to cover the busy line at all.
6. Fill remaining slots by preference rank across all employees at once: every rank 1
   preference, then every rank 2, and so on. Iterating employee by employee starves
   whoever sorts last.
7. Ties at the same rank go to whoever has received their preferred line least often
   across the rolling history window. This sits behind an injectable interface so it can
   be swapped for seniority.
8. Never place an employee on a line they are blocked from, or whose required skills they
   lack. Absolute, overriding everything above except locked assignments.
9. Fill remaining empty slots with unassigned eligible employees.
10. Warn for every line under RequiredHeadcount, every line with no leader, every available
    employee left unassigned, every line whose raised demand went uncovered, and every
    employee on overtime who ended up on a line that did not need one.

Headcount for a line on a given day is its LineDemand for that date when one exists, and
its standard RequiredHeadcount otherwise. Raising demand for a day is how the manager says
a line has more product to get out, and it is the same signal that tells the engine where
overtime should go. Overtime routing is a preference and not a restriction: somebody on
overtime who cannot go on a busy line, because they are blocked from it or lack a skill it
requires, falls through to the ordinary rules rather than being left standing, and the
mismatch is warned about instead.

The engine is pure: no I/O, no logging, no static state, deterministic given identical
inputs.

#### Explainability

Every assignment records why it was made: which rule placed it and at what preference
rank. The manager must be able to ask why someone is on a line and get an answer.

#### Warnings

Severity (Error, Notice), Code, human readable Message, optional LineId and EmployeeId.
The engine never throws for a business rule failure.

#### Rule validation

A separate validator checks a configuration for impossibility before generation is
attempted: two employees both mandatory on a single slot line, an employee blocked from
every line, a line whose required skills nobody holds, a line with no eligible leader.

#### Tests

xUnit covering at minimum: a mandatory preference is honoured; broken only on overtime; a
blocked line is never assigned even when short; a skill requirement is never violated;
rank 2 used when rank 1 is full; ties separated by the tie break; locked assignments
survive regeneration; an understaffed line warns rather than throws; identical inputs
produce identical output; fairness evens out across a simulated four week run; the
validator catches each impossibility case. A test data builder rather than repeated setup.

#### Repository setup

- .NET .gitignore plus `*.db`, `*.db-shm`, `*.db-wal`, and `*.xlsx` outside test fixtures.
- GitHub Actions running restore, build and test on every push and pull request. Now, not
  later, so every subsequent phase is protected.
- `Directory.Build.props` setting nullable, warnings as errors and language version once
  for all projects.
- `packages.lock.json` committed, CI restoring in locked mode.

#### Console harness

A throwaway `Linewise.ConsoleHarness` builds fake employees, lines and preferences, runs
the engine, and prints the roster and warnings as a text table. It exists so the engine
can be seen working before any UI, and is deleted in phase 5. Crude by design; no logic
lives in it.

**Done when:** tests green, `Linewise.Domain.csproj` contains no `PackageReference`, CI
passing on `main`, and the ordering of the rules can be recited unaided.

### Phase 2: Persistence

Scope: adds `Linewise.Infrastructure`.

- EF Core with SQLite, encrypted with SQLCipher. The key derives from a machine and user
  scoped secret held in Windows DPAPI and is never written to configuration.
- Database file in the per user application data folder, path from configuration, never
  beside the executable.
- One DbContext with explicit entity configuration classes, not attributes.
- All dates and times stored as UTC. Never perform date arithmetic in SQL.
- Avoid decimal, which SQLite stores as text and cannot sort or aggregate.
- Migrations, with an initial migration committed, applied automatically at startup.
- Repository interfaces in `Linewise.Application`, implemented in Infrastructure. The
  engine still knows nothing about EF Core.
- RosterVersion: Draft or Published, version number incrementing on each publish,
  published timestamp, and `Environment.UserName`. Publishing is an explicit action.
- AuditLog: what changed, when, by which Windows user, a free text reason, and the SHA-256
  hash of the previous entry, forming a tamper evident chain. No update or delete path is
  exposed. A verification routine walks the chain and reports the first broken link.
- Backup service: encrypted copy on startup, 10 rolling copies, plus a restore routine.
  Never `File.Copy` on a live database; use `VACUUM INTO` or the SQLite backup API.

No UI. Integration tests against a temporary file database prove: migrations apply cleanly
to an empty database; a roster round trips unchanged; the audit chain verifies; a tampered
audit entry is detected; a backup restores to a working database.

**Done when:** deleting the database and restarting recreates it cleanly, and a restore
from backup produces a working application.

### Phase 3: Excel import

The highest risk phase. Budget more time than seems reasonable.

Import lives in `Linewise.Infrastructure` behind an interface in `Linewise.Application`.
The source sheet lists employees down a column and dates across the top. Cell background
colour indicates status, using two distinct colours for working and off.

#### Requirements

- ClosedXML. Not EPPlus.
- Cell colour is unreliable. Handle indexed colours, theme colours and conditional
  formatting, not only plain RGB fills. Fall back gracefully when a colour cannot be
  resolved rather than crashing.
- Colour meaning is user configurable, never hardcoded.
- Name matching is fuzzy: trailing and doubled whitespace, case, surname first versus last,
  and common typos. Match against Employee.Aliases as well as FullName.
- ImportTemplate entity holding header row index, name column index, first date column
  index, date format, and colour mappings. This is what allows a different factory with a
  different layout to use the product.
- Agency and temporary workers: an unknown name must be addable during review as a
  temporary employee without leaving the import. Food production runs heavily on agency
  staff and a weekly hard stop on unknown names will kill adoption.
- Bulk employee import from a simple sheet, so initial setup does not require typing 150
  people by hand.

#### Three step pipeline

1. Parse into an intermediate ImportResult. No database writes.
2. Review: matched rows, unmatched names, ambiguous matches, unreadable cells.
3. Commit only after explicit confirmation.

The raw file bytes are stored with the committed import so it can be re-run after a
mapping fix.

#### Security

The .xlsx is a zip archive. Cap total uncompressed size and entry count to defeat
decompression bombs. Reject archives whose structure does not match the expected package
layout. Never evaluate formulas. Parse failures produce warnings, never an exception
reaching the user.

#### Tests

Synthetic .xlsx fixtures generated in the test project rather than committed files.
Covering: a clean sheet imports fully; trailing whitespace still matches; surname first
still matches; an unknown name surfaces as unmatched rather than being dropped; a theme
coloured cell resolves; an unreadable cell warns; an oversized archive is rejected.

**Done when:** run against a real anonymised sheet from the factory with an empty or fully
explainable unmatched list.

### Phase 4: Printed output

PDF generation with QuestPDF in `Linewise.Infrastructure` behind an interface in
`Linewise.Application`. The QuestPDF licence type is set explicitly at startup.

Three layouts:

1. Full sheet: one page per day and shift, all lines.
2. Per line sheet: one page per line, for posting at that line.
3. Amendment slip: a compact page showing only what changed since the last print, for
   pinning beside an already posted roster.

#### Design constraints

- Read from several feet away on a wall. Large type, strong hierarchy.
- Legible in monochrome. Never convey meaning by colour alone; use weight, borders and
  labels.
- Header carries company name and logo, date, shift, and the roster version number so a
  stale sheet on the wall is identifiable at a glance.
- Line leaders unmistakably marked.
- Configurable paper size, orientation, base font size and layout.

#### Security

Any exported cell value beginning with `=`, `+`, `-` or `@` is prefixed with an apostrophe
to prevent formula injection when opened in a spreadsheet application.

Tests generate each layout, assert a valid non empty PDF with the expected page count, and
write output to a folder for visual inspection.

**Done when:** printed on a real printer, posted on a wall, and read from four feet.

### Phase 5: Desktop shell and roster grid

The Avalonia shell and the roster grid, read only for now.

#### Setup

- Avalonia with FluentAvalonia. Native on Windows, visually consistent with Office.
- CommunityToolkit.Mvvm. Strict MVVM, no logic in code behind.
- Microsoft.Extensions.DependencyInjection wired at startup.
- Serilog to a rolling file in the per user application data folder, with a destructuring
  policy redacting employee names, aliases and user data file paths.
- Global unhandled exception handler that logs and shows a friendly dialog. A user must
  never see a stack trace.
- Optional application lock: PIN or Windows account check on launch and after an idle
  timeout. A lock, not an authorisation system.

#### Main window

Week grid with production lines as rows, days as columns, people as chips. Toolbar above
with import, generate and print. An always visible warnings strip listing understaffed
lines and missing leaders. Status bar with assigned, off and overtime counts. Draft versus
published state in the title bar.

#### Visual rules

- Density over whitespace. A full week across all lines fits without scrolling.
- Chrome is near neutral charcoal and grey. Each line has one accent colour, and those are
  the only saturated colours on screen.
- Never convey meaning by colour alone; pair with an icon or label. Roughly 8% of men have
  a colour vision deficiency and the source sheet already uses red and green, the worst
  possible pair.
- Embedded typeface (Inter), no *fetched* webfonts, and no typeface chosen for fashion.
  Amended by ADR 0013; the original rule said system font. No animation beyond instant
  state changes.

#### First run

With no database, no employees and no lines, guide the user through creating lines and
running a first import. Never drop them into an empty grid.

`Linewise.ConsoleHarness` is deleted in this phase. Done: the real window does
everything it demonstrated, and a throwaway kept past its purpose becomes something
somebody maintains by accident.

**Done when:** a roster generated in phase 1 and stored in phase 2 renders correctly, and
the window resizes without breaking.

### Phase 6: Editing and last minute changes

The feature that decides whether the product is used or abandoned, because somebody rings
in sick every week.

- Drag and drop between cells. Every manual move sets IsLocked and Source = Manual, so
  regeneration does not undo it.
- Full undo and redo.
- Mark absent: removes a person from every slot they hold that day and opens a replacement
  picker for each vacated slot.
- The replacement picker is ranked, never alphabetical: availability, required skills,
  preference for that line. It warns if taking that person creates a hole elsewhere.
- Cascade visibility: any change dropping a line below RequiredHeadcount highlights
  immediately in the grid and the warnings strip.
- Manual mode: clear a day entirely and place everyone by hand. Some weeks the manager will
  disagree with the engine, and forcing them to fight a generated result is how software
  gets abandoned.
- Every change captures a reason, written to the audit chain.
- Publishing increments the version, marks the printed sheet stale, and offers either a
  full reprint or an amendment slip.
- Draft autosaved continuously. A crash loses nothing.

ViewModel tests for the undo stack, lock survival through regeneration, and replacement
ranking.

**Done when:** a Monday morning sick call can be handled end to end, from marking absent to
amendment slip printed, in under thirty seconds.

### Phase 7: Configuration and rules

Everything the manager configures without code.

- Employee management: search, filter, add, deactivate. Never hard delete, so historic
  rosters stay intact.
- Per employee line preferences: drag to reorder ranked list, Mandatory and Blocked
  toggles, skills, leader eligibility.
- A check rules action running the phase 1 validator, reporting impossible configurations
  before generation is attempted.
- Line management: names, required headcount per shift, required skills, display order,
  accent colour.
- Shift pattern configuration.
- Import template management including colour mappings.
- Print settings: layout, paper size, orientation, font size, company name and logo.
- Terminology settings. Some sites say line, others station or cell; some say leader,
  others supervisor. The nouns are configurable.
- Tie break strategy: fairness or seniority.
- Retention settings for rosters and audit entries.

All settings persist to the database, not a config file, so they travel with a backup.

#### Day one usability

The application must be useful with zero rules configured, producing a reasonable roster
from availability and headcount alone, and accumulating rules as the manager corrects its
output. Requiring full configuration before first value is the most common way this kind of
tool dies.

**Done when:** a fictional second factory can be configured with different line names, a
different sheet layout and different terminology, without recompiling.

### Phase 8: Hardening and release

#### Packaging

- Velopack installer with auto update. Update packages signed, signature verified before
  apply, feed over HTTPS with certificate validation.
- Sign the installer with an OV code signing certificate. Be clear what this buys: it does
  not remove SmartScreen warnings, since Microsoft removed EV instant reputation in 2024
  and reputation now builds only from download volume, which a product sold to a handful of
  factories will never reach. Sign it for the verified publisher name in the UAC prompt,
  and because antivirus and application allowlisting treat signed binaries far more
  leniently, which matters most when auto update replaces files on disk. OV, not EV. Not
  before shipping: certificates are capped at 458 days validity and the key must live on a
  hardware token or cloud signing service.
- A one page install guide warning that a SmartScreen prompt will appear and stating
  exactly what to click.
- Version in an About dialog matching the assembly version.

#### Verification

- Backup and restore tested end to end, not just backup.
- `dotnet list package --vulnerable --include-transitive` failing the build on a known
  advisory.
- Confirm no real employee data anywhere in the repository or its history.

#### Repository files

Already in place: LICENSE, SECURITY.md, CHANGELOG.md, docs/adr/,
docs/threat-model.md, docs/data-protection.md, .github/workflows/ci.yml,
.github/pull_request_template.md. Written early rather than at release, because a
licence file that appears in the last week of a project is one nobody thought
about.

Written since: README.md, THIRD-PARTY-NOTICES.txt, docs/architecture.md.

Still to write: a user manual for the manager, including the restore procedure, and the
customer licence agreement.

No CONTRIBUTING.md. This project does not accept contributions and an empty ritual file is
worse than no file.

#### README constraints

- Screenshot immediately after the title, before any prose.
- Badges for CI status, .NET version, licence.
- Build and test commands that can be copied and run as written.
- A Known limitations section, kept honest and current.
- Under roughly a screen and a half in total. Everything else is a link.
- Terse fragments. Not full sentences with a bold lead-in on every bullet.
- No emoji in headings, no marketing voice, no Features section with decoration.
- Section lengths deliberately uneven. Some should be one line.

Two parts are written by hand at the end rather than drafted early, and stay as
placeholders until then: the Why section explaining what the tool replaces and why it
exists, and the Known limitations entries.

#### Anonymisation

The client company is never named anywhere in this repository. Refer to a food
manufacturing client. No real employee names, no real production line names, no real
headcounts, no real sample sheets, including in test fixtures and screenshots.

Commercial material stays outside the repository entirely.

#### Other documents

- ARCHITECTURE.md explaining the layered design and why Domain has no dependencies.
- docs/adr/ numbered decision records: desktop over web, SQLite over SQL Server, ClosedXML
  over EPPlus, greedy assignment over a constraint solver, single machine over networked.
  Context, decision, consequences, a paragraph each.
- docs/threat-model.md and docs/data-protection.md.
- CHANGELOG.md.
- A user manual written for the manager, not a developer, including the restore procedure.
- LICENSE stating terms explicitly. Not MIT or Apache if this is to be sold. Either an
  explicit all rights reserved notice for portfolio and evaluation purposes, or PolyForm
  Noncommercial 1.0.0. Repeated in the README.
- THIRD-PARTY-NOTICES.txt shipped with the installer, reproducing every dependency
  copyright notice. MIT and Apache 2.0 both require this even in a proprietary product.
  Never add a GPL or AGPL dependency.
- A customer licence agreement, distinct from the repository licence, covering permitted
  machines, ownership, support and updates, liability, and data handling on termination.

**Done when:** someone who has never seen the repository can clone it, run the tests, and
understand the architecture in ten minutes.

## 9. Validation

Before phase 6, replay a real month of historic data through the engine and compare
its output against what the manager actually produced by hand. Every difference is a
rule that was never articulated, and there will be more than expected. This is the
single highest value testing activity in the project.

## 10. Open questions

| # | Question | Owner |
|---|---|---|
| 1 | Retention period for rosters and audit entries, and its legal basis | Developer |
| 2 | Do night shifts and day shifts share a preference set or need separate ones | Manager |
| 3 | How are agency workers currently identified on the source sheet | Manager |
| 4 | What proportion of a typical week is manually overridden after generation | Manager |

Commercial questions, including IP ownership and dependency licence thresholds, are
tracked outside this repository.

## 11. Revision history

| Version | Date | Change |
|---|---|---|
| 1.0 | 29 Jul 2026 | Initial design. Single machine scope confirmed, networked deployment explicitly deferred. |
| 1.1 | 3 Aug 2026 | The site's sheet changed from coloured cells to written marks. Templates now read either. Holiday added as a status, distinct from a blank cell. LineDemand added, letting a line's headcount be raised for a day; overtime is routed to those lines first, and mismatches are warned about rather than enforced. |
| 1.2 | 3 Aug 2026 | Overtime routing moved ahead of ranked preferences, making ten rules rather than nine. Placing it at the backfill step, as 1.1 had it, meant an overtime worker with a first choice elsewhere was already on that line before the busy one was ever considered, so the rule would rarely have fired. |
