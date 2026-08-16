# 13. The application ships its own typeface

Date: 2026-08-12

Status: Accepted

Reverses a rule in section 8 of the technical design.

## Context

The design document's phase 5 visual rules say: "System font. No imported webfonts. No
animation beyond instant state changes."

That rule was written to prevent three things, and it is worth separating them, because
only two of them are still true when the font is embedded rather than fetched:

- A network call at startup, or a page that renders wrong until a font arrives. Real.
- A product that looks foreign on the machine it runs on. Arguable.
- Typography chosen for fashion rather than for the job. Real, and the one worth keeping.

The screen this product is judged on is a grid of short names and small numerals: "3 of 4"
at around eleven points, a hundred and forty times, read at a glance to find the cells that
are short. What that needs is unambiguous digits, a large x-height, and a one that cannot be
mistaken for a lowercase L.

The system font is whatever the machine is set to. On the office PC that is likely Segoe UI,
which is fine. It is not guaranteed, and it is not the same on the machine this is developed
on, which means the layout is being tuned against a font the product may not use.

## Decision

The application embeds Inter and sets it as the default family.

Embedded, through `Avalonia.Fonts.Inter`, not fetched. There is no network call, nothing to
fail at startup, and no behaviour that differs between a machine with the font installed and
one without. The application looks the same on the factory PC as it does here, which is the
property the layout work assumed all along.

The printed sheet is unaffected. QuestPDF has its own typography and its own constraints,
which are about a monochrome laser and four feet of distance rather than about a screen.

## Consequences

The rendered layout is now a property of this repository rather than of the machine. Density
decisions can be made once and trusted.

The cost is a font file in the installer and one more dependency to keep current, which
Dependabot already watches.

The part of the original rule that survives, and should be quoted at anybody proposing the
next font: no *fetched* fonts, and no typeface chosen because it looks current. Inter is here
for its digits at small sizes. If a future change wants a different face, the question to
answer is what it does for a wall of "3 of 4" — not whether it looks more modern.

The design document's section 8 should be amended rather than left to contradict this, and
that is done in the same change.
