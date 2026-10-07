"""Independent wire contract vectors: no production codec used to build expectations."""
import base64, copy, hashlib, json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
g=lambda n:f'00000000-0000-0000-0000-{n:012d}'
def pack(tag, fields):
    return json.dumps([tag]+[base64.b64encode(str(f).encode()).decode() for f in fields],separators=(',',':'))
def sha(s): return hashlib.sha256(s.encode()).hexdigest()
def command(fields): return pack('debt-command-v1',fields)
customer=['customer',g(1),g(2),g(3),'Ali Oʻgʻli','','','1']
sale=['sale_open',g(1),g(2),g(5),g(3),g(4),'0'*64,'10000','2000','0','1','2030-01-01']
payment=['payment',g(1),g(2),g(6),g(3),'3000','1000','20','2','','12000']
# Lock vectors to the already verified real repository output.
old=json.loads((ROOT/'debt/repository-fixtures.json').read_text())
for name,p in [('customer',customer),('sale_open',sale),('payment',payment)]:
    assert old[name]['payload']==command(p) and old[name]['hash']==sha(command(p))
def event(p):
    is_sale=p[0]=='sale_open'
    return [p[3],p[3],p[0],p[4],p[1],p[2],g(20),'1',p[10] if is_sale else p[8],command(p),sha(command(p)),
            '0' if is_sale else p[5],'0' if is_sale else p[6],'0' if is_sale else p[7],'' if is_sale else p[10]]+(
            [g(4),g(4),g(5),'8000',p[11],'Ali Oʻgʻli','0'] if is_sale else ['']*6+['1',g(4),'-4000'])
rows=[]
def add(name,fields=None,valid=False,kind='event',wire=None,store=None,**kwargs):
    if wire is None: wire=pack('debt-'+kind+'-v1',fields)
    rows.append(dict(id=name,op=kind,store=store or g(1),wire=wire,expected={'hash':sha(wire)} if valid else {'error':'invalid'},**kwargs))
def changed(fields,index,value):
    p=copy.deepcopy(fields);p[index]=value;return p
c=[g(3),g(1),g(20),command(customer),sha(command(customer))]
s=event(sale);p=event(payment)
add('customer',c,True,'customer');add('sale-open',s,True);add('payment',p,True)
q=copy.deepcopy(p);q[21:]=['2',g(4),'-1000',g(40),'-3000'];add('split-payment',q,True)
q[22:]=[g(40),'-3000',g(4),'-1000'];add('sender-order-preserved',q,True)
q=copy.deepcopy(s);q[20]='Ўзбек 👨‍👩‍👦';add('historical-unicode-name',q,True)
for name,cash,card,fee,rate in [('above-double-exact-range',9007199254740993,0,0,''),('max-int64',9223372036854775807,0,0,''),('fee-unknown-fx',0,4000,20,''),('fee-full-card',0,4000,4000,'0.00000001')]:
    cmd=payment.copy();cmd[5:8]=list(map(str,[cash,card,fee]));cmd[10]=rate
    q=event(cmd);q[-1]=str(-cash-card);add(name,q,True)
cmd=payment.copy();cmd[9]=g(4);add('target-account',event(cmd),True)
cmd=sale.copy();cmd[11]='';q=event(cmd);add('no-due-date',q,True)
for name,index,value in [('request-mismatch',1,g(50)),('customer-mismatch',3,g(50)),('store-mismatch',4,g(50)),('actor-mismatch',5,g(50)),('zero-device',6,g(0)),('upper-device',6,'abcdefab-abcd-abcd-abcd-abcdefabcdef'.upper()),('short-device',6,'1-1-1-1-1'),('zero-sequence',7,'0'),('negative-sequence',7,'-1'),('negative-time',8,'-1'),('changed-time',8,'3'),('changed-cash',11,'3001'),('changed-fee',13,'21'),('changed-fx',14,'13000'),('missing-lines',21,'0'),('negative-count',21,'-1'),('oversized-count',21,'10001'),('wrong-hash',10,'0'*64),('unknown-kind',2,'credit_refund'),('positive-line',23,'4000'),('zero-line',23,'0'),('sum-mismatch',23,'-3999'),('line-min-int64',23,'-9223372036854775808'),('wrong-account-format',22,'bad')]:
    add(name,changed(p,index,value))
