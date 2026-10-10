# Frozen debt envelope — stage 3A-2b-1

Production codecs: `DebtEnvelope.cs` and `DebtEnvelope.kt`. This stage is a pure,
strict codec/validator with no database writes, inbox, endpoints or UI integration.
It is not the completed 3A-2b DB receiver. Existing customer/event components retain
their v1 format and size limits. Shared strict framing helpers are now internal.

## Envelope and binding

`debt-envelope-v1` is the same canonical ASCII JSON-array/base64/strict-UTF-8 framing
as `DebtWire`. Its five decoded string fields are:

1. Packet GUID (event/request GUID, or customer GUID for customer-only).
2. Store GUID, matching the trusted local expected store.
3. Optional original customer-create wire (empty string if already known).
4. Optional event wire; mandatory except for customer-only.
5. Frozen sale wire; mandatory for `sale_open`, forbidden for payment/customer-only.

A supplied customer must be the event's customer, and its creation request must not
collide with the event request. Event/account/command consistency is checked by the
existing component validator. Sale GUID, timestamp, total/cash/card must match the
opening command; command `SaleFingerprint` is **SHA-256 of the entire canonical
`debt-sale-v1` wire**, including item order, stock operation IDs, historical names,
original costs, FX and fee. A different individually valid sale is not interchangeable.

Customer-only and payment packets do not fabricate a sale. Opening's component
cash/card remain zero as defined in `DebtWire`; initial tender is on the frozen sale.
A decoded packet contains immutable strings; decoded sale item lists are read-only.
`DebtWire.Fingerprint(fullEnvelope)` is the full-body replay fingerprint for the next
DB stage. It is not authentication, and this stage does not implement dedup receipts.

## Frozen sale fields

Tag: `debt-sale-v1`. Header fields (zero-based):

| Index | Value |
|---|---|
| 0–1 | sale GUID, occurred-at timestamp |
| 2–6 | total, historical total cost, initial cash, initial card, initial card fee — integer tiyin |
| 7–9 | card fee percentage, historical USD/UZS rate, payment type (`DEBT` only) |
| 10 | item count |
| 11 onward | 13 strings per item in frozen order |

Per-item fields: item GUID, product GUID, historical product name, category, unit,
warehouse GUID, historical warehouse name, quantity, UZS sale unit price, original
unit cost, original cost currency (`UZS`/`USD`), stock operation GUID, stock delta.
No receiver-local database row IDs or current product/warehouse balances are sent.
Distinct basket rows may reference the same product/warehouse, but item GUIDs and
stock operation GUIDs must each be unique within the sale. Each delta must be exactly
negative quantity, e.g. quantity `2.5` has stock delta `-2.5`. Negative resulting stock
remains allowed. Cross-packet operation identity and DB ownership checks are still
receiver responsibilities; a syntactically valid UUID proves neither.

## Exact numerical contract

- Money is signed Int64-compatible canonical integer text in tiyin, validated here
  as nonnegative; total must be positive and cash + card strictly below total.
- Quantity, UZS price, original cost, FX and fee rate are unique nonnegative decimal
  strings with at most 12 integral and 8 fractional digits. No leading zeroes,
  trailing fractional zeroes, exponent, sign, whitespace, NaN or infinity. `0` is
  valid where allowed; quantity must be positive. Normalization is a caller task,
  and callers must reject precision loss rather than silently truncate input.
- Arbitrary-precision integer intermediates are used on both platforms, at scale
  10^8. Sum all unrounded item values first, then round half-away-from-zero once
  to tiyin. Per-line rounded sums are not interchangeable with this contract.
- Total = rounded sum(quantity × UZS unit price).
- Cost = rounded sum(quantity × original cost × historical rate for USD costs).
  For UZS cost the multiplier is 1. USD cost requires positive historical FX,
  including zero-cost USD items. FX `0` is allowed for all-UZS baskets and means no
  usable historical rate; reports must not substitute today's rate later.
- Initial fee = rounded(card minor × fee percentage / 100), with percentage 0–100
  and fee 0–card. The existing sale adapter maps this snapshot to its historical
  `tax_amount`/`tax_rate` card-commission fields; no tax/report behavior changes here.
