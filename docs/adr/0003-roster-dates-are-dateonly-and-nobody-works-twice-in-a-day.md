# 3. Roster dates are DateOnly, and nobody is rostered twice in a day

Date: 2026-07-31

Status: Accepted

## Context

Two questions the briefs left open had to be settled before the engine could be written.
First, what type a roster date is. The standing rule that all dates are stored as UTC
exists to stop instants drifting between time zones, but a roster date is not an instant.
Nobody works the Tuesday shift in another time zone, and converting a factory date to UTC
and back is how a Monday becomes a Sunday. Second, whether an employee available on a
date may be placed on more than one shift that day, since availability is recorded per
date while assignment happens per shift.

## Decision

Roster dates in the domain are `DateOnly`. The UTC rule continues to apply to instants:
audit timestamps, publication times and anything else recording when something happened.
An employee gets at most one place per date across every shift generated in that call.
Locked assignments are exempt, because a lock is the manager overruling the engine and
the engine does not argue with them.

## Consequences

A roster date means the same thing regardless of where the machine thinks it is, and the
mapping in phase 2 stores it as a date rather than a timestamp. The one-place-per-day rule
means a night shift generated in the same call cannot reuse somebody already on days,
which is the behaviour a factory expects but does prevent a genuine double shift being
generated automatically. A manager who wants one can still place it by hand, where it will
be locked and left alone. If double shifts turn out to be routine rather than exceptional,
this is the rule to revisit.
