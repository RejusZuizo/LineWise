# 11. Development happens on Linux and shipping does not

Date: 2026-08-05

Status: Accepted

Extends ADR 0006.

## Context

ADR 0006 put `Linewise.Infrastructure` on `net8.0-windows`, because the database key comes
from DPAPI and saying so is more honest than suppressing the platform analyser. That
remains right, and this does not reverse it.

What it did not say is what the target framework is actually for. It is a build time
statement to the analyser. It is not a runtime barrier, and the difference turned out to
matter: development moved to Arch Linux, and the assumption was that nothing would run
there at all.

That assumption was wrong. With the SDK installed, the whole solution builds on Linux with
no warnings and the entire test suite passes — including the projects on `net8.0-windows`,
because a platform specific target framework without `UseWPF` or `UseWindowsForms` is
ordinary `net8.0` plus an attribute. The tests never reach DPAPI either, since they
substitute their own key provider precisely so a run does not touch the developer's
protected store.

Exactly one thing does not work: running the application. Startup resolves
`IDatabaseKeyProvider`, gets the DPAPI implementation, and throws.

## Decision

The key provider is chosen at runtime on `OperatingSystem.IsWindows()`.

On Windows it is DPAPI, unchanged. Elsewhere, a debug build gets a provider that keeps the
key in a file with owner-only permissions, and a release build gets one that refuses to
produce a key and explains why.

The registration of the development provider is inside `#if DEBUG`. The enforcement is that
the line is not compiled into a release build, rather than that somebody remembers not to
ship it. The class itself is always compiled, so it can be tested in either configuration;
reaching it in a release build would mean constructing it deliberately, which is a
different act from being handed it by the container.

`WindowsCurrentUser` is renamed `EnvironmentCurrentUser` and registered unconditionally.
It only ever called `Environment.UserName`, which works everywhere. The name asserted a
platform dependency that did not exist, and the first attempt at running on Linux duly
produced an identical second class before anybody noticed.

The development provider is marked `[UnsupportedOSPlatform("windows")]`. This is ADR 0006's
move in the other direction: the assembly claims Windows, so the analyser rejects Unix file
mode calls, and the answer is again to say what is true rather than suppress the warning.

## Consequences

The application can be built and run on the development machine, which is the point.

**The development provider is weaker than DPAPI and this is not a detail.** DPAPI binds the
key to an account on a machine, so a copied file opens nothing. A key file binds to nothing:
any process running as that user can read it, and a backup of the home directory carries
the key next to the database it unlocks. On a machine holding invented names that is an
acceptable trade. Anywhere else it is not, which is why a release build has no such
provider in it rather than merely preferring not to use one.

This is recorded as threat T11. The residual risk is a debug build being distributed, which
no control here prevents and which the release process has to.

Windows remains the platform CI gates on and the platform a release is built on. The Linux
job runs the unit tests for fast feedback and to keep the core honest about portability; it
is not permitted to gate a release on its own.

Nothing about the product's target changes. This ships to Windows.
