# 7. What the audit hash covers

Date: 2026-08-03

Status: Accepted

## Context

Each audit entry stores the SHA-256 of the entry before it, so history cannot be edited
quietly. Turning that sentence into code forced three questions the design did not settle.
What exactly gets hashed, given that the sequence number is assigned by the database after
the entry is built. How fields are separated, given that a free text reason can contain any
punctuation a separator might use. And what stops somebody appending a second entry that
claims the same predecessor, producing two histories that both verify.

## Decision

The hash covers the timestamp, user, action, summary, reason and previous hash, joined with
the ASCII unit separator. The sequence number is excluded, because the store assigns it
after the entry is sealed. Ordering is still protected: verification walks in sequence
order and checks that each entry points at the hash of the one before it, so removing,
inserting or reordering entries all break a link. The timestamp is formatted with a fixed
pattern that ignores `DateTimeKind`, since SQLite returns Unspecified and any conversion on
the way out would produce a different hash than the one computed on the way in. A unique
index on the previous hash column stops the chain forking. Hashing and verification live in
the domain, not in the database layer, because the chain is a rule about what an audit log
is rather than a detail of how one is stored.

## Consequences

Verification reports the first entry that fails and stops, distinguishing an edited entry
from a broken link, which tells the operator whether a row was changed or removed. Tests
tamper with the database directly through SQL, the way somebody with a SQLite browser and
the key would, and both cases are caught. Two costs are worth naming. A separator inside a
reason field would still be possible to craft if somebody could write arbitrary control
characters into it, so the field is length limited and written by the application rather
than pasted through. And appending is a read of the last hash followed by an insert, which
is not atomic; on a single machine with a single operator that race does not arise, and the
unique index turns it into a failed insert rather than a forked history if it ever does.
