# Debt wire components — stage 3A-1

Production sources: C# `Debt/DebtWire.cs`, Kotlin `data/debt/DebtWire.kt`.
This is a pure codec/validator, with no DB writes or network call sites. It does not
release HELD journal entries or advertise `debtLedgerV1` in the running application.
The complete financial transport remains stage 3A-2 (split further if necessary).

## Canonical frame

A component is a compact JSON array: a literal tag followed by separately base64
encoded, strict UTF-8 string fields. There is no whitespace or optional JSON escaping.
Tags are `debt-customer-v1` and `debt-event-v1`. Canonical representation is deliberate:
integer and decimal text never pass through a JSON floating-point reader. Decoding
requires exact base64 and JSON framing; roundtrip is byte-identical on C#/Kotlin.

All fields are strings before base64 encoding. Integers use `0` or an optional minus
followed by digits without leading zeroes; no `+`, exponent, whitespace or fractions.
The symmetric signed Int64 range excludes MIN_VALUE. UUIDs are nonzero, lowercase,
hyphenated canonical UUIDs. Optional fields are empty strings, never JSON null.
The packet tag is the schema version; unknown tags and event kinds fail closed.

Limits: 2 MiB of ASCII frame text, 10,000 event lines, 16,384 characters of inner command.
These are codec bounds, **not yet local command limits**. Before enabling UI/transport,
stage 3A-2 must preflight the complete financial envelope before a local commit, or add
an atomic fragmentation protocol. Never leave an already committed event permanently
unsendable. Existing HTTP body limit is 8 MiB; envelope overhead/sale/items/stock and
batch limits must be included in that preflight.

## Customer creation component

Ordered decoded fields: `guid, storeGuid, deviceGuid, payload, payloadHash`.

`payload` is the original repository `debt-command-v1` snapshot, not current mutable
contact data. It contains `customer, store, actor, guid, name, phone, note, createdAt`.
The codec validates identity binding, lengths/control characters, creation timestamp
and lowercase SHA-256. Device is the original creation epoch. Current contact revision,
archive flag and contact edits need a separate stage 3B conflict protocol; do not
silently replace the frozen create request with a renamed customer's current row.

## Financial event component

Ordered decoded fields (zero-based positions):

| Positions | Fields |
|---|---|
| 0–6 | guid, requestGuid, kind, customerGuid, storeGuid, actorGuid, deviceGuid |
| 7–10 | deviceSequence, occurredAt, original command payload, payloadHash |
| 11–14 | cashMinor, cardMinor, feeMinor, optional feeUsdRate |
| 15–20 | account GUID, sale GUID, opening event GUID, originalDebtMinor, optional dueDate, historical customer name |
| 21 | line count |
| 22 onward | repeated accountGuid, signed debtDeltaMinor pairs, in frozen line-index order |

Account fields are all empty for a payment. A sale opening has one account and zero
lines. Customer/store ownership of the opening account is inherited from the header.
Current supported kinds are only `sale_open` and `payment`; return/reversal/refund/
transfer codecs belong to their integration stage. Reference GUID is consequently
not a field in these two message kinds; never discard an unsupported referenced event.

The original repository command must agree with header request/customer/store/actor,
time and money. Opening account must agree with sale, opening event, due date and
`total - initial cash - initial card > 0`. Opening header money is zero so initial
sale tender and initial debt are not counted twice. Historical name is preserved.

Payment lines must have distinct account UUIDs, strictly negative deltas, and sum to
`-(cash + card)`. Fee is nonnegative and no greater than card amount. Fee FX snapshot
is either absent or a positive bounded decimal string. A targeted command may only
contain its target account. Arbitrary-precision intermediate sums reject overflow
without platform-dependent wrapping.

The sender's line order and allocation are preserved. The future DB exporter must
verify contiguous stored line indices `0..n-1` before encoding; do not silently repair
gaps by renumbering an incomplete event. Validation does **not** consult
receiver balances or re-run oldest-debt allocation; a delayed valid payment may produce
customer credit after merging concurrent offline payments.

## Integrity, replay and authorization boundaries

