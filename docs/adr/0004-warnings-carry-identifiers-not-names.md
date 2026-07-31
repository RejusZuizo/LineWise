# 4. Warnings carry identifiers, and the text comes from a resource file

Date: 2026-07-31

Status: Accepted

## Context

Each warning needs human readable text, and the obvious text names the person: "Ada
Fictional is available but not placed on any line." That string then travels wherever the
warning travels. The Serilog redaction policy planned for phase 5 destructures objects and
would not save a message whose personal data had already been baked into a string. Warning
text is also user facing, and user facing strings belong in resource files from the
outset rather than being retrofitted once the factory floor turns out to be multilingual.

## Decision

`RosterWarning` carries a `WarningCode`, an optional `LineId`, an optional `EmployeeId`
and a date. Its `Message` is resolved from `EngineWarnings.resx` and names lines but never
people. Whatever displays a warning resolves the identifier to a name at the point of
display. Anything deciding behaviour, tests included, keys off the code and never off the
text.

## Consequences

A warning that reaches a log file carries no personal data, which is one fewer thing for
the redaction policy to catch, and translating the product means translating one resource
file rather than hunting string literals through the engine. The cost is that a warning is
no longer self contained: it needs the employee list beside it to read properly, which the
console harness demonstrates. Message text also varies with the current culture while the
assignments themselves do not, so determinism is a property of the roster and of the
warning codes, not of the rendered strings.
