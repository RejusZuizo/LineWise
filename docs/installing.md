# Installing Linewise

One page. Read it before double clicking, because Windows is going to object once and you
need to know that it is expected.

## What you need

A Windows PC. Nothing else. The .NET runtime is inside the installer, so there is nothing to
install first.

## Windows will warn you, once

When you run the installer, Windows SmartScreen will show a blue box saying **"Windows
protected your PC"** and offering only a **Don't run** button.

To continue:

1. Click **More info**.
2. Click **Run anyway**.

That is the whole thing. It will not ask again on this machine.

## Why it warns

The installer is not code signed. A signing certificate has to be bought, renewed every year
or so, and kept on a hardware token, and that has not been done yet.

Being honest about what signing would fix: it would put a real publisher name in the prompt
instead of "Unknown publisher", and antivirus and application allowlisting treat signed
programs far more leniently. It would **not** remove the SmartScreen warning. Microsoft
removed instant reputation for new certificates in 2024, and reputation now builds from
download volume that a product installed on a handful of factory PCs will never reach.

So the warning is expected, and it stays expected. If you would rather not click through it,
do not install this.

## Where it puts things

| | |
|---|---|
| The application | `%LOCALAPPDATA%\Linewise` |
| Your data | `%LOCALAPPDATA%\Linewise\data` |
| Backups | ten rolling copies, beside the data |
| Logs | beside the data, names redacted |

Per user, not per machine, so it does not need an administrator. Nothing is written to
Program Files and nothing touches the registry beyond a shortcut.

## Your data is encrypted, and the key is tied to this machine

The database is encrypted with a key derived from your Windows account on this PC.

**This matters more than it sounds.** Copying the database file to another machine, or to
another Windows account, gets you a file nobody can open. That includes the backups, which
are encrypted with the same key.

If the PC is replaced or the Windows profile is rebuilt, the roster history goes with it
unless somebody has exported what they need first. That is a deliberate trade: it means a
stolen laptop is a stolen brick rather than the personal data of every employee on site.

## Updating

Linewise checks for updates on startup and applies them in the background. There is nothing
to do.

## Uninstalling

Settings, Apps, Linewise, Uninstall. Your data folder is left behind on purpose, so a
reinstall finds the roster history where it was. Delete `%LOCALAPPDATA%\Linewise` by hand if
you want it gone.

## If it will not start

The log is the first thing to look at: `%LOCALAPPDATA%\Linewise\data\logs`. It holds no
employee names by design, so it is safe to send on.

A healthy start writes two lines, and the second is the one that matters:

```
Main window created.
Loaded roster for "2026-08-17": 5 lines, 1 days, 166 placed
```

The first line on its own means the window opened but could not read anything.
