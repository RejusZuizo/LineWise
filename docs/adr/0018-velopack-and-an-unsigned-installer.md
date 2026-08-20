# 18. Velopack, self contained, and an installer nobody has signed

Date: 2026-08-19

Status: Accepted

## Context

The application could only be run with `dotnet run` from a source checkout. That is not a
product, and everything else in phase 8 depends on there being an artefact to sign, test and
hand to somebody.

Three decisions had to be made at once: what builds the installer, what goes inside it, and
what to do about code signing.

## Decision

**Velopack.** MIT licensed, produces a real installer plus an auto update feed from one
command, and needs no Windows SDK on the build machine. MSIX wants a signing certificate
before it will install at all, which turns the signing question into a blocker rather than a
trade. WiX is heavier than a single user desktop application justifies. Squirrel is what
Velopack replaced.

`VelopackApp.Build().Run()` is the first statement in `Main`, before the logger. On an
installed build that call is how the application answers the install, update and uninstall
hooks, and anything done before it happens during a hook that is not supposed to be running
an application: opening a log file, building a container, or on an update, writing into a
folder that is about to be replaced. It does nothing when run normally, which is why it is
safe to have in place before anybody has installed anything.

**Self contained, win-x64.** The factory PC is a machine nobody here administers, and
"install the .NET runtime first" is a support call rather than an instruction. It costs about
147 MB unpacked, paid once at install.

**Not trimmed.** Avalonia resolves controls through reflection and this application resolves
services the same way. A trimmed build fails at the moment somebody opens a screen rather
than at build time, and no size saving is worth a defect shaped like that.

**Unsigned, and said out loud.** An OV certificate has to be bought, renewed inside a 458 day
cap, and kept on a hardware token or a cloud signing service. None of that has happened.

What matters is being accurate about what signing would buy, because it is easy to assume it
buys more than it does. It would put a real publisher name in the prompt instead of "Unknown
publisher", and antivirus and application allowlisting treat signed binaries far more
leniently — which matters most when an auto update replaces files on disk. It would **not**
remove the SmartScreen warning. Microsoft removed instant reputation for new certificates in
2024, and reputation now accrues from download volume that a product installed on a handful
of factory PCs will never reach.

So the warning is expected, permanently, and `docs/installing.md` tells the person exactly
what to click rather than leaving them to decide whether the thing they were sent is
malware.

## Consequences

There is an installer, and a release is a tag rather than a ceremony. The release workflow
runs the whole test suite again before packing, because a tag can be moved and a release
built from code nobody tested is the one thing that workflow must not produce. It creates a
draft release rather than publishing one, so the last step is still a person.

**The honest limit: none of this has been run.** Packaging is the one part of this repository
that cannot be tested from the development machine. `vpk pack` on Linux builds a Linux
AppImage; the same command on Windows builds the Windows installer, and only the second is
what this project wants. The publish step was verified here — it cross compiles, produces
`Linewise.exe`, and carries the SQLCipher native binaries, which is the piece most likely to
go missing and would only fail once it reached a factory PC. Everything after that is written
from the documentation and has never executed.

The auto update feed has no home yet. Velopack will produce the files; nothing serves them.
Until a feed exists, updating means running a newer installer, and the workflow's output is a
draft release with the installer attached rather than a published channel.

QuestPDF's Community licence terms depend on the revenue of the deploying company. That
threshold has to be checked before this is sold, and it is recorded in
THIRD-PARTY-NOTICES.txt as well as here because it is the kind of thing that gets discovered
late.
