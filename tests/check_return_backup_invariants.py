"""Read-only checks of recorded returns; does not simulate new application operations."""
import hashlib
import sqlite3
import sys
from decimal import Decimal
from pathlib import Path

money = lambda x: Decimal(str(x)).quantize(Decimal('0.01'))
for path in map(Path, sys.argv[1:]):
    before = hashlib.sha256(path.read_bytes()).digest()
    db = sqlite3.connect(path.resolve().as_uri() + '?mode=ro', uri=True)
    db.row_factory = sqlite3.Row
    returns = db.execute('SELECT * FROM returns').fetchall()
    assert returns, 'No return fixtures'
    assert len({r['request_guid'] for r in returns}) == len(returns), 'Duplicate request'
    for r in returns:
        receipt = db.execute('SELECT * FROM sales WHERE guid=?', (r['guid'],)).fetchone()
        assert receipt is not None, 'Missing return receipt'
        assert db.execute('SELECT 1 FROM sales WHERE guid=?', (r['sale_guid'],)).fetchone(), 'Missing original sale'
        lines = db.execute('SELECT * FROM return_items WHERE return_guid=?', (r['guid'],)).fetchall()
        assert lines, 'Missing return items'
        assert money(receipt['total_amount']) == -sum(money(x['refund_amount_uzs']) for x in lines), 'Receipt/refund mismatch'
        assert money(receipt['total_cost']) == -sum(money(x['cost_reversal_uzs']) for x in lines), 'Cost reversal mismatch'
        assert money(receipt['cash_amount']) == -money(r['cash_refund']), 'Cash mismatch'
        assert money(receipt['card_amount']) == -money(r['card_refund']), 'Card mismatch'
        for x in lines:
            assert db.execute('SELECT 1 FROM sale_items WHERE guid=? AND sale_guid=?', (x['sale_item_guid'],r['sale_guid'])).fetchone(), 'Wrong original sale item'
        if r['status'].startswith('reversal:'):
            original = db.execute('SELECT * FROM returns WHERE guid=?', (r['status'].split(':',1)[1],)).fetchone()
            assert original is not None
            for key in ['cash_refund','card_refund','fee_reversal']:
                assert money(r[key]) == -money(original[key]), 'Reversal header mismatch'
            for x in lines:
                old = db.execute('SELECT * FROM return_items WHERE return_guid=? AND sale_item_guid=?', (original['guid'], x['sale_item_guid'])).fetchone()
                assert old is not None
                for key in ['quantity','refund_amount_uzs','cost_basis_uzs','cost_reversal_uzs']:
                    assert abs(Decimal(str(x[key]))+Decimal(str(old[key]))) < Decimal('0.000001'), 'Reversal item mismatch'
    for row in db.execute('SELECT i.guid,i.quantity,COALESCE(SUM(r.quantity),0) returned FROM sale_items i JOIN return_items r ON r.sale_item_guid=i.guid GROUP BY i.guid'):
        assert -0.000001 <= row['returned'] <= row['quantity'] + 0.000001, 'Returned quantity out of range'
    db.close()
    assert before == hashlib.sha256(path.read_bytes()).digest()
    print(f'PASS {path.name.split("_")[0]}: {len(returns)} recorded returns; receipt, cost, cash/card, source lines, reversal, quantity bounds, source unchanged')
