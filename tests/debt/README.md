# Debt core contract tests (stage 2A)

The runners compile the actual C# and Kotlin production source files. The shared
`fixtures.json` contains expected results, not a second implementation of the formulas.
97 cases cover exact minor units, invalid input, overflow, sale remainder,
oldest-first / explicit receipt allocations, separate receivable and credit,
offline overcollection, arrival-order independence, partial returns, payment fee
reversal, credit transfer and refunds. Identifiers in fixtures are deliberately
short; persistent boundaries will use canonical GUIDs.

Run with .NET 8, JDK 17, Kotlin 2.0.21 and the test-only org.json 20240303 jar:

```sh
dotnet run --project tests/Debt.CoreTests -- tests/debt/fixtures.json /tmp/debt-csharp.json
kotlinc app/src/main/java/uz/pos/electro/data/debt/DebtAccounting.kt app/src/main/java/uz/pos/electro/data/debt/DebtWire.kt tests/debt/KotlinRunner.kt tests/debt/WireRunner.kt -cp /path/to/json.jar -include-runtime -d /tmp/debt-tests.jar
java -cp /tmp/debt-tests.jar:/path/to/json.jar KotlinRunnerKt tests/debt/fixtures.json /tmp/debt-kotlin.json
python tests/debt/verify_results.py tests/debt/fixtures.json /tmp/debt-csharp.json /tmp/debt-kotlin.json
```

On Windows, use `;` between Java classpath entries. The `Debt accounting core`
GitHub workflow installs checksum-verified test dependencies and runs these commands.
There are no new Android runtime dependencies.

## Integration boundaries

- Amounts are signed minor units (100 tiyin per UZS), range `-Long.MAX_VALUE`
  through `Long.MAX_VALUE`. The minimum signed Int64 is excluded so negation is safe.
- `parseUzs` accepts canonical ASCII decimal input, no grouping/sign/exponent or
  excess fractional digits. The UI must explicitly normalize displayed separators.
  It never converts via Double or rounds a user's input silently.
- `balance` uses a wide accumulator so valid final balances don't depend on event
  arrival order. The caller supplies deduplicated event lines; this is not a sync engine.
- `allocate` rejects known excess. Applying valid delayed payments to `balance`
  may produce credit. Do not rerun allocation against a later balance on replay.
- `payment` records full cash/card received and a separate actual fee expense;
  all debt effects have zero new sales revenue. Fees do not reduce debt repayment.
- `reversePayment` reverses the original allocation and cash/card; fee reversal is
  an explicit value bounded by the original fee. Unrefunded fees remain expenses.
- `splitReturn` only splits an already validated return value between debt offset
  and money refund. It does not validate item quantities or authorize a return.
- `transferCredit` and `refundCredit` validate arithmetic against supplied balances.
  A future transactional authority must re-read balances and authorize the operation.
- These classes do not create events, assign users/dates, check store ownership,
  save data, implement deduplication, or update reports. Those are stages 2B–7.
- Collection constructors return defensive immutable snapshots. Persisted input
  identifiers/customer ownership and canonical payload hashes belong to the repository.

## Verification so far

Both cores compiled locally; each passed all 97 fixtures and the cross-language
comparison. The sandbox's `dotnet run` launcher fails in `Process.GetStat`, so the
local C# check used the .NET 8.0.408 Roslyn compiler directly against net8.0 reference
assemblies (nullable checks and warnings-as-errors), then ran the resulting assembly
on .NET 8.0.15. CI uses the normal project command above.

No database, LAN, UI, Windows application or Android application integration has
been tested in this stage. The full D01–D27 acceptance matrix is not yet complete.

GitHub Actions also passed with the normal project build: [run 37285212424](https://github.com/akkiS18/LinePOS/actions/runs/37285212424), tested code commit `62150310d30a6fe27020194758eba86b76eb3fa6`. Both implementations passed 97/97 and the parity comparison.

## Stage 3A-1 wire corpus

The runner now also supports two optional trailing arguments: wire fixture input and
wire result output. Compile both core sources and both Kotlin runners as above.
See [DEBT_WIRE.md](../../docs/DEBT_WIRE.md) for the 92-vector component contract and
its explicit transport/authorization boundaries. Current verification is recorded in
[DEBT_CHECKPOINT_UZ.md](../../docs/DEBT_CHECKPOINT_UZ.md).