for bad in ['01','+1','-0','1.0','1e0',' 1','1\n','9223372036854775808']:
    add('invalid-number-'+repr(bad),changed(p,7,bad))
q=p.copy();q[21:]=['2',g(4),'-2000',g(4),'-2000'];add('duplicate-account',q)
q=p.copy();q[21:]=['2',g(4),'-9223372036854775807',g(40),'-9223372036854775807'];add('delta-overflow',q)
q=event(payment);q[15:21]=s[15:21];add('payment-with-account',q)
q=p.copy();q[20]='hidden';add('partial-account',q)
for name,index,value in [('opening-double-count',11,'2000'),('bad-original',18,'8001'),('bad-opening-ref',17,g(6)),('account-sale-mismatch',16,g(40)),('empty-historical-name',20,''),('blank-historical-name',20,'\u0085'),('bad-due-date',19,'2030-02-30')]:
    add(name,changed(s,index,value))
q=s.copy();q[21:]=['1',g(4),'-1'];add('opening-with-lines',q)
for name,changes in [('negative-cash',{5:'-1'}),('fee-exceeds-card',{7:'1001'}),('invalid-rate',{10:'1e4'}),('zero-rate',{10:'0'}),('target-mismatch',{9:g(40)}),('zero-payment',{5:'0',6:'0',7:'0'}),('total-overflow',{5:str(2**63-1),6:'1'}),('fraction-money',{5:'3000.1'})]:
    cmd=payment.copy()
    for i,v in changes.items():cmd[i]=v
    add(name,event(cmd))
for date in ['0000-01-01','2030-02-30','2030-1-1','10000-01-01']:
    cmd=sale.copy();cmd[11]=date;add('invalid-date-'+date,event(cmd))
add('wrong-expected-store',p,store=g(99));add('unknown-schema',wire=pack('debt-event-v2',p))
add('extra-field',p+['extra']);add('truncated-line',p[:-1]);add('trailing-garbage',wire=pack('debt-event-v1',p)+'x')
add('whitespace-json',wire=pack('debt-event-v1',p).replace(',',', ',1));add('object-instead-array',wire='{}')
add('bad-utf8',wire=pack('debt-event-v1',p).replace('"MDAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDA2"','"/w=="',1))
add('unpadded-base64',wire=pack('debt-event-v1',p).replace('"MQ=="','"MQ"',1))
for name,index,value in [('customer-wrong-store',1,g(99)),('customer-zero-id',0,g(0)),('customer-bad-hash',4,'0'*64)]:
    add(name,changed(c,index,value),kind='customer')
for name,index,value in [('customer-empty-name',4,''),('customer-control',4,'Ali\n'),('customer-long-name',4,'x'*257),('customer-long-phone',5,'x'*65),('customer-long-note',6,'x'*2049),('customer-negative-time',7,'-1')]:
    cmd=customer.copy();cmd[index]=value;q=c.copy();q[3]=command(cmd);q[4]=sha(q[3]);add(name,q,kind='customer')
for name,peer,caps,valid in [('peer-ok',g(1),['other','debtLedgerV1'],True),('peer-old',g(1),[],False),('peer-wrong-shop',g(99),['debtLedgerV1'],False),('peer-wrong-version',g(1),['debtLedgerV2'],False),('peer-case',g(1),['debtledgerv1'],False)]:
    rows.append(dict(id=name,op='peer',store=g(1),peer=peer,capabilities=caps,expected={'accepted':True} if valid else {'error':'invalid'}))
rows.append(dict(id='oversized-frame',op='oversize',store=g(1),expected={'error':'invalid'}))
(ROOT/'tests/debt/wire-fixtures.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
print(f'{len(rows)} wire fixtures')
