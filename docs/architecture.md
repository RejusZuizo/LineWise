# Architecture

Four projects. Dependencies point inwards, and nothing points back out.

```
Linewise.Domain          entities, enums, the audit chain. ZERO NuGet dependencies.
Linewise.Application     the engine, use cases, and every interface the outside is reached through.
Linewise.Infrastructure  EF Core, SQLCipher, ClosedXML, QuestPDF, DPAPI, the file system.
Linewise.Desktop         Avalonia views and view models.
Linewise.Tests           xUnit. References Infrastructure, so it can prove the real thing works.
```

Nothing in Domain or Application may reference Avalonia, EF Core, ClosedXML, QuestPDF or the
file system. `Linewise.Domain.csproj` contains no `PackageReference` at all, and a test
asserts it.

## Why the domain has no dependencies

Because the rules are the product. Everything else — which database, which spreadsheet
library, which UI framework — is a decision that could be remade, and the engine should
survive all of them. Keeping the domain free of packages is what makes that claim checkable
rather than aspirational.

It also keeps the engine testable without mocks. A test is a plain object graph and an
assertion, which is why the suite needs no mocking framework.

## The engine

`IAssignmentEngine.Generate` takes one immutable `AssignmentRequest` holding everything it is
allowed to know, and returns a `RosterWeek`. It reads no repository, opens no file and writes
no log. Given identical inputs it produces an identical week, and there is a test for that.

Ten rules in a fixed order. The ordering is the design; the count is not, and has changed
twice. Each placement records which rule made it and at what preference rank, so "why is she
on Ovens" has an answer. [ADR 0001](adr/0001-greedy-assignment-over-a-constraint-solver.md),
[ADR 0002](adr/0002-the-engine-takes-an-immutable-snapshot.md).

The one part likely to be argued about — who wins a tie — sits behind `ITieBreakStrategy`.

## Persistence

EF Core over SQLite, encrypted with SQLCipher, keyed from DPAPI on Windows.

Domain types are sealed records with init-only properties, which a relational store does not
naturally like. Identifiers and parent keys live in EF shadow state, set by the repository on
insert; collections become tables of their own, mapped through row types that exist only
inside the infrastructure assembly. The domain therefore compiles without knowing EF Core
exists, and the schema is properly normalised.
[ADR 0005](adr/0005-immutable-domain-records-persisted-through-shadow-state.md).

Every replace is a delete followed by an insert of the same keys, and `ExecuteDelete` does not
touch the change tracker — so each one tells the tracker to forget what it just removed.
Skipping that is what made a screen impossible to save twice.

## Scopes

Repositories are scoped to a database context. The main window is a singleton and lives for
the whole run, so it holds `IServiceScopeFactory` rather than repositories: each operation
opens a scope, works, and disposes it. Each dialog gets a scope of its own, disposed when the
window closes.

`ContainerTests` builds the real container with `ValidateScopes` on, so a singleton holding a
scoped service fails a build rather than reaching an operator.

## The desktop

Strict MVVM. No logic in code behind — the exceptions are closing a window, and two attached
behaviours that own a gesture (Escape to dismiss, drag to reorder) rather than a decision.

View models are constructed and driven in tests without a UI thread, which is the practical
test of whether they are doing the work rather than the views.

Every user-facing string lives in `Resources/Strings.resx`. Singular and plural are separate
entries, never an "s" appended in code. A theory asserts every accessor has an entry, which
has twice caught text that had no business being translatable.

## What is deliberately absent

No authentication or authorisation. One installation, one machine, one operator; supervisors
and workers consume paper. That removes network data access entirely and is the reason the
threat model is about the machine rather than an API.

No network calls of any kind. No telemetry, no crash reporting, no fetched fonts.

## Where the reasoning lives

Seventeen numbered decisions in [adr/](adr/), including the ones later reversed and why.
[technical-design.md](technical-design.md) is the plan; [threat-model.md](threat-model.md)
and [data-protection.md](data-protection.md) are living documents rather than sections of it.
