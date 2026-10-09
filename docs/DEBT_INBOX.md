# Durable debt inbox — 3A-2b-2a

`DebtEnvelopeInbox.cs` / `DebtEnvelopeInbox.kt` persist validated canonical packets and
atomically receive **customer creation and payment**. This bounded stage does NOT
implement the concrete sale/item/stock adapter or local before-commit envelope freeze.
Every valid `sale_open` packet stays `WaitingForSaleAdapter`, even when some components
already exist locally. No callback can bypass this gate through the new inbox API.

Existing component `DebtSyncStore.Apply` remains available for its earlier trusted
component use/tests. It is not a network endpoint or a substitute for the missing
full-sale receiver. None of these APIs are connected to the app UI/transport yet.

## API and explicit outcomes

The host supplies a bound store, `canSync`, and `canImportActor`. Global permission,
schema/store binding, writer FK enforcement and idle sync-control are checked by the
shared internal `DebtSyncStore.Write` transaction entry. Every packet actor (including
the optional customer creation's actor) is authorized inside that transaction, even
on identical retries. Authentication/provenance/forwarded-actor mapping is still the
future host's responsibility; a wire actor UUID is not proof of permission.

- `Receive(wire, receivedAt)` / `receive`: strictly decode before entering the writer
  transaction. `receivedAt` is trusted local nonnegative time; an identical pending
  retry preserves the first timestamp and original bytes.
- `Applied`: complete supported packet and full-body receipt are durably committed.
  A first echo of an already-local component may only add the outer receipt.
- `AlreadyApplied`: exact full-body receipt AND persisted component reconstruction
  match. No extra ledger, allocation, journal or contact write is performed.
- `WaitingForDependency`: packet is durably pending; financial effects are NOT applied.
- `WaitingForSaleAdapter`: opening is durably pending; no customer/sale/stock/event is
  partially applied. This is an explicit unfinished integration gate.
- `ReadPending(guid)` / `readPending`: returns immutable wire/first timestamp/reason,
  or null. Checks stored GUID/store/body and current authorization again.
- `ExportApplied(guid)` / `exportApplied`: returns exact original completed wire, or
  null. Rechecks receipt hash/body and component reconstruction; never rebuilds a
  packet from current mutable contact/product data.

There is no automatic retry worker here. After the missing dependency commits, call
Receive again with the same stored wire; concurrent retries are safe. ReadPending
followed by Receive may race another receiver, but the writer-side receipt comparison
makes that race an identical replay. All public operations should be called outside
an enclosing application transaction: a future transport must not ACK before its
actual outermost commit has completed.

## Transaction and replay contract

Customer/event operations now have **internal** transaction-participant visibility;
no new public arbitrary-SQL callback was added. The inbox uses the SAME existing writer
transaction for preflight, ledger/journal writes, full receipt insert and inbox removal.
It never invokes the public component Apply and then writes a receipt in a second tx.

Before mutation, known customer/event bodies, request collisions, sender sequence,
customer/store and account ownership are checked. Dependencies are inspected within
the same writer snapshot; missing accounts/customer cause a pending row without any
component writes. Wrong ownership/body/sequence is a hard conflict, not a successful
pending result. During application `sync_control.applying=1` suppresses ordinary echo
capture; it returns to zero on commit or rollback. Frozen sender allocation is applied
through the component bridge; local `TakePayment` is never re-run.

Unexpected exceptions after mutation propagate and roll back the entire operation.
They are never caught and converted into a partially applied pending result. In
particular, failure inserting the outer receipt OR deleting the old inbox row rolls
back the event, command receipt, allocation lines, component seal and held journal.
A customer create and its outer receipt also roll back together.

Pending identity is `debt_sync_inbox.packet_guid`, with exact canonical payload/store
comparison. Changed content under an existing pending/completed ID is rejected even
when independently codec-valid, including adding/removing the optional customer wire.
The sender must therefore freeze ONE outer body, not regenerate variants on retry.

Completed receipt in existing `sync_meta`:

- key: `debt_envelope_v1:<packet-guid>`
- value: `SHA256(full canonical envelope) + newline + full canonical envelope`

The receipt is retained for durable replay and exact future relay. It is not a network
ACK, signature or source outbox implementation. A pending row and completed receipt
for the same ID conflict; neither is silently repaired or evicted. Mutable contact
rename/archive does not change the frozen original customer creation on replay.
Existing debt journal entries remain `acked=-1` (HELD).

## Bounded storage and errors

Existing schema v1 `debt_sync_inbox` is reused; no Room/schema migration. Maximum 128
pending packets and 32 MiB total canonical ASCII payload characters per database.
Envelope's existing 6 MiB individual limit still applies. Android pending/receipt
body reads use 65,536-character SQL substr chunks to keep each cursor row bounded;
reading or comparing a valid multi-MiB body does not require a single giant cursor row.
Android bind arguments follow placeholder occurrence order, including retry reason
updates and chunk offsets. Both aggregate limits are
checked in the writer transaction before adding a new pending row. Identical pending
retries do not consume a second slot, and applicable dependency/customer packets are
allowed to commit even when the pending queue is full. Capacity raises the distinct
`DebtInboxFullException`; the sender must retain the packet. No oldest-first eviction,
TTL deletion or misleading ACK is implemented.

Only validated known v1 packets enter this inbox. Malformed/unknown versions/kinds are
rejected without persistence or cursor/ACK advancement. Durable quarantine of unknown
future formats, operator UI, background retry scheduling and disk-full handling at the
transport boundary remain future work. Permission, integrity and SQL errors likewise
propagate; existing pending data stays intact on rollback. The sender retains data
until the eventual authenticated end-to-end protocol confirms full acceptance.

The existing stripped desktop DB download deletes sync_meta; it would delete these
full receipts as well as older creation/component seals. It must become debt-aware
before enabling UI/transport. Main/release/Firebase/CBU remain unchanged.

## Verification and next work

Real .NET production SQLite and Android Room tests cover restart before/after apply,
missing-account wait without partial customer writes, preserved receive time/body,
strict pending/completed conflicts, concurrent once-only payment, rename/archive,
permission checks on retry/export, outer-receipt and inbox-delete failure rollback,
customer rollback, original relay bytes, count/byte quota and retry while full,
corrupt completed receipt, held journal, and the explicit opening gate. Quota byte
stress deliberately seeds occupied DB bytes; it is not an accepted malformed packet.
A valid >2 MiB opening envelope is persisted, reloaded after a Room restart and
compared on retry without changing its first timestamp. Test opening dependencies use the prior trusted component fixture callback, not a
claim that concrete sale/items/stock import is implemented.

Next bounded stage **3A-2b-2b**: concrete frozen sale/items/stock DB adapter, exact
legacy numeric conversion checks, local envelope freeze/preflight in the same sale
transaction, and opening replay/stock-operation identity validation. Extend this SAME
receipt/inbox transaction when lifting the opening gate; never add a separate commit.
Then implement authenticated transport/ACK/full/delta/download gates in 3A-2c.
Current CI evidence and exact commit are in `DEBT_CHECKPOINT_UZ.md`.

### Android model prerequisite (before the concrete adapter)

Android now reads Room `DEBT` receipts as `PaymentType.DEBT`, rather than silently
falling back to CASH. Existing receipt/report labels show Nasiya. No customer/account
is inferred from a legacy DEBT header, and no new checkout option is exposed.
`SaleRepository.completeSale` rejects DEBT before its writer transaction, including a
fully paid DEBT request. The concrete ledger sale API must be used once implemented.
The legacy JSON sale codec rejects DEBT import/export (including an already-known sale
GUID in a snapshot), so it cannot silently import a debt header as CASH or acknowledge
that header without its ledger envelope. Snapshot exceptions roll back its existing
outer transaction/cursor. These guards are not the complete 3A-2c protocol, journal
coalescing, restored-DB, full/delta or server-side barriers; all remain required before
feature enablement. The inbox opening gate above remains unchanged.
