# Security policy

## Reporting a vulnerability

Report privately. Do not open a public issue.

- Preferred: GitHub's private vulnerability reporting, through the Security tab
  of this repository.
- Alternative: rejusz.coding@gmail.com

Include what you did, what happened, and what you expected. A proof of concept
helps. If the finding involves employee data, describe the data class rather than
sending any actual data.

Expect an acknowledgement within seven days and an assessment within thirty. This
is a single maintainer project, so those are honest numbers rather than
aspirational ones.

There is no bug bounty.

## Supported versions

The most recent release only. This product is deployed to a small number of
installations and updated in place; older versions are not patched.

## What this software is

A single user Windows desktop application running on one office machine, with no
network services, no server component, and no multi user access. It holds the
personal data of the employees it rosters: names, working patterns, and absence
status.

That shape determines what is worth reporting. There is no login to bypass and no
API to abuse. The interesting surface is local.

## In scope

- Anything defeating encryption at rest, or exposing the database key.
- Anything causing the audit chain to accept a tampered or deleted entry.
- Malicious spreadsheet handling: decompression bombs, path traversal through
  archive entries, formula evaluation, or any parse path reaching arbitrary code.
- Formula injection surviving into an exported file.
- Personal data reaching log files, crash reports, or exported PDFs left in
  world readable locations.
- Anything allowing an unsigned or substituted update package to be applied.
- Dependency vulnerabilities reachable from the shipped application.

## Out of scope

- Physical access attacks beyond the stated threat model. An attacker sitting at
  an unlocked machine with the operator's Windows session open is assumed to have
  won; the application lock is a deterrent, not a boundary.
- Denial of service against a single user desktop application.
- Findings that require the operator to be already compromised at the operating
  system level. DPAPI is scoped to that Windows account, and an attacker holding
  the account holds the key by design.
- Missing hardening on packages not reachable from shipped code, for example test
  only dependencies.
- Social engineering.

## Related documents

- [docs/threat-model.md](docs/threat-model.md)
- [docs/data-protection.md](docs/data-protection.md)
