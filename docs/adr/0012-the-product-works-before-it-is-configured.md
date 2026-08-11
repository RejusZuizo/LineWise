# 12. The product works before it is configured

Date: 2026-08-11

Status: Accepted

## Context

The design document states that the application must be useful with zero rules configured,
producing a reasonable roster from availability and headcount alone, and accumulating rules
as the manager corrects its output. It names requiring full configuration before first value
as the most common way this kind of tool dies.

Building the desktop shell turned that from a principle into two concrete decisions, both
found by running the application rather than by testing it.

**A week with no shifts produces no days.** A roster is generated per shift. A first run has
no shifts, so generate returned a roster with nothing in it, and the screen showed an empty
grid with nothing explaining why. The failure mode was a blank screen rather than an error,
which is the kind that gets reported as "it does not work".

**A first import has no template to parse against.** A template describes the shape of one
factory's sheet, and the screen that builds one is in phase 7. Without a default, the import
button had nothing to do until a configuration screen two phases away.

Both could have been answered by refusing: an error saying "configure a shift first", a
dialog saying "create a template first". That is defensible, honest, and precisely the
behaviour the design document warns against.

## Decision

Where a missing configuration has an obvious answer, the application supplies it and carries
on.

Generation creates a single day shift per date when a week has none. It only ever adds, so a
site that configures nights keeps them.

The first import creates a default template describing the layout in front of us: names down
the first column, dates across the first row, and the words the site writes in its cells.

Both defaults are ordinary records once created, editable on the screens that arrive in
phase 7. Neither is special-cased anywhere else, and nothing reads "is this the default".

## Consequences

A person can install this, import a sheet, add their lines and print a roster without being
sent to a settings screen first. That is the property the design document asked for, and it
is now demonstrable rather than aspirational.

The cost is that the application makes assumptions on the operator's behalf. Two things keep
that honest. Each default is visible and editable rather than hidden in code, so somebody who
disagrees can change it rather than work around it. And each is a shape the site has actually
shown us, rather than a guess: the template's rules are the marks on the sheet in front of
us, and the single day shift is what a site running one shift a day has.

Neither default is silent about what it did. A defaulted template is a row in a list; a
defaulted shift produces days that appear on screen. The alternative — inferring something
and not recording it — would be the version of this decision worth objecting to.

The risk worth naming: a default that is wrong for a second factory is more expensive than
no default, because it produces a plausible answer rather than an obvious failure. That is
the argument for phase 7's template editor arriving before this is sold to anybody, and it
is why the import surfaces unrecognised marks rather than quietly treating them as a day off.
