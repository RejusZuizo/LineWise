# Threat model

| | |
|---|---|
| **Status** | Living document. Revise when the deployment shape changes. |
| **Last updated** | 5 August 2026 |

Extracted from section 4 of the technical design, which now points here.

## What is being protected

The personal data of every employee on a factory site: names, aliases, working
patterns, and absence status. Under UK GDPR this is personal data, and absence
status sits close enough to health data to be worth treating carefully even
though a reason for absence is never recorded.

## Deployment shape

One installation, on one unattended Windows PC in a factory office, used by one
production manager. No network services, no server, no multi user access, no
authentication system. The output is paper.

That shape removes most of a typical application's attack surface and
concentrates what remains on the machine itself, the files it writes, and the
files it reads.

## Threats and controls

| ID | Threat | Control | State |
|---|---|---|---|
| T1 | Physical access to an unattended machine | Optional application lock, idle timeout | Planned, phase 5 |
| T2 | Database file copied to removable media | Encryption at rest via SQLCipher | Implemented |
| T3 | Backup files as an unprotected full copy of the dataset | Encrypted backups, key never stored beside them | Implemented |
| T4 | Malicious or malformed import file | Archive size limits, structural validation, no macro execution | Implemented |
| T5 | Formula injection reaching a spreadsheet or CSV export | Sanitise leading `=`, `+`, `-`, `@` on exported cell values | Implemented, not yet reachable |
| T6 | Personal data leaking into log files | Serilog destructuring policy redacting names and identifiers | Planned, phase 5 |
| T7 | Compromised or spoofed update package | Signed packages, signature verified before apply, HTTPS only | Planned, phase 8 |
| T8 | Malicious transitive dependency | Lock file with locked mode restore, vulnerability scan in CI | Implemented |
| T9 | Audit history altered to hide a change | Hash chained append only audit log | Implemented |
| T10 | Exported PDFs left in shared folders | Default export path outside shared locations, documented in the manual | Planned, phase 6 |
| T11 | A development key provider reaching a release build | Registration compiled out of release builds | Implemented |

## Controls in detail

### Encryption at rest

SQLCipher through `Microsoft.Data.Sqlite`, with the SQLCipher bundle rather than
the plain one — exactly one SQLite provider may be loaded, which is why the
projects reference `Sqlite.Core`.

The key derives from a machine and user scoped secret held in Windows DPAPI and
is never written to configuration. Extra entropy is mixed in, so a blob lifted
from the machine cannot be unprotected by another application running as the same
user without also knowing that value.

The sharp edge, stated plainly because it is the consequence people discover
late: **losing the Windows profile means losing the database.** Backups are
encrypted with the same key and do not save you. This is why the restore
procedure is documented and tested rather than assumed.

### Tamper evident audit log

Every audit entry stores the SHA-256 hash of the previous entry, forming a chain.
A verification routine walks the chain and reports the first broken link, so
editing or deleting a historic entry becomes detectable rather than silent.
Entries are append only at the application layer: no update or delete path is
exposed.

This is tamper *evidence*, not tamper *prevention*. Anybody who can write to the
database file can rewrite the chain wholesale. What they cannot do is alter one
entry and have it go unnoticed.

### Import as a trust boundary

An `.xlsx` is a zip archive, and a spreadsheet arriving from elsewhere is
untrusted input. The parser caps total uncompressed size and entry count to
defeat decompression bombs, rejects archives whose structure does not match the
expected package layout, and never evaluates formulas.

Parse failures produce warnings, never an exception reaching the user as a stack
trace.

### Formula injection

Any exported cell value beginning with `=`, `+`, `-` or `@` is prefixed with an
apostrophe. Without this, a crafted employee name becomes code execution on
whichever machine opens the export.

Deliberately **not** applied to PDF output. A PDF is never evaluated by a
spreadsheet, and an apostrophe in front of somebody's name on a wall sheet is a
defect rather than a defence. The control belongs to the comma separated and
workbook exports, which is where it will be wired up when those exist.

### Log hygiene

A Serilog destructuring policy redacts employee names, aliases, and any file path
containing user data. Logs are written to the per user application data folder,
never beside the executable.

### Update integrity

Update packages are signed and the signature is verified before application. The
feed is HTTPS only with certificate validation. An updater that fetches and
executes without verification is a remote code execution path with a friendly
name.

### Supply chain

`packages.lock.json` committed, CI restoring with `--locked-mode`, and
`dotnet list package --vulnerable --include-transitive` failing the build on a
known advisory. GitHub Actions are pinned to major version tags; SHA pinning is
open, and is only worth doing alongside a bot that keeps the pins current.

### Application lock

Optional PIN or Windows account check on launch and after an idle timeout. This
is a lock, not an authorisation system. There are no roles, and it is a deterrent
against a passer by rather than a security boundary.

### T11: development key providers

Development happens on Arch Linux; the product ships to Windows. DPAPI does not
exist on Linux, so running the application on the development machine requires a
non-Windows key provider — a libsecret binding, or a mode-0600 key file.

Such a provider is weaker than DPAPI by construction. DPAPI binds the key to an
account on a machine, so a copied file opens nothing. A key file binds to nothing:
any process running as that user can read it, and a backup of the home directory
carries the key next to the database it unlocks.

**Control, implemented.** The key provider is selected at runtime on
`OperatingSystem.IsWindows()`, and the registration of the development provider
sits inside `#if DEBUG`. A release build does not contain the line that would hand
it out; a release build on a non-Windows platform instead resolves a provider that
refuses and says why. The enforcement is that the code is absent, not that somebody
remembers. See ADR 0011.

**Residual risk.** A debug build being distributed. Nothing here prevents that, and
the release process has to: releases are built on Windows, in Release, from a
tagged commit. Worth stating plainly rather than implying the control is total.
