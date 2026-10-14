"""Run against read-only backup sources; all writes go to in-memory copies."""
import hashlib
import sqlite3
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[1]
schemas = [root / p for p in ('sync/schema.sql', 'app/src/main/assets/wifi-sync-schema.sql', 'desktop/PosElectro.Desktop/Sync/schema.sql')]
assert len({p.read_bytes() for p in schemas}) == 1, 'Schema copies differ'
print('PASS three schema copies match')
for source in map(Path, sys.argv[1:]):
    digest = hashlib.sha256(source.read_bytes()).digest()
    with sqlite3.connect(source.resolve().as_uri() + '?mode=ro', uri=True) as original:
        db = sqlite3.connect(':memory:')
        original.backup(db)
    # Only stock capture definitions changed by this fix; install exact production SQL.
    chunks = schemas[0].read_text().split('-- statement')
    for chunk in chunks:
        if any(name in chunk for name in ('sync_stocks_insert', 'sync_stocks_delete')):
            db.execute(chunk)
    db.execute('UPDATE sync_control SET applying=0,current_group="" WHERE id=1')
    start = db.execute('SELECT COALESCE(MAX(seq),0) FROM sync_journal').fetchone()[0]
    db.execute("INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('zero-test-product','zero-test-warehouse',0,0)")
    db.execute("DELETE FROM product_stocks WHERE product_guid='zero-test-product'")
    assert db.execute('SELECT COUNT(*) FROM sync_journal WHERE seq>?', (start,)).fetchone()[0] == 0
    print('PASS', source.name.split('_')[0], 'zero insert/delete produces no journal entries')
    db.execute("INSERT INTO product_stocks(product_guid,warehouse_guid,quantity,updated_at) VALUES('zero-test-product','zero-test-warehouse',5,0)")
    db.execute("UPDATE product_stocks SET quantity=-2 WHERE product_guid='zero-test-product'")
    db.execute("DELETE FROM product_stocks WHERE product_guid='zero-test-product'")
    deltas = [r[0] for r in db.execute('SELECT delta FROM sync_journal WHERE seq>? ORDER BY seq', (start,))]
    assert deltas == [5, -7, 2], deltas
    print('PASS', source.name.split('_')[0], 'nonzero insert/update/delete and negative inventory preserved')
    db.close()
    assert hashlib.sha256(source.read_bytes()).digest() == digest
    print('PASS', source.name.split('_')[0], 'original backup unchanged')
