# 10. The printed sheet carries no meaning in colour

Date: 2026-08-03

Status: Accepted

## Context

Lines each have an accent colour, and using it on the printed roster is the obvious way to
tell them apart. Three things argue against relying on it. Factory printers are frequently
monochrome, and nobody finds out which one the sheet went to until it is on the wall.
Sheets get photocopied, which flattens colour further. And roughly eight percent of men
have a colour vision deficiency, on a sheet read mostly by men, in a product whose source
spreadsheet already distinguishes states using red and green.

The line leader is the case that matters most. Somebody arriving at a line needs to know
who to ask within a second or two, from several feet away, on whatever came out of the
printer.

## Decision

Nothing on the printed sheet is distinguished by colour alone. The line leader is marked
three ways at once: a filled block before the name, the name in bold, and the word LEADER
beside it. Any one of those surviving is enough. Line accent colours are off by default and
sit behind a setting, so a site that has checked its printer can turn them on as decoration
rather than as information.

Type size is one configurable number that everything else scales from, because the distance
a sheet is read from is a property of the wall it is pinned to and not something that can be
decided here.

## Consequences

The sheet is plainer than it could be, and a site with a colour printer gets less out of it
than the screen will. That is the right trade for something whose whole purpose is being
legible in a corridor. Marking the leader three times costs horizontal space, which is part
of why the full sheet is one day per page rather than a week to a page: a week across the
top would shrink every name to something nobody could read while walking past.

A test writes each layout to `artifacts/print-preview` for a person to open, because
producing a valid PDF and producing one that can be read across a factory are different
claims and only the first can be asserted. Looking at the output is what caught the
amendment slip explaining a leader mark it does not use.
