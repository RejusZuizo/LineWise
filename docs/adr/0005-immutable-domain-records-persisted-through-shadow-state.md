# 5. Immutable domain records persisted through shadow state

Date: 2026-08-03

Status: Accepted

## Context

ADR 2 made every domain type a sealed record with init-only properties, and noted that EF
Core would need care as a result. It needed more than care. An `Assignment` is what the
engine produces and what the determinism tests compare, so it deliberately carries no
identifier and no reference to the roster it belongs to. A relational store wants both.
The same problem appears on `Employee.SkillIds` and `Employee.Aliases`, which are
collections on the record and want to be tables. The obvious fixes were all bad: adding a
`RosterVersionId` to `Assignment` would put a persistence concern in the engine's output
and break its value equality, and flattening the collections into delimited strings would
hide lists inside columns.

## Decision

Identifiers and parent keys live in EF shadow state, declared in the entity configuration
and set explicitly by the repository on insert. Collections become tables of their own,
mapped through row types that exist only inside the infrastructure assembly, and the
repository composes the domain record from them on the way out. `RosterDay` gets a row of
its own carrying an ordinal, so day order and empty days survive a round trip rather than
being re-derived by grouping. Roster children are read back ordered by their insert key,
so a reloaded roster is in exactly the order the engine produced.

## Consequences

The domain compiles without any reference to EF Core and the schema is properly normalised,
with no lists hiding in columns. The round trip test compares full records, explanations
and warning text included, and passes. The cost lands on the repository, which is longer
and more explicit than one built on navigation properties would be: it sets shadow keys by
name, and a rename that misses one of those strings would fail at run time rather than at
compile time. The constants at the top of the roster repository exist to keep that blast
radius to one file. This trade is worth revisiting only if the repository layer starts
growing faster than the domain it serves.
