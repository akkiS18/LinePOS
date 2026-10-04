-- Split only at this marker; trigger bodies contain semicolons.
CREATE TABLE IF NOT EXISTS sync_control (id INTEGER PRIMARY KEY CHECK(id=1), applying INTEGER NOT NULL DEFAULT 0, current_group TEXT NOT NULL DEFAULT '');
-- statement
INSERT OR IGNORE INTO sync_control(id,applying) VALUES(1,0);
-- statement
CREATE TABLE IF NOT EXISTS sync_journal (seq INTEGER PRIMARY KEY AUTOINCREMENT, op_id TEXT NOT NULL UNIQUE, kind TEXT NOT NULL, entity_guid TEXT NOT NULL, warehouse_guid TEXT NOT NULL DEFAULT '', delta REAL NOT NULL DEFAULT 0, base_revision INTEGER NOT NULL DEFAULT 0, payload TEXT, group_id TEXT NOT NULL DEFAULT '', acked INTEGER NOT NULL DEFAULT 0);
-- statement
CREATE TABLE IF NOT EXISTS sync_versions (kind TEXT NOT NULL, entity_guid TEXT NOT NULL, revision INTEGER NOT NULL, PRIMARY KEY(kind,entity_guid));
-- statement
CREATE TABLE IF NOT EXISTS sync_meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
-- statement
CREATE TABLE IF NOT EXISTS sync_conflicts (kind TEXT NOT NULL, entity_guid TEXT NOT NULL, revision INTEGER NOT NULL, message TEXT NOT NULL, PRIMARY KEY(kind,entity_guid));
-- statement
CREATE TRIGGER IF NOT EXISTS sync_products_insert AFTER INSERT ON products WHEN (SELECT applying FROM sync_control WHERE id=1)=0 BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,base_revision,group_id) VALUES(lower(hex(randomblob(16))),'product',NEW.guid,COALESCE((SELECT revision FROM sync_versions WHERE kind='product' AND entity_guid=NEW.guid),0),(SELECT current_group FROM sync_control WHERE id=1)); END;
-- statement
CREATE TRIGGER IF NOT EXISTS sync_products_update AFTER UPDATE ON products WHEN (SELECT applying FROM sync_control WHERE id=1)=0 AND (NEW.barcode IS NOT OLD.barcode OR NEW.name IS NOT OLD.name OR NEW.category IS NOT OLD.category OR NEW.cost_price IS NOT OLD.cost_price OR NEW.cost_currency IS NOT OLD.cost_currency OR NEW.selling_price IS NOT OLD.selling_price OR NEW.selling_price_2 IS NOT OLD.selling_price_2 OR NEW.unit_type IS NOT OLD.unit_type OR NEW.min_stock_alert IS NOT OLD.min_stock_alert OR NEW.is_deleted IS NOT OLD.is_deleted OR NEW.note IS NOT OLD.note) BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,base_revision,group_id) VALUES(lower(hex(randomblob(16))),'product',NEW.guid,COALESCE((SELECT revision FROM sync_versions WHERE kind='product' AND entity_guid=NEW.guid),0),(SELECT current_group FROM sync_control WHERE id=1)); END;
-- statement
CREATE TRIGGER IF NOT EXISTS sync_warehouses_insert AFTER INSERT ON warehouses WHEN (SELECT applying FROM sync_control WHERE id=1)=0 BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,base_revision,group_id) VALUES(lower(hex(randomblob(16))),'warehouse',NEW.guid,COALESCE((SELECT revision FROM sync_versions WHERE kind='warehouse' AND entity_guid=NEW.guid),0),(SELECT current_group FROM sync_control WHERE id=1)); END;
-- statement
CREATE TRIGGER IF NOT EXISTS sync_warehouses_update AFTER UPDATE ON warehouses WHEN (SELECT applying FROM sync_control WHERE id=1)=0 AND (NEW.name IS NOT OLD.name OR NEW.is_primary IS NOT OLD.is_primary OR NEW.is_deleted IS NOT OLD.is_deleted) BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,base_revision,group_id) VALUES(lower(hex(randomblob(16))),'warehouse',NEW.guid,COALESCE((SELECT revision FROM sync_versions WHERE kind='warehouse' AND entity_guid=NEW.guid),0),(SELECT current_group FROM sync_control WHERE id=1)); END;
-- statement
CREATE TRIGGER IF NOT EXISTS sync_sales_insert AFTER INSERT ON sales WHEN (SELECT applying FROM sync_control WHERE id=1)=0 BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,base_revision,group_id) VALUES(lower(hex(randomblob(16))),'sale',NEW.guid,COALESCE((SELECT revision FROM sync_versions WHERE kind='sale' AND entity_guid=NEW.guid),0),(SELECT current_group FROM sync_control WHERE id=1)); END;
-- statement
CREATE TRIGGER IF NOT EXISTS sync_stocks_insert AFTER INSERT ON product_stocks WHEN (SELECT applying FROM sync_control WHERE id=1)=0 BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,warehouse_guid,delta,group_id) VALUES(lower(hex(randomblob(16))),'stock',NEW.product_guid,NEW.warehouse_guid,NEW.quantity,(SELECT current_group FROM sync_control WHERE id=1)); END;
-- statement
CREATE TRIGGER IF NOT EXISTS sync_stocks_update AFTER UPDATE ON product_stocks WHEN (SELECT applying FROM sync_control WHERE id=1)=0 AND NEW.quantity IS NOT OLD.quantity BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,warehouse_guid,delta,group_id) VALUES(lower(hex(randomblob(16))),'stock',NEW.product_guid,NEW.warehouse_guid,NEW.quantity-OLD.quantity,(SELECT current_group FROM sync_control WHERE id=1)); END;
-- statement
CREATE TRIGGER IF NOT EXISTS sync_stocks_delete AFTER DELETE ON product_stocks WHEN (SELECT applying FROM sync_control WHERE id=1)=0 BEGIN INSERT INTO sync_journal(op_id,kind,entity_guid,warehouse_guid,delta,group_id) VALUES(lower(hex(randomblob(16))),'stock',OLD.product_guid,OLD.warehouse_guid,-OLD.quantity,(SELECT current_group FROM sync_control WHERE id=1)); END;

