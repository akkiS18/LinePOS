# Local debt repository — stage 2B-2

Implementations: `DebtRepository.cs` and `DebtRepository.kt`. These are local command
boundaries backed by the production SQLite/Room database. No cashier, UI or sync
receiver calls them yet. This is not a debt-feature release.

## Operations and transaction contract

- `BindStore`: bind one canonical, nonzero store UUID, idempotently; refuse rebind.
  The host must obtain a trusted shared store identity during setup/pairing. Do not
  invent an independent store on each already-paired device.
- `CreateCustomer`: insert contact, durable creation-request snapshot in `sync_meta`,
  and held journal entry in one transaction. Customer UUID doubles as creation
  request UUID. Equal phone numbers are allowed. Original creation retry still works
  after a later contact rename/archive; altered retry content is rejected.
- `OpenSale`: the trusted cashier callback writes sale/items/stock using the supplied
  connection/transaction. Repository verifies persisted receipt amount, cash, card,
  and timestamp, then inserts opening event/account, receipt and outbox in that same
  transaction. Failure rolls everything back. Existing sales cannot be retroactively
  attached. Original debt and customer name/due date are frozen in the account.
- `TakePayment`: allocate against locally known open accounts using the pure core,
  freeze allocation lines, and persist event/lines/receipt/outbox atomically. Card fee
  is an expense field and does not reduce debt relief. Collection creates no sale.
- `ReadAccounts`: project original debt plus signed event lines with overflow-safe
  core arithmetic. Currently uses the same permission-gated writer transaction for a
  consistent snapshot; separate read permission/query API belongs to UI integration.

The opening event has zero cash/card/fee and no delta lines: original debt is already
in `debt_accounts.original_debt_minor`. Initial sale tender stays on the sale.

Host prerequisites: installed debt v1 and existing Wi-Fi capture schema, authenticated
actor UUID, store UUID and a real permission callback. Desktop `DatabaseContext`
installs debt schema; the host must also initialize its existing Wi-Fi schema before
constructing a repository. No call site may replace authorization with constant true.
Tests deliberately inject permissions, including a denied case. Local commands also
reject `sync_control.applying!=0` or an already active transaction group, so they cannot
run under remote capture suppression or accidentally join another operation.

The future cashier adapter must validate the basket, item/stock effects, payment type,
fee/FX snapshot and supply a stable SHA-256 basket fingerprint. Repository binds that
fingerprint to retry identity; it does not recompute the basket fingerprint or prove
arbitrary callback SQL correct. Never use a second connection inside the callback.
A successful callback may not launch deferred/asynchronous writes.

## Retry and identity

Request UUID/event UUID are identical for financial commands. Successful identical
retry returns that stored UUID before allocation or cashier callback. A different
canonical payload with the same request fails. Customer-creation and financial request
namespaces cannot reuse an ID. Permission and store checks precede replay.

Canonical format is ASCII JSON: first element `debt-command-v1`, followed by UTF-8
fields encoded separately as base64. Fixed command-specific field order, invariant
integer representation and strict UTF-8 avoid C#/Kotlin JSON escaping differences.
SHA-256 is lowercase hex. Actor/store are bound to the command. Tests compare actual
stored payloads against common independently generated fixtures. This local command
snapshot is **not** the future complete event wire representation.

Each repository instance has a new writer epoch UUID; sequence increments within that
epoch. Restart/copied database writers cannot reuse old `(device_guid, sequence)` pairs.
Existing events and retry identities retain their original values. A stable physical
device display label and restore/server cursor reconciliation remain stage 3B work.
The caller must retain the request UUID and original payload for retries; generating
a new UUID for a second tap describes a new command. UI single-flight/persisted draft
handling is still required at integration.

## Transport hold — mandatory integration boundary

New debt journal entries use `acked=-1` (HELD). A new credit sale also holds all
trigger-captured sale/stock entries in its transaction group. Current mobile pending
selection reads `acked=0`, so these entries are not released by that path.

**This is not a complete legacy transport barrier.** Existing desktop full pull and
other snapshot/legacy export paths can read sales independently of journal status.
Therefore do not wire these methods into live cashier/UI before stage 3 capability
negotiation and every relevant push/pull/full-snapshot path is debt-aware. No release
or main merge is authorized merely because repository tests pass.

Stage 3 must serialize the committed event header and frozen allocation lines, plus
account/customer/sale dependencies, and deliver whole groups only to compatible peers.
A receiver must apply the sender's frozen lines; never invoke `TakePayment` to allocate
again against receiver-local balances. ACK loss and replay must not alter money.
Do not delete or acknowledge held entries before complete peer acceptance. Audit old
journal cleanup paths as part of integrating the held state.

## Boundaries and tests

Only customer creation, credit-sale opening and ordinary collection are implemented.
There is no public arbitrary event/line append API; all lines are created in the same
transaction as their event. Direct SQL is trusted infrastructure, not a security API:
immutable SQL triggers alone do not seal against a malicious extra INSERT.

Returns, reversal, credit transfer/refund, contact edits/conflicts, wire protocol,
restore reconciliation, reports, permission UI and backup workflow integration remain
later stages. Known overpayment is rejected locally; independent disconnected devices
can still collectively overcollect. Reconciliation must preserve all accepted money
and display resulting customer credit rather than discard a valid event.

Tests use real SQLite on .NET and production Room on Android: durable/changed retry,
store and permission guards, archive handling, concurrent identical payment, sale
callback rollback, injected outbox failure rollback, held groups and shared canonical
fixtures. Android additionally closes/reopens Room before sale replay. Desktop tests
exercise product-stock capture; Android uses a transaction probe for callback effects.
These do not replace two physical devices, complete sync or D01–D27 acceptance tests.
