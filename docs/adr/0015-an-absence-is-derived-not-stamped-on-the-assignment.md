# 15. An absence is derived, not stamped on the assignment

Date: 2026-08-18

Status: Accepted

Builds on ADR 0014.

## Context

Phase 6 asks for a name that greys out rather than disappearing when somebody is marked
absent, so the manager can still see who should have been there. That is a requirement
about a screen, and it constrains the data underneath it.

The obvious implementation is a flag on the assignment: `IsAbsent`, set when the manager
marks somebody off. It is one field, it is easy to read, and it is wrong in three separate
ways. The engine produces assignments and its determinism tests compare them, so a field
the engine never sets would have to be defaulted and ignored there. The same fact would
then be stored twice, on the availability record and on every assignment that person holds
that day, with nothing keeping the two in step. And an assignment is what the roster
repository writes wholesale on every autosave, so the flag would have to survive a
regenerate that deliberately does not preserve anything unlocked.

ADR 0014 had already put the absence on the availability record. The question this settles
is what the roster does about it.

## Decision

Nothing is written to the roster at all. Marking somebody absent writes availability and
leaves every assignment they hold exactly as it was.

Absence is then a derived question, answered by one shared type. `Attendance` takes the
week's availability and answers whether a given placement is somebody who will not be in.
The grid asks it, so the chip greys and the cell counts "5 of 6" instead of "6 of 6". The
printed sheet asks it, so the wall sheet leaves them off. `Eligibility` already exists for
the same reason on the engine's side: one answer, so two readers cannot disagree.

`Attendance` deliberately does not read silence as absence. A person with no availability
record at all is treated as present, which is the opposite of the rule the engine applies
when deciding who to place. The two are answering different questions. The engine asks
"may I place this person", where silence has to mean no. This asks "was the roster's
decision overturned", where silence means nothing happened. Reading it the engine's way
would grey out every name in a week whose sheet was never imported, and print a wall sheet
with nobody on it.

The printed sheet omits an absent person rather than striking them through. A wall sheet
answers "who is on this line", and a name on it that will not be there answers it wrongly.
What changed since the last print is the amendment slip's job, which is where the manager
looks for it.

## Consequences

The absent person's chip survives a reload, because their assignment is still in the stored
draft, and disappears on the next regenerate, because the engine reads the availability and
does not place them again. That is the right lifetime for it: within the day it is the
answer to "who was supposed to be on Ovens", and once the week is rebuilt it is history
rather than a hole.

Everything that reads a roster now has to ask who is actually in, and something that forgets
to will quietly overstate a line. Three readers exist today and all three ask. A fourth —
the amendment slip, the per-line sheet's own counts, anything phase 6 adds later — is where
this would first go wrong, which is the argument for the shared type rather than three
`Where` clauses.

The printer gained an input, `Availabilities`, defaulting to empty. Every caller written
before this passes nothing and prints exactly what it printed before.

Testing the printed side needed a proxy. QuestPDF embeds its fonts as subsets, so a name
is glyph indices inside a compressed stream by the time it reaches the file and searching
the bytes for it finds nothing. The test asserts that a sheet printed with an absence is
exactly the size of one printed from a roster that never had that person on the line, and a
different size from one where they are present. Byte equality was rejected: the documents
carry a unique identifier and are not reproducible byte for byte between runs.
