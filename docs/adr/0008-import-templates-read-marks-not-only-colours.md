# 8. Import templates read marks, not only colours

Date: 2026-08-03

Status: Accepted

## Context

The design described an availability sheet where cell background colour carried the
meaning, with two colours separating working from off. Partway through building the
importer the site changed its sheet: cells now say "work" or "holiday", or say nothing at
all. The obvious response was to swap the colour reader for a text reader and move on. The
less obvious observation is that the sheet had just demonstrated it can change, and that a
parser written against whichever scheme happens to be current will need rewriting the next
time somebody reorganises a spreadsheet.

## Decision

An import template holds a list of rules, each mapping either a piece of text or a fill
colour to a status. Text rules are tried before colour rules regardless of the order they
are stored in, because text survives being copied between workbooks, emailed and re-saved,
and colour frequently does not. An empty cell means whatever the template says it means,
which is a day off everywhere seen so far. A mark that matches no rule falls back to that
same default and raises a warning, so a colour nobody mentioned gets noticed rather than
quietly becoming a day off. Theme colours are resolved through the workbook theme with
their tint applied, since the standard Excel colour picker produces theme colours and
treating them as unreadable would fail on most coloured sheets in the wild.

Holiday was added as a status alongside Off. Both mean unavailable and the engine treats
them identically, but a planned absence and a blank cell are different things to the
manager staring at a line that is two people short.

## Consequences

Both schemes work, and both are covered by tests, so the older coloured sheet still imports
if one turns up from an archive or a second site. Adding a third scheme is configuration
rather than code. The cost is a template with more in it than a single-scheme parser would
have needed, and a rule list that a site could in principle configure into contradicting
itself; text winning over colour makes that outcome predictable rather than arbitrary.
Colours that genuinely cannot be resolved, an indexed colour outside the palette or a theme
part that is absent, are reported as unreadable rather than guessed at, which turns a
silently wrong roster into a visible question.
