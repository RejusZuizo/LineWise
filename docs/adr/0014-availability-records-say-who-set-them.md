# 14. Availability records say who set them

Date: 2026-08-17

Status: Accepted

## Context

Phase 6 starts with marking somebody absent, and the first question it asks is what that
writes. Absence is a change to two different records at once: availability, because the
person is not in today, and the roster, because the slots they held need filling.

Two answers were on the table. Write both, so the absence survives a regenerate. Or write
only the roster, which is honest about what it touched and leaves the imported sheet
alone, at the cost of the next Generate putting the absent person straight back onto a
line.

Roster-only was rejected quickly: a button that is undone by the button next to it is not
a feature. Writing both is right, and reading the code turned up the reason it is not
enough on its own.

`IAvailabilityRepository.ReplaceAsync` deleted and rewrote every record in a date range,
because an import is a statement about a whole week rather than an addition to it. That is
correct for the sheet. It is wrong for the manager. A sheet corrected and re-sent on
Tuesday afternoon would have restored everybody marked absent that morning, with no
warning and nothing on screen to say it had happened — the same failure `IsLocked` already
exists to prevent for an assignment, on the one record that had no equivalent.

## Decision

`Availability` carries an `AvailabilitySource` of `Imported` or `Manual`, defaulting to
`Imported` so every record written before this reads as what it was.

`ReplaceAsync` becomes `ReplaceImportedAsync`, which deletes only the imported records in
the range and skips any incoming record that would land on a manual one. It returns the
records it kept, and the import raises one warning per record naming the person and the
day. `SetManualAsync` writes the other kind, and forces `Manual` rather than accepting a
source from its caller: the method name is the promise.

The engine does not read the new field. Who is available is the same question whoever
answered it, and provenance decides only what an import may overwrite. That keeps this out
of the rule ordering entirely.

The rename is deliberate. `ReplaceAsync` would have gone on promising more than it
delivers, and ADR 0011 has already been through one round of a name asserting something
untrue.

## Consequences

Marking somebody absent survives both a regenerate and a re-import, which is what makes it
worth building. The screen can always distinguish what the sheet said from what the
manager said, because both are still there.

Three costs are worth naming. The import no longer replaces a week unconditionally, so
"re-import to start again" is no longer strictly true — a manual record has to be changed
by hand, and reporting each one is what stops that being a surprise rather than a
discovery. The count of records written now excludes what was kept, which is a smaller
number than the sheet's row count and the one the operator actually wants. And a second
composite index sits on the availability table, because the import's delete now filters on
source as well as date.

The alternative considered and rejected was a separate `Absence` entity beside
availability. It would have kept the imported data pristine, at the price of two tables
answering "is this person in on Tuesday" and an ordering rule between them. One record
with a provenance field says the same thing without the second place to look.
