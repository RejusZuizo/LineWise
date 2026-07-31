# 2. The engine takes an immutable snapshot, not a repository

Date: 2026-07-31

Status: Accepted

## Context

The engine needs employees, lines, preferences, availability, locked assignments and a
window of recent history. The obvious way to give it those is a repository interface it
can query. That would make the engine's behaviour depend on what the database held at the
moment each query ran, make a test a matter of mocking five interfaces, and leave the
door open for somebody to add a lazily loaded navigation property later and quietly turn
a pure function into one that touches a file.

## Decision

`Generate` takes one `AssignmentRequest`: an immutable record holding everything the
engine is allowed to know. All domain types are sealed records with init-only properties,
so nothing can be modified underneath a generation in progress. The engine reads no
repository, opens no file and writes no log. A `GenerationContext` is built per call to
hold the indexed lookups and the running fairness ledger, so the engine itself carries
nothing between calls and can safely be a singleton.

## Consequences

A test is a plain object graph and an assertion, which is why the suite needs no mocking
framework. Reproducing a disputed roster is a matter of keeping the request. The caller
now has to decide how far back the history window reaches and load it up front, which is
a real cost and is the caller's decision to make rather than the engine's. Immutable
records will need care when EF Core maps them in phase 2, since updating a tracked entity
means constructing a new one rather than assigning to a property.
