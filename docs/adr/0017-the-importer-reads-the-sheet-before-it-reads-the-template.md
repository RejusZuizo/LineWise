# 17. The importer reads the sheet before it gives up on it

Date: 2026-08-18

Status: Accepted

Extends ADR 0008.

## Context

ADR 0008 made an import template describe the shape of a sheet: which row holds the dates,
which column holds the names, and what a mark in a cell means. That decision was right and
this does not reverse it. What it left is a chicken and egg problem the product has been
living with since ADR 0012 papered over it.

A template describes one factory's sheet. A first import has no template, so ADR 0012 had
the application invent one describing the layout in front of us — names in column A, dates
across row 1 — and carry on. That is a guess dressed as configuration. It is correct for
the sheet we have seen and wrong for a sheet with a title block above the dates, a company
logo, a payroll number beside the names, or a blank row somebody left for spacing. All four
are ordinary spreadsheet furniture, and any of them makes the import find nothing at all.

The failure mode is the bad kind. The template's indices point at cells that hold nothing
useful, no dates are found, and the operator is told the sheet has no date columns — which
is false, and which they cannot act on without a configuration screen to go and correct.

## Decision

The importer works the layout out by reading the sheet, and does it as a rescue rather than
as a policy.

The template is tried first and wins whenever it produces rows. A layout somebody has
configured, or that was learned from a previous import, is never quietly overruled by a
guess. Only when parsing produces nothing does the detector run.

Detection looks for one thing: a row holding at least two dates, and a column to the left of
those dates holding text that is not a number and not a date. The row with the most dates
wins. Two dates rather than one, because a single date on a row is as likely to be a
"printed on" stamp in a corner as the start of a week.

The name column is the one holding the most values that read like names. Counting non-empty
cells is not enough and the first version of this got it wrong: a sheet with a payroll number
in column A and the name in column B fills both columns completely, the count ties, and the
tie-break took the numbers. Excluding anything whose whole text parses as a number separates
1001 from Ada Fictional without needing to know what a name looks like.

**What is detected is written back onto the template when the import is committed.** That is
the whole of "it adapts": the shape is worked out once, becomes ordinary configuration, and
the next sheet of that shape is parsed rather than guessed at. Nothing reads "was this
detected" afterwards, and the template that results is the same kind of object as one typed
in by hand.

The same principle covers names. A spelling the operator ties to somebody is a fact about
that person rather than about this week's sheet, so it is kept as an alias — which the
matcher has read since phase 3. The unfamiliar spelling that had to be resolved every Monday
is resolved once.

Both kinds of learning announce themselves. A detected layout raises a notice saying what
was assumed, in the terms an operator would use to check it, and a learned layout or alias
raises another saying what was kept. A configuration change nobody was told about is one
nobody can account for later.

## Consequences

A sheet nobody has described imports, and the second one of the same shape imports without
detecting anything. That is the property asked for, and it costs one class and a rescue
branch rather than a rewrite of the parser.

**The honest limit: there is no real sheet from the site to test this against.** Detection
was built and tested against the two shapes this project has seen — coloured cells and
written marks — plus deliberate distortions of them: dates pushed down the sheet, names
pushed across it, a numbers column beside the names, a stray date in a corner. It will find
the shapes somebody thought to invent. A sheet nobody imagined will still need a template
built by hand, and the site request that would close this gap is still outstanding.

Detection is deliberately dumb, and dumb in a direction that fails loudly. It refuses rather
than guessing when it cannot find both a date row and a name column, because a layout that
half works produces a plausible roster built from the wrong cells, and that is far worse than
an import that will not run.

What is not detected is what a mark means. A sheet whose cells are coloured rather than
written on still needs somebody to say which colour means what, because no amount of reading
the file reveals that orange means holiday. The unrecognised-mark warning from ADR 0008 is
still how that surfaces, and turning those corrections into template rules the way names are
turned into aliases is the obvious next step rather than something done here.
