# Data protection

| | |
|---|---|
| **Status** | Living document. Positions marked *to confirm* are not yet settled. |
| **Last updated** | 5 August 2026 |

Extracted from section 4.3 of the technical design, which now points here.

This document records the positions the software takes. It is not legal advice,
and the deploying organisation remains the data controller for the employee data
this application holds.

## What is held

| Data | Why it is held |
|---|---|
| Employee name | Printed on the roster, so a person can find their line |
| Aliases | Matching names as they appear on an imported spreadsheet |
| Active or inactive | Keeping historic rosters intact after somebody leaves |
| Temporary or agency flag | Agency staff turn over weekly and are treated differently |
| Skills, line preferences, leader eligibility | Deciding who may work where |
| Availability status per day | Working, Off, Overtime or Holiday |
| Assignments | Which line and shift, and which rule placed them there |
| Audit entries | Who changed what, when, and why |

## What is deliberately not held

No addresses. No contact details. No dates of birth. No employment terms, pay, or
grade. No reason for an absence beyond the status enum — "Holiday" is recorded,
"hospital appointment" is not, and this is the single most important
minimisation decision in the design, because a reason for absence would drag
health data into the application.

## Roles

The deploying organisation is the **controller**. The software is a tool it runs
on its own machine; there is no processor relationship, because no data reaches
the author or any third party. There are no network calls, no telemetry, no crash
reporting service, and no cloud storage.

## Retention

**Rosters and assignment history: six years from the end of the employment
relationship. Audit entries: the same.** *To confirm with the deploying
organisation.*

The basis for proposing six years is the limitation period for bringing a claim
on a contract in England and Wales, during which an employer may need to evidence
who worked where and when. Employers commonly align employment record retention
with that period.

This is a starting position and a configurable setting, not an assertion about
what any particular organisation must do. Whoever owns the deploying
organisation's GDPR position should confirm or replace it, and the number they
give should be recorded here with their reasoning rather than substituted
silently.

Retention is enforced by a setting in the database rather than a hardcoded
constant, so it travels with a backup and can be changed without a release.

## Erasure

Employees are **deactivated, never hard deleted**, so historic rosters stay
intact and continue to say what they said at the time.

A separate anonymisation routine replaces name fields on records past the
retention window, leaving the assignment history intact and the person
unidentifiable.

The position to state to any employee exercising a right to erasure: employment
record retention obligations constrain that right while they apply, and once the
retention window passes, anonymisation removes the identifying data while
preserving the operational record. That is a position for the controller to take
and defend, and it is recorded here so it is taken deliberately.

## Access and portability

There is no self service. An employee asking what the system holds about them is
answered by the manager, from the employee record and the roster history. This is
a consequence of the single user design and should be stated in the user manual
rather than discovered during a subject access request.

## Security

See [threat-model.md](threat-model.md). The controls relevant here are encryption
at rest, encrypted backups, the tamper evident audit log, and the logging
redaction policy that keeps names out of log files.

## Test data

**No real employee data anywhere.** Not in the repository, not in fixtures, not
in screenshots, not in commit messages, not in issues, and not in git history.
Test fixtures use obviously fictional names — Ada Fictional, Bram Invented, Cleo
Notreal — chosen so that a real name appearing among them would be conspicuous.

Spreadsheet fixtures are generated in the test project at runtime rather than
committed, because a file from the factory is personal data regardless of which
folder it sits in.

`.gitignore` refuses `*.db`, `*.db-shm`, `*.db-wal` and any `.xlsx` outside a
test fixtures folder, so the common accidents fail closed.

## Open points

| # | Point | Owner |
|---|---|---|
| 1 | Confirm the retention period and its basis | Deploying organisation |
| 2 | Whether agency staff records follow the same retention as employees | Deploying organisation |
| 3 | Where the anonymisation routine sits in the delivery plan | Developer |