- Calculated total/cost must fit Int64 and exactly equal the transmitted amounts.
  No `Double` conversion is used. Subsequent persistence to the legacy floating-point
  sale tables needs an explicit loss/roundtrip check in the DB adapter.

## Size and unsupported data

Sale: 1–1000 items, at most 2 MiB canonical frame. Existing customer/event components
retain their 2 MiB/10,000 event-line limits. Envelope: at most 6 MiB canonical ASCII
frame. Non-ASCII nested content is strict UTF-8 encoded in canonical base64. Unicode
names can exceed byte limits before reaching the item-count limit; both are enforced.
Frame splitting stops at max-fields + 1, so a malformed delimiter flood cannot allocate
an unbounded list of token objects within the byte limit. The shared component parser
uses the same bounded split. A maximum-size malformed frame is covered on all runtimes.

This leaves room beneath the existing 8 MiB HTTP cap for one envelope, but it is NOT
a complete HTTP/batch limit policy. The eventual HTTP wrapper, batch selection and
local writer must preflight the exact serialized request or use atomic fragmentation.
Calling this validator after committing a local sale is too late. UI remains disabled.

Unknown versions/kinds, malformed framing, missing/extra fields, unsupported return
kinds and mismatched snapshots fail closed. Durable quarantine/error handling belongs
to 3A-2b-2; do not interpret rejection as a successful synchronization or ACK.

## DB integration status and next bounded stage: 3A-2b-2b

Stage 3A-2b-2a now provides the validated durable inbox and atomic customer/payment
receiver; see `DEBT_INBOX.md`. Desktop and Android now have the opt-in concrete receivers documented in
`DEBT_SALE_RECEIVER.md`; default instances still gate openings. Local before-commit
envelope freeze is still missing. The integration requirements below remain the
checklist for source integration; receiver coverage is in that document.

- Define and validate trusted product/warehouse/actor dependencies and local numeric
  conversion before any writes. Do not invent missing products or use current costs.
- Freeze exact source sale/items/stock operations and the envelope in the SAME local
  transaction as the opening event/outbox; compute the above SaleFingerprint before
  `OpenSale`. Old placeholder/arbitrary test fingerprints are not exportable financial
  envelopes. Do not silently retrofit or reconstruct immutable stock effects from
  today's inventory. Source adapters must capture original movement IDs and deltas.
- Extend the inbox's existing SAME writer boundary for sale/items/stock, ledger,
  full-body replay receipt and inbox completion. Internal component transaction
  participants already exist; do not call public Apply and then commit another receipt.
- Lift `WaitingForSaleAdapter` only after concrete frozen sale/stock validation and
  persistence are tested. Identical opening replay skips every effect; changed body
  or reused stock operation identity conflicts. Re-export preserves original bytes.
- Existing pending inbox handles known v1 missing dependencies. Unknown future format
  quarantine is not implemented. No ACK/cursor until the whole financial group commits.
- Add real SQLite/Room tests for opening rollback at every write boundary, concurrency,
  restart/replay, product/warehouse dependency ordering, original relay and local size/
  precision preflight. Current inbox tests explicitly use fixture opening dependencies;
  they do not prove concrete sale/items/stock import works.

Then 3A-2c adds authenticated capability/store negotiation, push/pull/ACK, old-peer
barriers, HELD-safe coalescing and debt-safe DB download. `canSync`/actor callbacks and
pure `RequirePeer` do not substitute for those integrations. Firebase/CBU unchanged.

## Verification

155 independent shared fixtures generated by `tests/debt/generate_envelope_fixtures.py`
use Python Decimal for reference totals/fees and independent framing/hashes. C# and
JVM Kotlin run the same corpus and compare every expected result. Android API26/API35
run the same runner/corpus; a Python test prevents drift between the copies. Coverage
includes Unicode, historical USD cost, fractional quantity, aggregate rounding,
amounts beyond 2^53, fee half-tiyin boundaries, overflow, altered snapshots, duplicate
item/operation IDs, stock mismatch, unsupported versions and exact item/byte limits.
The prior 97 arithmetic and 92 component fixtures remain enabled unchanged. Current
CI commit/run evidence is in `DEBT_CHECKPOINT_UZ.md`.
