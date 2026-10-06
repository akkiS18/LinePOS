"""Execute the exact shipped debt schema on real SQLite; repository tests are a later stage."""
from pathlib import Path
import sqlite3
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = (ROOT / 'debt/schema.sql').read_text()

class DebtSchemaTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(':memory:')
        self.db.execute('PRAGMA foreign_keys=ON')
        self.db.execute('CREATE TABLE sales(guid TEXT PRIMARY KEY, total_amount REAL)')
        # Exact existing outbox definition, without installing unrelated product triggers.
        journal = next(s for s in (ROOT/'sync/schema.sql').read_text().split('-- statement') if 'CREATE TABLE IF NOT EXISTS sync_journal ' in s)
        self.db.execute(journal)
        self.install()
        self.db.execute("INSERT INTO debt_scope VALUES(1,'shop')")
        for c in ['c1','c2']:
            self.db.execute("INSERT INTO debt_customers(guid,store_guid,name,created_at,device_guid) VALUES(?,'shop',?,1,'device')",(c,c))
        self.db.commit()
    def tearDown(self): self.db.close()
    def install(self):
        for s in SCHEMA.split('-- statement'): self.db.execute(s)
    def event(self, guid='e1', customer='c1', kind='sale_open', ref=None, cash=0):
        seq=int(guid[1:])
        self.db.execute('''INSERT INTO debt_events(guid,request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,reference_guid)
VALUES(?,?,1,?,?,'shop','actor','device',?,1,'{}',?,?,0,0,?)''',(guid,'req-'+guid,kind,customer,seq,'0'*64,cash,ref))
    def account(self,guid='a1',customer='c1',opening='e1',amount=100):
        self.db.execute('INSERT INTO sales VALUES(?,123)',('sale-'+guid,))
        self.db.execute('''INSERT INTO debt_accounts(guid,sale_guid,customer_guid,store_guid,opening_event_guid,original_debt_minor,customer_name_at_sale)
VALUES(?,?,?,'shop',?,?,'Historic name')''',(guid,'sale-'+guid,customer,opening,amount))
    def line(self,event='e2',account='a1',customer='c1',delta=-40):
        self.db.execute("INSERT INTO debt_event_lines VALUES(?,0,?,?,'shop',?)",(event,account,customer,delta))
    def seed(self):
        self.event();self.account();self.event('e2',kind='payment',cash=40);self.line();self.db.commit()
    def test_schema_copies_match(self):
        for p in ['desktop/PosElectro.Desktop/Debt/schema.sql','app/src/main/assets/debt-schema.sql']:
            self.assertEqual(SCHEMA,(ROOT/p).read_text())
    def test_install_is_idempotent_and_does_not_create_debt(self):
        self.install();self.install()
        self.assertEqual(1,self.db.execute('SELECT version FROM debt_schema').fetchone()[0])
        self.assertEqual(0,self.db.execute('SELECT COUNT(*) FROM debt_events').fetchone()[0])
    def test_known_money_types_and_overflow_rejected(self):
        self.event()
        for i,amount in enumerate([0,-1,1.5,'9223372036854775808']):
            with self.assertRaises(sqlite3.IntegrityError):self.account('bad'+str(i),amount=amount)
        with self.assertRaises(sqlite3.IntegrityError): self.event('e2',cash=-9223372036854775808)
    def test_unknown_customer_and_store_rejected(self):
        with self.assertRaises(sqlite3.IntegrityError):self.event(customer='missing')
        with self.assertRaises(sqlite3.IntegrityError):
            self.db.execute("INSERT INTO debt_customers(guid,store_guid,name,created_at,device_guid) VALUES('c3','other','Name',1,'d')")
    def test_cross_customer_line_rejected(self):
        self.event();self.account();self.event('e2',customer='c2',kind='payment')
        with self.assertRaises(sqlite3.IntegrityError):self.line(customer='c2')
    def test_missing_sale_and_opening_event_rejected(self):
        self.event()
        with self.assertRaises(sqlite3.IntegrityError):
            self.db.execute("INSERT INTO debt_accounts VALUES('a','missing','c1','shop','e1',1,NULL,'Name')")
        self.account(opening='missing')
        with self.assertRaises(sqlite3.IntegrityError):self.db.commit()
        self.db.rollback()
    def test_request_and_device_sequence_unique(self):
        self.event()
        with self.assertRaises(sqlite3.IntegrityError):self.event()
        with self.assertRaises(sqlite3.IntegrityError):
            self.db.execute("INSERT INTO debt_events SELECT 'different',request_guid,schema_version,kind,customer_guid,store_guid,actor_guid,device_guid,device_sequence,occurred_at,payload,payload_hash,cash_minor,card_minor,fee_minor,fee_usd_rate,reference_guid FROM debt_events")
    def test_immutable_financial_history(self):
        self.seed()
        self.db.execute("INSERT INTO debt_command_receipts VALUES('req-e2',?,'e2','{}')",('0'*64,));self.db.commit()
        for table in ['debt_events','debt_accounts','debt_event_lines','debt_command_receipts','debt_scope']:
            column=self.db.execute(f'PRAGMA table_info({table})').fetchone()[1]
            for sql in [f'UPDATE {table} SET {column}={column}',f'DELETE FROM {table}']:
                with self.assertRaises(sqlite3.IntegrityError):self.db.execute(sql)
        self.assertEqual(1,self.db.execute('SELECT COUNT(*) FROM debt_event_lines').fetchone()[0])
    def test_single_reversal_per_original(self):
        self.event();self.event('e2',kind='payment');self.event('e3',kind='payment_reversal',ref='e2')
        with self.assertRaises(sqlite3.IntegrityError):self.event('e4',kind='payment_reversal',ref='e2')
    def test_no_cascade_delete_of_original_sale(self):
        self.event();self.account()
        with self.assertRaises(sqlite3.IntegrityError):self.db.execute('DELETE FROM sales')
    def test_rollback_includes_sale_account_event_line_and_outbox(self):
        self.event();self.account();self.event('e2',kind='payment');self.line()
        self.db.execute("INSERT INTO sync_journal(op_id,kind,entity_guid,payload,group_id) VALUES('op','debt_event','e2','{}','g')")
        self.db.rollback()
        for table in ['sales','debt_events','debt_accounts','debt_event_lines','sync_journal']:
            self.assertEqual(0,self.db.execute(f'SELECT COUNT(*) FROM {table}').fetchone()[0])
    def test_backup_preserves_payment_without_new_sale(self):
        self.seed()
        self.db.execute("INSERT INTO sync_journal(op_id,kind,entity_guid,payload) VALUES('op','debt_event','e2','{}')");self.db.commit()
        with sqlite3.connect(':memory:') as target:
            self.db.backup(target)
            self.assertEqual(-40,target.execute('SELECT debt_delta_minor FROM debt_event_lines').fetchone()[0])
            self.assertEqual(0,target.execute('SELECT acked FROM sync_journal').fetchone()[0])
            self.assertEqual([],target.execute('PRAGMA foreign_key_check').fetchall())
    def test_nonunique_phone_and_archive_preserve_history(self):
        self.seed();self.db.execute("UPDATE debt_customers SET phone='123',archived=1")
        self.assertEqual(2,self.db.execute("SELECT COUNT(*) FROM debt_customers WHERE phone='123'").fetchone()[0])
        self.assertEqual(2,self.db.execute('SELECT COUNT(*) FROM debt_events').fetchone()[0])
    def test_incomplete_migration_rolls_back_ddl(self):
        with sqlite3.connect(':memory:') as target:
            target.execute('BEGIN')
            try:
                for s in SCHEMA.split('-- statement'): target.execute(s)
                target.execute('INVALID SQL')
            except sqlite3.DatabaseError:target.rollback()
            self.assertEqual(0,target.execute("SELECT COUNT(*) FROM sqlite_master WHERE name GLOB 'debt_*'").fetchone()[0])

if __name__=='__main__':unittest.main()
