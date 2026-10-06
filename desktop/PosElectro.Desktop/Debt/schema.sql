-- Debt ledger v1. Each delimiter block is one SQLite statement (API 26 compatible).
CREATE TABLE IF NOT EXISTS debt_schema (id INTEGER NOT NULL PRIMARY KEY CHECK(id=1), version INTEGER NOT NULL CHECK(version=1));
-- statement
CREATE TABLE IF NOT EXISTS debt_scope (
 id INTEGER NOT NULL PRIMARY KEY CHECK(id=1),
 store_guid TEXT NOT NULL UNIQUE CHECK(length(trim(store_guid)) BETWEEN 1 AND 128)
);
-- statement
CREATE TABLE IF NOT EXISTS debt_customers (
 guid TEXT NOT NULL PRIMARY KEY CHECK(length(trim(guid)) BETWEEN 1 AND 128),
 store_guid TEXT NOT NULL, name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 256),
 phone TEXT NOT NULL DEFAULT '', note TEXT NOT NULL DEFAULT '',
 archived INTEGER NOT NULL DEFAULT 0 CHECK(archived IN (0,1)),
 revision INTEGER NOT NULL DEFAULT 0 CHECK(typeof(revision)='integer' AND revision>=0),
 created_at INTEGER NOT NULL CHECK(typeof(created_at)='integer' AND created_at>=0),
 device_guid TEXT NOT NULL CHECK(length(trim(device_guid)) BETWEEN 1 AND 128),
 UNIQUE(guid,store_guid),
 FOREIGN KEY(store_guid) REFERENCES debt_scope(store_guid) ON DELETE RESTRICT
);
-- statement
CREATE INDEX IF NOT EXISTS debt_customers_phone ON debt_customers(store_guid,phone);
-- statement
CREATE TABLE IF NOT EXISTS debt_events (
 guid TEXT NOT NULL PRIMARY KEY CHECK(length(trim(guid)) BETWEEN 1 AND 128),
 request_guid TEXT NOT NULL UNIQUE CHECK(length(trim(request_guid)) BETWEEN 1 AND 128),
 schema_version INTEGER NOT NULL CHECK(schema_version=1),
 kind TEXT NOT NULL CHECK(kind IN ('sale_open','payment','return_offset','return_reversal','payment_reversal','credit_refund','credit_transfer')),
 customer_guid TEXT NOT NULL, store_guid TEXT NOT NULL,
 actor_guid TEXT NOT NULL CHECK(length(trim(actor_guid)) BETWEEN 1 AND 128),
 device_guid TEXT NOT NULL CHECK(length(trim(device_guid)) BETWEEN 1 AND 128),
 device_sequence INTEGER NOT NULL CHECK(typeof(device_sequence)='integer' AND device_sequence>0),
 occurred_at INTEGER NOT NULL CHECK(typeof(occurred_at)='integer' AND occurred_at>=0),
 payload TEXT NOT NULL CHECK(length(payload)>0),
 payload_hash TEXT NOT NULL CHECK(length(payload_hash)=64 AND payload_hash NOT GLOB '*[^0-9a-f]*'),
 cash_minor INTEGER NOT NULL CHECK(typeof(cash_minor)='integer' AND cash_minor>=-9223372036854775807),
 card_minor INTEGER NOT NULL CHECK(typeof(card_minor)='integer' AND card_minor>=-9223372036854775807),
 fee_minor INTEGER NOT NULL CHECK(typeof(fee_minor)='integer' AND fee_minor>=-9223372036854775807),
 fee_usd_rate TEXT, reference_guid TEXT,
 UNIQUE(guid,customer_guid,store_guid), UNIQUE(device_guid,device_sequence),
 FOREIGN KEY(customer_guid,store_guid) REFERENCES debt_customers(guid,store_guid) ON DELETE RESTRICT,
 FOREIGN KEY(reference_guid,customer_guid,store_guid) REFERENCES debt_events(guid,customer_guid,store_guid) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED
);
-- statement
CREATE INDEX IF NOT EXISTS debt_events_customer_date ON debt_events(customer_guid,occurred_at,guid);
-- statement
CREATE INDEX IF NOT EXISTS debt_events_reference ON debt_events(reference_guid);
-- statement
CREATE UNIQUE INDEX IF NOT EXISTS debt_events_one_reversal ON debt_events(reference_guid) WHERE kind IN ('payment_reversal','return_reversal');
-- statement
CREATE TABLE IF NOT EXISTS debt_accounts (
 guid TEXT NOT NULL PRIMARY KEY CHECK(length(trim(guid)) BETWEEN 1 AND 128),
 sale_guid TEXT NOT NULL UNIQUE, customer_guid TEXT NOT NULL, store_guid TEXT NOT NULL,
 opening_event_guid TEXT NOT NULL UNIQUE,
 original_debt_minor INTEGER NOT NULL CHECK(typeof(original_debt_minor)='integer' AND original_debt_minor>0),
 due_date TEXT, customer_name_at_sale TEXT NOT NULL CHECK(length(trim(customer_name_at_sale))>0),
 UNIQUE(guid,customer_guid,store_guid),
 FOREIGN KEY(sale_guid) REFERENCES sales(guid) ON DELETE RESTRICT,
 FOREIGN KEY(customer_guid,store_guid) REFERENCES debt_customers(guid,store_guid) ON DELETE RESTRICT,
 FOREIGN KEY(opening_event_guid,customer_guid,store_guid) REFERENCES debt_events(guid,customer_guid,store_guid) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED
);
-- statement
CREATE INDEX IF NOT EXISTS debt_accounts_customer_due ON debt_accounts(customer_guid,due_date);
-- statement
CREATE TABLE IF NOT EXISTS debt_event_lines (
 event_guid TEXT NOT NULL, line_index INTEGER NOT NULL CHECK(typeof(line_index)='integer' AND line_index>=0),
 account_guid TEXT NOT NULL, customer_guid TEXT NOT NULL, store_guid TEXT NOT NULL,
 debt_delta_minor INTEGER NOT NULL CHECK(typeof(debt_delta_minor)='integer' AND debt_delta_minor<>0 AND debt_delta_minor>=-9223372036854775807),
 PRIMARY KEY(event_guid,line_index), UNIQUE(event_guid,account_guid),
 FOREIGN KEY(event_guid,customer_guid,store_guid) REFERENCES debt_events(guid,customer_guid,store_guid) ON DELETE RESTRICT,
 FOREIGN KEY(account_guid,customer_guid,store_guid) REFERENCES debt_accounts(guid,customer_guid,store_guid) ON DELETE RESTRICT
);
-- statement
CREATE INDEX IF NOT EXISTS debt_lines_account ON debt_event_lines(account_guid);
-- statement
CREATE TABLE IF NOT EXISTS debt_command_receipts (
 request_guid TEXT NOT NULL PRIMARY KEY CHECK(length(trim(request_guid)) BETWEEN 1 AND 128),
 payload_hash TEXT NOT NULL CHECK(length(payload_hash)=64 AND payload_hash NOT GLOB '*[^0-9a-f]*'),
 event_guid TEXT NOT NULL UNIQUE, result TEXT NOT NULL CHECK(length(result)>0),
 FOREIGN KEY(event_guid) REFERENCES debt_events(guid) ON DELETE RESTRICT
);
-- statement
CREATE TABLE IF NOT EXISTS debt_sync_inbox (
 packet_guid TEXT NOT NULL PRIMARY KEY CHECK(length(trim(packet_guid)) BETWEEN 1 AND 128),
 store_guid TEXT NOT NULL, payload TEXT NOT NULL CHECK(length(payload)>0),
 received_at INTEGER NOT NULL CHECK(typeof(received_at)='integer' AND received_at>=0),
 error TEXT NOT NULL DEFAULT '',
 FOREIGN KEY(store_guid) REFERENCES debt_scope(store_guid) ON DELETE RESTRICT
);
-- statement
CREATE TRIGGER IF NOT EXISTS debt_events_no_update BEFORE UPDATE ON debt_events BEGIN SELECT RAISE(ABORT,'Immutable debt event'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_events_no_delete BEFORE DELETE ON debt_events BEGIN SELECT RAISE(ABORT,'Immutable debt event'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_accounts_no_update BEFORE UPDATE ON debt_accounts BEGIN SELECT RAISE(ABORT,'Immutable debt account'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_accounts_no_delete BEFORE DELETE ON debt_accounts BEGIN SELECT RAISE(ABORT,'Immutable debt account'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_lines_no_update BEFORE UPDATE ON debt_event_lines BEGIN SELECT RAISE(ABORT,'Immutable debt line'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_lines_no_delete BEFORE DELETE ON debt_event_lines BEGIN SELECT RAISE(ABORT,'Immutable debt line'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_receipts_no_update BEFORE UPDATE ON debt_command_receipts BEGIN SELECT RAISE(ABORT,'Immutable debt receipt'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_receipts_no_delete BEFORE DELETE ON debt_command_receipts BEGIN SELECT RAISE(ABORT,'Immutable debt receipt'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_scope_no_update BEFORE UPDATE ON debt_scope BEGIN SELECT RAISE(ABORT,'Store rebinding requires reconciliation'); END;
-- statement
CREATE TRIGGER IF NOT EXISTS debt_scope_no_delete BEFORE DELETE ON debt_scope BEGIN SELECT RAISE(ABORT,'Store rebinding requires reconciliation'); END;
-- statement
INSERT OR IGNORE INTO debt_schema(id,version) VALUES(1,1);