-- statement
CREATE TABLE IF NOT EXISTS returns (
 guid TEXT PRIMARY KEY, sale_guid TEXT NOT NULL, created_at INTEGER NOT NULL,
 operator_guid TEXT NOT NULL, reason TEXT NOT NULL, status TEXT NOT NULL,
 cash_refund REAL NOT NULL, card_refund REAL NOT NULL, fee_reversal REAL NOT NULL,
 request_guid TEXT NOT NULL UNIQUE, authority_guid TEXT NOT NULL,
 request_hash TEXT NOT NULL, result_json TEXT NOT NULL);
-- statement
CREATE INDEX IF NOT EXISTS returns_sale ON returns(sale_guid);
-- statement
CREATE TABLE IF NOT EXISTS return_items (
 guid TEXT PRIMARY KEY, return_guid TEXT NOT NULL REFERENCES returns(guid), sale_item_guid TEXT NOT NULL,
 product_guid TEXT NOT NULL, quantity REAL NOT NULL, refund_amount_uzs REAL NOT NULL,
 cost_basis_uzs REAL NOT NULL, cost_reversal_uzs REAL NOT NULL, warehouse_guid TEXT NOT NULL,
 disposition TEXT NOT NULL, original_usd_rate REAL NOT NULL);
-- statement
CREATE INDEX IF NOT EXISTS returns_line ON return_items(sale_item_guid);
-- statement
CREATE TABLE IF NOT EXISTS return_quarantine (
 product_guid TEXT NOT NULL, warehouse_guid TEXT NOT NULL, quantity REAL NOT NULL,
 PRIMARY KEY(product_guid,warehouse_guid));
-- statement
CREATE TABLE IF NOT EXISTS return_drafts (sale_guid TEXT PRIMARY KEY, request_guid TEXT NOT NULL UNIQUE, authority_guid TEXT NOT NULL, payload TEXT NOT NULL, result TEXT, state TEXT NOT NULL);
