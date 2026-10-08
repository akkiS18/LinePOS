# Debt DB component bridge — stage 3A-2a

`DebtSyncStore.cs` / `DebtSyncStore.kt` export and import the validated components
from stage 3A-1 using real SQLite/Room writer transactions. There are no network/UI
call sites, no capability advertisement, no ACK/cursor advancement and no release of
HELD journal entries. A complete sale/items/stock transport envelope is still required.

## Export and durable frozen content

`ExportCustomer` / `ExportEvent` read and freeze a component inside one transaction.
A customer uses its original `debt_customer_create:<guid>` command snapshot and
original creation device, never current mutable name/archive metadata. Missing original
creation metadata is an integrity error; do not invent it from today's customer row.

An event includes its original actor/device/sequence, immutable opening account and
all line-index-ordered allocations. Export verifies the command receipt and contiguous
line indices `0..n-1`. Missing receipts, unsupported kinds/references/schema or broken
component invariants fail. The codec continues to validate exact numbers and identity.

`sync_meta[debt_wire_v1:customer:<guid>]` and `sync_meta[debt_wire_v1:event:<guid>]`
store `SHA256(full canonical component) + newline + full canonical component`.
An existing seal must be exactly equal; it is never updated or silently regenerated.
Exports after restart reproduce the same bytes. A corrupted seal or later changed
component causes a visible integrity exception.

This reuses the existing metadata table, so no Room/debt schema migration is needed.
These keys and original creation snapshots are essential persistent data. Ordinary
full DB snapshots preserve them, but the existing desktop `download_db` endpoint
strips `sync_meta` and remains unsafe for debt restore until integration fixes it.

## Atomic import

`Apply(customerPackets, eventPackets, writeSale?)` copies and decodes the input, then
uses a single writer transaction. Its limits are 500 components and 8 MiB total ASCII
component text, in addition to individual codec limits. Full transport envelope size
and local pre-commit preflight remain integration requirements.

Customer creations are processed before openings, and openings before payments;
input event order may therefore place a payment before its opening. Line order inside
an event is never changed. All mutations, retry receipts, frozen seals and held journal
entries commit together. Any rejected actor, dependency, ownership, collision or SQL
failure rolls back the entire batch, including earlier customer/sale writes.

Imported event/device/request IDs are preserved. An existing event is reconstructed
from DB and compared byte-for-byte with the incoming full component before accepting
replay; a command-payload hash alone cannot validate allocation lines. This also handles
an echo of a local event that was not previously exported/sealed. Matching replay does
not call the sale adapter, append a line or create another journal entry. A different
allocation or device sequence with the same request is an integrity conflict.

Creation requests cannot reuse financial request IDs and financial requests cannot
reuse customer creation IDs. Existing SQL uniqueness also rejects a different event
that reuses a sender's `(device_guid, device_sequence)` pair. Orphaned seals/receipts
are not repaired by import.

For each new payment the DB verifies that every account exists and belongs to the
header's customer and store. It applies the sender's frozen deltas directly, without
calling local `TakePayment`, reallocating against receiver balances, or rejecting an
event merely because a contact is now archived. Two legitimate offline collections
can therefore converge to customer credit. Contact replay does not undo renames or
unarchive a customer.

Missing customer/account or absent required sale adapter throws the distinguishable
`DebtDependencyException`; the transaction rolls back. There is **no durable inbox
implementation yet**. The future transport must keep/resend the complete group or
persist it atomically in a validated inbox. Never ACK this exception or discard money.

## Trusted host and sale adapter

The host supplies an already bound store UUID, `canSync` gate and `canImportActor`
policy. Both permissions are checked within the writer transaction (including replay).
Store binding, schema version, FK enforcement and idle sync-control state are required.
The bridge never automatically binds/rebinds a receiver to the packet's store.
The actor policy must validate trusted provenance, including legitimate historical
forwarded events; a packet-provided actor ID is not proof of authorization. Authentication,
identity mapping, pairing capabilities and role policy are still host/transport work.

For a new sale opening, `writeSale` must synchronously write the associated sale/items/
stock on the supplied connection/transaction, after the full envelope has been validated.
The bridge verifies persisted total, cash, card and timestamp against the frozen command,
then inserts the opening event and account. It refuses to attach a new opening to an
already existing unrelated receipt. An identical replay skips the callback entirely.

The callback must not commit the transaction, use another connection, perform network
or external side effects, or schedule deferred writes. It must validate the basket
fingerprint, item/stock effects, historical cost/FX and payment type. The component
bridge cannot infer those from the debt component alone.

`sync_control.applying=1` suppresses ordinary echo capture throughout the receiver
transaction and returns to zero on commit/rollback. Only held debt component journal
rows are added here; they do not yet encode a complete relayable sale/stock envelope.
The current public `Apply` owns its transaction. The envelope stage must extract an
internal transaction participant or otherwise extend this same boundary; calling
`Apply` and then saving the outer receipt/cursor in a second transaction is forbidden.
Future transport must persist the full incoming envelope and its own full-body dedup
receipt, including callback effects, before acknowledging or forwarding it. A seal of
the debt component alone cannot prove that the surrounding stock/body was unchanged.

## Tests and remaining integration gates

Tests use four independent production databases on .NET and Android Room. They cover
opening-before-payment dependency sorting, cross-database export/import, separate
offline payments converging to credit, local echo, restart and concurrent replay,
archived/renamed contacts, altered lines with unchanged command payload, sender-sequence
collision, wrong account owner/store, denied permissions, missing dependencies, callback
failure, injected seal failure, corrupt seals and gapped stored lines. Desktop uses real
product-stock capture; Android uses a transactional stock-effect probe, plus existing
production capture tests. The Android receiver DB is physically closed/reopened before
replay. All imported debt journal entries remain HELD.

Stage 3A-2b-1 now supplies the pure frozen financial envelope codec (see
`DEBT_ENVELOPE.md`). Next bounded stage: local frozen capture/preflight and a durable
full-envelope receiver/inbox, then handshake/push/pull/ACK adapters and old-peer barriers. Audit full/delta pull,
metadata coalescing, stripped DB download and restore identity as documented in
`DEBT_WIRE.md`. UI, release and main merge remain blocked on those integrations.
Current verified run/commit evidence lives in `DEBT_CHECKPOINT_UZ.md`.
