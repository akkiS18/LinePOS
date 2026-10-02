"""Run the exact shipped SQLite capture schema, without Android or mocked SQL."""
from pathlib import Path
import sqlite3
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = (ROOT / 'sync/schema.sql').read_text()

class CaptureSchemaTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(':memory:')
        self.db.executescript('''
CREATE TABLE products(guid TEXT PRIMARY KEY,barcode TEXT,name TEXT,category TEXT,cost_price REAL,cost_currency TEXT,selling_price REAL,selling_price_2 REAL,unit_type TEXT,min_stock_alert REAL,is_deleted INTEGER,note TEXT,stock_quantity REAL,updated_at INTEGER);
CREATE TABLE warehouses(guid TEXT PRIMARY KEY,name TEXT,is_primary INTEGER,is_deleted INTEGER);
CREATE TABLE sales(guid TEXT PRIMARY KEY);
CREATE TABLE product_stocks(product_guid TEXT,warehouse_guid TEXT,quantity REAL,updated_at INTEGER,UNIQUE(product_guid,warehouse_guid));
INSERT INTO products(guid,name,stock_quantity) VALUES('p','Кабель',10);
INSERT INTO product_stocks VALUES('p','w',10,1);
''')
        for statement in SCHEMA.split('-- statement'):
            self.db.executescript(statement)
        self.db.commit()
    def tearDown(self):
        self.db.close()
    def test_schema_copies_match(self):
        for name in ['app/src/main/assets/wifi-sync-schema.sql','desktop/PosElectro.Desktop/Sync/schema.sql']:
            self.assertEqual(SCHEMA,(ROOT/name).read_text())
    def test_stock_adjustment_captures_difference(self):
        self.db.execute('UPDATE product_stocks SET quantity=8 WHERE product_guid="p"')
        self.assertEqual(-2,self.db.execute("SELECT SUM(delta) FROM sync_journal WHERE kind='stock'").fetchone()[0])
    def test_sale_stock_and_outbox_rollback_together(self):
        self.db.execute('UPDATE product_stocks SET quantity=quantity-2')
        self.db.execute("INSERT INTO sales VALUES('sale-1')")
        self.db.rollback()
        self.assertEqual(10,self.db.execute('SELECT quantity FROM product_stocks').fetchone()[0])
        self.assertEqual(0,self.db.execute('SELECT COUNT(*) FROM sync_journal').fetchone()[0])
        self.assertEqual(0,self.db.execute('SELECT COUNT(*) FROM sales').fetchone()[0])
    def test_stock_only_update_does_not_capture_metadata(self):
        self.db.execute('UPDATE products SET stock_quantity=8,updated_at=2')
        self.assertEqual(0,self.db.execute('SELECT COUNT(*) FROM sync_journal').fetchone()[0])
    def test_remote_snapshot_does_not_echo(self):
        self.db.execute('UPDATE sync_control SET applying=1')
        self.db.execute('UPDATE product_stocks SET quantity=7')
        self.db.execute("UPDATE products SET name='Yangi' ")
        self.db.execute('UPDATE sync_control SET applying=0')
        self.assertEqual(0,self.db.execute('SELECT COUNT(*) FROM sync_journal').fetchone()[0])
    def test_transfer_has_single_group_and_zero_net_quantity(self):
        self.db.execute("UPDATE sync_control SET current_group='transfer-1'")
        self.db.execute('UPDATE product_stocks SET quantity=8')
        self.db.execute("INSERT INTO product_stocks VALUES('p','w2',2,2)")
        self.assertEqual((1,0),self.db.execute('SELECT COUNT(DISTINCT group_id),SUM(delta) FROM sync_journal').fetchone())
    def test_revision_is_captured_without_phone_clock(self):
        self.db.execute("INSERT INTO sync_versions VALUES('product','p',42)")
        self.db.execute("UPDATE products SET name='Oʻtkazgich' ")
        self.assertEqual(42,self.db.execute("SELECT base_revision FROM sync_journal").fetchone()[0])

if __name__=='__main__': unittest.main()
