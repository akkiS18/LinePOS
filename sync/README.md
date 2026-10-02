# Wi-Fi sync V2

Wi-Fi sync connects Android directly to the Windows application on TCP 8080. It does
not use Firebase, CBU, an Internet API, or an external server. Licensing and exchange-rate
code are unchanged. Both applications must be upgraded together; V1 sync writes return 410.

## Inventory and delivery

* The desktop is the authority for reconciled stock. Devices keep selling offline.
* SQLite triggers capture catalog edits, receipts and **stock differences** in the same
  transaction as the business write. The mobile queue survives process/device restarts.
* Product edits never transmit an absolute stock overwrite. New shipments, adjustments,
  sales and transfers produce stock differences with random operation IDs.
* A sale's receipt and inventory changes share a group; a transfer's two sides share a
  group. Group members stay together when batches are split. The server commits an entire
  batch and its operation IDs in one transaction; any failure rolls everything back.
* The server acknowledges exact operation IDs. A lost response causes an identical retry;
  the unique journal ID prevents repeat deductions, including after server restart.
* The mobile client polls the local server every two seconds while its process is running.
  SSE and wall-clock cursors are no longer used. A consistent database snapshot and a
  monotonically increasing journal cursor cover reconnects and changes between requests.
* Incoming receipts do not deduct stock again. Received stock is authoritative, with any
  concurrently created, unpushed local differences retained. Cursor and received records
  commit together. Applying remote data suppresses outbound trigger capture.
* Catalog conflicts compare journal revisions, not phone clocks. The Wi-Fi dialog offers
  **Telefon tahriri / Kompyuter tahriri**. Stock operations are retained in either choice.

## First upgrade and connection

1. Back up both existing databases before upgrading. Upgrade the desktop and Android together.
2. Desktop: Wi-Fi page -> **Yangi ulanish kodi / QR**. Android: scan the QR, or enter
   its IP and the eight-digit code. The code expires after five minutes and is single-use.
3. The existing desktop stock is the starting authority for shared product GUIDs. Migration
   does not upload a phone's old absolute stock over it. Historical unacknowledged mobile
   receipts are reconciled by GUID and deducted only when absent on the desktop.
4. Products present only on the phone are imported with their initial warehouse quantities.
   Unsynced legacy receipts and newly queued differences are excluded from that baseline
   to prevent double-counting. Same barcode with different GUIDs is a migration conflict;
   sync stops with an explicit message instead of silently changing receipt identities.
5. A database already associated with a different desktop identity cannot automatically be
   combined with another store. Export/import planning is needed for that move.

Room version 10 -> 11 installs additive sync tables and triggers. Existing business tables
are preserved. Destructive migration fallback was removed: an unsupported schema fails
instead of clearing store data. The SQL uses API-26-compatible stock updates.

## Local permissions

Pairing issues a random per-device token; only its SHA-256 hash is stored on the desktop.
Every read/write/export requires the token. **Telefonlar ruxsatini bekor qilish** revokes all
issued tokens. V2 requests also carry the paired database identity; the server rejects a
missing or mismatched identity before applying writes. Firewall rules are limited to
Private networks and LocalSubnet. Android
accepts only local/link-local/loopback destinations and does not follow redirects.

HTTP header limits, an 8-MiB request-body limit, a 15-second request timeout and a 32-client
limit bound resource use. Request bodies are read as UTF-8 bytes, not character counts.

Tokens and traffic currently travel over plain HTTP. Use a trusted private shop LAN; this
is not protection from an attacker sniffing or controlling that LAN. Do not expose port
8080 to the Internet. A future local TLS/pinned-certificate layer would protect transport
without requiring an online server.

**Kompyuterdan tiklash** uses authenticated V2 synchronization into the existing Room
schema, preserving receipts and local user IDs. It no longer substitutes a desktop SQLite
file for an Android database. Desktop file export strips sync identities and tokens.

## Verification

Run the executable tests against the actual C# store and HTTP server:

```sh
dotnet run --project tests/WifiSync.CoreTests
```

Run actual Room/SQLite capture tests on Android 8 (API 26) and a current device:

```sh
bash gradlew :app:connectedDebugAndroidTest
```

Before release, test on Windows and two Android devices:

* Start with 10 units. Disconnect Wi-Fi; sell 2 on desktop and 3 on a phone. Reconnect:
  all devices must show 5, with both receipts present. Reconnect repeatedly.
* Interrupt a response after commit and resend the same batch. Stock changes once.
* Disconnect/kill the app during a sale or transfer; reopen and verify receipt + stock.
* Transfer two units between warehouses. Repeat delivery and restart both apps: each
  warehouse changes once, including the originating phone.
* Change the same price on two offline devices. Verify the conflict choice and that stock
  is preserved. Enter Cyrillic/Uzbek product names, notes and warehouse names.
* Test QR expiry/reuse, an unknown device, token revocation and denied legacy endpoints.
* Verify a migrated real database on a **copy**, including legacy receipts and tombstones.
* Check 10,000 products, a large receipt history and several phones for latency/memory.

### Validation performed in the implementation environment

* C# core and actual loopback HTTP server: 20 executable tests passed (see test source).
* Exact shipped SQLite trigger schema: 7 Python/SQLite tests passed.
* Windows GitHub Actions runner: full WPF Debug build passed after removing two invalid
  `CornerRadius` attributes from Buttons in `CashierView.xaml`; their Border templates
  retain the rounding.
* Android GitHub Actions runner: `:app:assembleDebug :app:assembleDebugAndroidTest` passed,
  including real Room/KSP, Hilt and Compose compilation (no stubs).
* The HTTP test also checks missing/wrong paired database identities: rejected writes
  leave inventory unchanged; a matching identity commits exactly once.
* Real Android Room/SQLite capture tests: all 4 passed on API 26, and all 4 passed on API 35.
  [GitHub Actions verification run](https://github.com/akkiS18/LinePOS/actions/runs/36781175813)
  includes both builds, the 27 core/schema tests and both emulator runs.
* Physical Windows/Android Wi-Fi, firewall, migrated shop databases and load tests remain
  separate release gates. No production APK/EXE was published.

### Operational limits

Sync is eventual, usually within the two-second foreground polling interval. Android
background execution/Doze and process termination can delay it until the app runs again;
queued business writes remain durable. Disconnected devices can oversell the last item;
negative stock is preserved rather than hiding real sales.

Journal retention is intentionally unbounded in this first V2 version: deleting entries
would invalidate client cursors and deduplication. Measure storage growth before rollout.
Restoring an older desktop database can invalidate cursors/operation history; sync fails
when the cursor is beyond the restored journal. Treat backup rollback as an explicit
recovery operation, not a live database swap. Load tests and Windows firewall behavior
must be checked on the production hardware.
