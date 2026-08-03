# 9. Overtime follows demand rather than preference

Date: 2026-08-03

Status: Accepted

## Context

Overtime existed as a status but did nothing except permit a mandatory preference to be
broken. Nobody works overtime for its own sake: it is worked because a line has more
product to get out than its usual complement can handle. The engine had no way of knowing
which line that was, and no reason to send the extra person there.

Two shapes were possible. A flag on a line saying overtime is authorised, plus a separate
headcount, or one number saying what the line needs that day. The first can contradict
itself: a line authorised for overtime but with no raised headcount has nowhere to put
anybody, and a raised headcount with no authorisation is a line short of people that the
engine is forbidden to fill.

An earlier draft of this put the routing at the backfill step, which was wrong. An
overtime worker with a first choice elsewhere is placed on it while preferences are being
filled, so by the time backfill runs they are already standing on the wrong line. The rule
would have been correct on paper and almost never fired.

## Decision

`LineDemand` holds one number per line per date, replacing that line's standard headcount
for the day. Above standard means busy, and busy is where overtime goes. Below standard is
allowed too, because a line can be quiet.

Routing runs immediately after leader selection and before ranked preferences, so somebody
on overtime is placed where they are needed before they can take a line they merely prefer.
It is a preference and not a restriction: an overtime worker blocked from the busy line, or
lacking a skill it requires, falls through to the ordinary rules rather than being left
standing. Two warnings cover the gaps: a line whose raised demand went uncovered, and
somebody on overtime who ended up on a line that was not busy.

## Consequences

The engine now has a reason to place people that has nothing to do with what they want,
which is correct but worth knowing when reading a roster: an assignment explained as
overtime cover is not the engine ignoring somebody's preferences by accident. Short
handedness and uncovered demand are reported separately, because a line that cannot run and
a line that will run without its planned cover are different problems and the manager acts
on them differently. Shortfall is measured against the smaller of the standard headcount
and the demand, so quietening a line for a day does not produce a spurious warning that it
is short of people; a test exists for that because the first implementation got it wrong.
