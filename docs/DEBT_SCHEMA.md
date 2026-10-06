# Debt schema v1 (stage 2B-1)

Canonical SQL: `debt/schema.sql`. Its Android asset and desktop embedded resource
must be byte-identical; `tests/test_debt_schema.py` enforces this.

This stage installs empty auxiliary tables, not a usable debt feature. No old DEBT
receipt becomes a customer debt automatically. No debt write is captured or sent by
the current sync transport. UI and transactional repositories are subsequent stages.

## Migration lifecycle

- Android Room 13 -> 14: snapshot first, then atomic DDL in Room's migration
  transaction. Room enables foreign keys in its generated onOpen, after migration;
  `installDuringMigration` therefore checks referential integrity explicitly and
  `install` requires enabled foreign keys on the transaction writer on normal open. A WAL read connection on API 35 can report a different connection-local PRAGMA, so both the installer and test check inside the writer transaction. The test also attempts a real orphan insert and requires rejection. The installer never disables keys.
- Desktop: an existing sales database without the debt marker is backed up before
  installing the schema. A fresh database has no old data to back up. Reopening an
  already migrated database does not create another migration backup.
- Marker `debt_schema.version=1` is independent of Android's user_version=14.
  Desktop does not pretend that all of its pre-existing tables are Room entities.
- Both installers reject an unknown version, partial debt table set, mismatched
  SQL definitions, or broken debt foreign keys. DDL and marker are one transaction.
- SQL definition checks deliberately fail closed if an existing debt table's
  constraints were edited. This is not automatic repair or data recovery.

## Repository handoff (2B-2)

The database enforces integer amounts, foreign keys, customer/store ownership,
request uniqueness, device-sequence uniqueness, one reversal per referenced event,
and no UPDATE/DELETE of financial records. This does **not** validate full business
semantics by itself. A repository must validate event kind/reference, canonical GUID
and payload hash, line totals, actor/store identity, and authorization.

`original_debt_minor` is the opening balance. The `sale_open` event is the audit
anchor, not a second opening delta. Do not count the initial sale cash/card again as
a later debt collection. All subsequent signed account changes are event lines.

Event headers/lines and the existing `sync_journal` outbox must commit atomically.
Appending a line to a previously finalized event must be rejected by the repository;
SQL's immutable UPDATE/DELETE triggers alone do not seal a multi-row event. Identical
request replay returns the original result; a different body must fail, not overwrite.

Accounts, including their historical name and initial due date, are immutable. A
future due-date edit needs a separate audited model. Customer contact metadata is
mutable; equal phone numbers are allowed and must not auto-merge distinct people.
Store binding is not seeded here and cannot be changed with a plain UPDATE.

Missing dependencies can be kept in `debt_sync_inbox` only after validating the
packet's store. This table is storage groundwork, not an implemented inbox protocol.

## Tests

- Python: exact shipped SQL, 14 debt cases plus 7 existing capture cases.
- C#: production `DatabaseContext`/`DebtSchema`, fresh/reopen, preserved legacy sale,
  backup before migration, unsupported schema and atomic DDL rollback.
- Android API 26/35: actual Room upgrade, backup version/data, fresh schema,
  foreign-key enforcement, future-version refusal, DDL rollback and snapshot copy.

See `docs/DEBT_CHECKPOINT_UZ.md` for verified CI runs and remaining work. Do not
interpret schema-level rollback tests as completed repository or device-sync tests.

Verified code commit: `a0b7060b917825510faec18068dd1d373146f689`.
[CI run 37332080525](https://github.com/akkiS18/LinePOS/actions/runs/37332080525) passed core/desktop-build/android-build and both API 26/35 instrumentation jobs. The arithmetic parity workflow also passed: [37332080765](https://github.com/akkiS18/LinePOS/actions/runs/37332080765).
