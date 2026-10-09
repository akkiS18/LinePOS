# Desktop frozen sale receiver — 3A-2b-2b desktop part

`DebtSaleReceiver.cs` is an internal participant in the existing
`DebtEnvelopeInbox` writer transaction. It persists the canonical historical sale,
items, stock deltas, ledger opening and full-envelope receipt atomically. No network,
UI or local checkout source API is connected to it in this stage.

## Opt-in and dependencies

The desktop inbox constructor accepts an optional `Func<string,long?> resolveActorUser`.
Without it, new opening packets retain `WaitingForSaleAdapter`. Supplying it enables
the concrete receiver, not an arbitrary sale-writing callback. The existing canSync,
canImportActor, store, schema and transaction-state checks still apply on receive and
replay/export. The resolver runs synchronously inside the writer boundary and must be
trusted, side-effect-free host policy, never a value taken from incoming JSON.

Desktop has no users table: the host owns the authenticated source actor GUID to
positive local attribution ID mapping. Null/nonpositive mapping waits for dependency;
there is no automatic user 1 fallback. Android's later port must also validate its
actual local users dependency. Source actor GUID remains in the immutable event.
Transport authentication and forwarded-actor authorization are still stage 3A-2c.

All product and warehouse GUIDs must exist. Missing metadata produces durable
`WaitingForDependency` without partially creating a customer, sale or stock record.
Existing soft-deleted metadata can still identify a previously accepted offline sale.
Current product/warehouse names, prices and costs never replace historical fields.
A missing stock row for otherwise known metadata starts at zero; negative stock is
intentionally allowed. Multiple item rows for one product/warehouse accumulate their
individual deltas. Other warehouses remain unchanged and the product aggregate is
recomputed across all warehouses with decimal arithmetic. Existing stock timestamps
never regress to the older incoming sale timestamp.

## Numeric boundary

The wire codec already checks totals, FX, fees and canonical decimal strings. The
receiver additionally preflights legacy SQLite REAL persistence before writing.
Amounts in tiyin divide exactly by 100 as decimal; quantities, prices, unit costs,
FX, fees and stock use decimal calculations. A decimal -> double -> invariant shortest
roundtrip decimal must equal the original value. Values with magnitude over 1e18,
non-finite existing stock, lossy amounts or intermediate/final stock balances reject
the transaction rather than silently round. This is intentionally stricter than the
pure wire codec: a codec-valid packet may be unsafe for current legacy columns.
The future local source adapter must use equivalent precision preflight before
accepting a sale, so it cannot create a permanently unreceivable financial envelope.

## One commit and replay

The inbox preflights and mutates under the same writer transaction. `applying=1`
suppresses ordinary sale/product/stock echo triggers. The concrete participant writes:

- `sales` with DEBT=3, historical monetary/FX/time fields and trusted user attribution;
- `sale_items` with original GUIDs, order, product/warehouse identity and snapshots;
- final `product_stocks` quantities and the product aggregate;
- one held `debt_stock` journal row per original stock operation GUID, with its frozen
  delta, product, warehouse, item GUID payload and opening event group;
- held `debt_sale` journal marker (`debt-sale:<sale-guid>`), whose payload is
  `<trusted-local-user-id>:<SHA256(canonical-sale-wire)>` in the opening group.

The existing bridge then writes customer/account/event/component receipts and the
inbox writes the full-body receipt and removes pending state before the SAME commit.
Unexpected failures propagate and roll back every write. Waiting outcomes are only
produced during preflight, never by catching a failure after mutation.

Any reused stock operation ID (regardless of journal kind), item GUID, sale GUID or
opening event without the exact completed full-envelope receipt is a conflict. A
matching legacy header or component-only fixture cannot establish that stock was
applied once, so it cannot be attached or repaired implicitly. Local-source first-echo
support therefore remains blocked until source-side full-envelope freeze is added.

Exact completed replay verifies the original full body, component reconstruction,
persisted sale/header/items and durable operation markers. It makes no new stock or
money changes. Current stock is NOT compared to a historical resulting balance:
legitimate later sales and receipts must not invalidate replay. Relay returns the
original envelope bytes. A changed body or corrupted historical row/movement fails.
Existing completed openings can be verified/exported even by an inbox instance with
no new-opening resolver; current canSync/canImportActor authorization is still checked.

## Evidence and remaining gates

`DebtSaleReceiverTests` uses production DatabaseContext/SQLite and exercises dependency
retry, non-default actor mapping, historical snapshots, negative stock, absent stock
rows, aggregate balance, restart/replay, concurrent duplicates, changed packet/item/
movement, operation collision, unrelated legacy sale, precision rejection and ten
injected rollback boundaries (including second item, ledger, outer receipt and inbox
delete). Existing default opening-gate tests remain unchanged. Exact CI results are
in `DEBT_CHECKPOINT_UZ.md`.

This is the desktop receiver part only. Android still gates all opening packets.
Next: port and test the concrete receiver in Room, then add local sale/envelope freeze
and size/precision preflight in the source transaction. No standalone network ACK,
auto retry worker, capability advertisement or journal release is added. Journal
pruning/coalescing and stripped database download must preserve the durable evidence
above in 3A-2c. UI, returns, reports, backups and release gates remain in the full plan.
Firebase/CBU and main are unchanged.