`Fingerprint(canonicalWire)` is lowercase SHA-256 of the **entire** frozen component,
including device/sequence, opening account and allocation lines. The old repository
payload hash covers only the local command and is insufficient for wire replay checks.
This hash detects changed content; it is not a MAC, signature, or proof of permission.

`RequirePeer` validates matching canonical store UUID and exact `debtLedgerV1`
capability. It is a helper, not a completed handshake or authentication mechanism.
Expected store must come from trusted local binding/pairing, never from the untrusted
packet itself. Existing paired bearer authentication and permission/actor mapping are
still required. Distinguish stable store identity from server/restore epoch.

Receiver integration must, in one writer transaction:

1. Authenticate and validate store/capability, then decode the complete envelope.
2. Compare request/event GUID and full frozen component hash/content for durable replay.
   Same GUID with different lines, order, sequence or account is an integrity conflict.
3. Reject reuse of customer-creation request IDs by financial events. Check each account belongs to the header customer/store against existing DB or
   validated dependencies. Codec-valid UUIDs do not prove account ownership/existence.
4. Validate associated sale/items/stock and basket fingerprint. This component carries
   only the debt side and cannot prove the sale callback/item effects correct.
5. Apply header, account and frozen lines without recalculating allocation; preserve
   already accepted offline events even if contact is now archived.
6. Persist group/dedup receipt and cursor atomically, then ACK. Missing dependencies or
   unsupported kinds/schema need durable inbox/error handling and **no false ACK**.

Decoded line collections are read-only snapshots. There is no mutable parser-owned
list that can silently change between validation and subsequent serialization.

## Existing transport audit and next work

| Current path | Required stage 3A-2 work |
|---|---|
| `LocalSyncServer.Pair`, authenticated `/api/ping` | Trusted store binding and negotiated capability; don't advertise early |
| `/api/v2/push` → `WifiSyncStore.Push` | Whole financial envelope, dependency checks, full-content dedup, durable ACK |
| `/api/v2/pull` → `WifiSyncStore.Pull` | Full and delta paths currently read sales independently of HELD journal; guard old peer and transfer a consistent complete ledger snapshot/cursor |
| Android `LocalSyncManager.freeze/pending` | Current `acked=0` selection excludes HELD; old freezer expects object payloads, while local debt journal holds command arrays; materialize/freeze a full envelope before releasing anything |
| Android metadata coalescing/batch selection | Must not delete or split metadata/stock dependencies of a held financial group; include whole-group size preflight |
| `/api/sync/download_db` | Currently strips sync tables including `sync_meta`, which contains customer creation retry snapshots. Define debt-safe export/restore identity and preserve required receipts/metadata before enabling debt |
| Old write endpoints | Currently return 410 after authentication; retain this refusal, do not reintroduce a bypass |

Ordinary backup snapshots and the stripped desktop download endpoint have different
semantics. Do not treat the existing download as a safe debt restore just because it
contains the debt SQL tables. Full restore epoch/reconciliation is stage 3B/7 work.

## Verification

92 independent shared vectors, generated by `tests/debt/generate_wire_fixtures.py`,
cover real repository command payloads, Unicode, amounts beyond 2^53/at Int64 max,
full and split payments, frozen order, peer mismatch, malformed framing/base64/UTF-8,
unknown version/kind, missing/extra lines, duplicate accounts, sum/fee/identity/date
errors and size bounds. C# and JVM Kotlin compare against the same expected wire hash
and each other. Android API26/API35 run the same corpus against the real runtime.
`tests/test_debt_wire_fixtures.py` prevents Android corpus drift. Existing arithmetic,
repository, schema and Wi-Fi regression suites remain enabled.

These are component/codec tests, not completed receiver, ACK-loss, two-device sync,
restored-database reconciliation or physical cashier UI tests.

To include the wire corpus in the standalone runners, append
`tests/debt/wire-fixtures.json /tmp/csharp-wire.json` to the .NET runner invocation,
and `tests/debt/wire-fixtures.json /tmp/kotlin-wire.json` to the Java invocation in
`tests/debt/README.md`. Compare with:

```sh
python tests/debt/verify_results.py tests/debt/wire-fixtures.json /tmp/csharp-wire.json /tmp/kotlin-wire.json
```
