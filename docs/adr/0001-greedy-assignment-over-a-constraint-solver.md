# 1. Greedy rule ordering rather than a constraint solver

Date: 2026-07-31

Status: Accepted

## Context

Rostering is a constraint satisfaction problem, and there are mature solvers for it.
A solver would find better rosters than a hand written greedy pass, particularly when
preferences, skills and fairness pull against each other. It would also arrive as a
dependency in the Application layer, produce answers nobody can explain, and turn every
"why is she on Ovens today" into a shrug. The manager is not asking for the optimal
roster. They are replacing a spreadsheet they filled in by hand, and their first question
about any generated result will be why it did what it did.

## Decision

The engine applies nine rules in a fixed order: locked assignments, availability,
mandatory preferences, leader selection, ranked preferences across the whole workforce at
once, the tie break, the absolute blocked and skill filter, backfill, then warnings. Each
placement records which rule made it and at what preference rank. The one part likely to
be argued about, who wins a tie, sits behind `ITieBreakStrategy` and can be swapped
without touching the engine.

## Consequences

Rosters are produced in tens of milliseconds against the two second budget, and every
placement can be justified in one sentence. The engine will sometimes leave a line short
where a solver would have found a rearrangement that filled it, and it does not attempt
to undo an earlier placement to make a later one possible. That is visible rather than
hidden: every shortfall becomes a warning the manager can act on. If this trade turns out
to be wrong, the surface to replace is `IAssignmentEngine` alone, since nothing outside it
knows how a placement was chosen.
