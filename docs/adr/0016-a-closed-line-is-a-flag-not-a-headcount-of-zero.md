# 16. A closed line is a flag, not a headcount of zero

Date: 2026-08-18

Status: Accepted

Extends ADR 0009.

## Context

A line does not run every day. Maintenance, a product that is not being made this week, a
Sunday the site is shut. The manager needs to say so for one date without changing what the
line is for every other date.

`LineDemand` already holds one number per line per date, replacing that line's standard
headcount, and ADR 0009 chose that shape deliberately over a flag plus a separate number
because the two could contradict each other. The obvious move is therefore to say a closed
line is a demand of zero and write no new concept at all.

That is wrong for three reasons, and only the third is obvious in hindsight.

The validator already treats a required headcount of zero as a configuration fault, because
a line that always needs nobody can never be filled and somebody has almost certainly
mistyped. Reusing zero for a deliberate closure means the validator either stops reporting a
real fault or starts reporting a legitimate closure.

A cell drawn for a line with a headcount of zero is an empty cell. So is a cell for a line
that needed four people and found none. Those are opposite situations — one is fine and one
stops production — and the grid would have no way to tell them apart.

And the number is worth keeping. A line closed on Tuesday was going to run at six; if
closing it overwrites the six with a zero, reopening it has nothing to restore and silently
resets the line to its standard headcount.

## Decision

`LineDemand` gains `IsClosed`. The headcount stays on the record and is carried through a
closure untouched, so reopening the day restores what the line was set to run at.

The engine skips a closed line in every rule that places somebody: mandatory preferences,
leader selection, operating assistants, overtime routing, ranked preferences and backfill.
It also skips it in the whole of rule 10, so a closed line raises no shortfall, no missing
leader and no missing assistant warning. A line that is not running is not short of people,
and saying so on a day it is not running would bury the lines that are.

Overtime routing is covered by the same rule: `IsRunningHot` is false for a closed line
whatever number is sitting on its record, so the extra pair of hands goes to a line that is
actually running.

Locked assignments are the exception, as they are everywhere else. A lock is the manager
overruling the engine and the engine does not argue with them, so somebody locked onto a
closed line stays there — and a warning says the two decisions contradict each other,
alongside the two warnings that already exist for a lock that breaks availability or
eligibility.

Closing a line does not move anybody. The write and the redraw are immediate, but the people
on that line stay where the last generate put them until somebody presses generate again.
That is the same rule every other rule change follows: editing a rule does not rewrite a
week that may already be on a wall.

## Consequences

The manager can shut a line for a day and the roster stops pretending it is short of people.
The people who were on it become available to the lines that are running, which is visible
the moment generate is pressed — a test asserts exactly that, because it is the reason for
doing this rather than simply hiding the row.

The cost is one more piece of state per line per day, and a second thing to remember when
reading `LineDemand`: the number means nothing when the flag is set. The property
documentation says so, and `HeadcountFor` returns zero for a closed line so that no caller
has to know the rule.

ADR 0009's argument against a flag still stands where it was aimed. What it rejected was a
flag saying overtime is *authorised* sitting beside a headcount that might not agree with
it — two numbers describing the same thing. Closed is not a second opinion about the
headcount; it is a statement that the headcount does not apply. Those are different, and the
distinction is the whole of why this is a flag and the other was not.
