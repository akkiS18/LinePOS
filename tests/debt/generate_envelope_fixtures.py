"""Independent decimal/rational expectations for the frozen debt sale/envelope contract."""
import base64, copy, hashlib, json
from decimal import Decimal, localcontext, ROUND_HALF_UP
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
g=lambda n:f'00000000-0000-0000-0000-{n:012d}'
def pack(tag,fs):return json.dumps([tag]+[base64.b64encode(str(f).encode()).decode() for f in fs],separators=(',',':'))
def sha(s):return hashlib.sha256(s.encode()).hexdigest()
def cmd(fs):return pack('debt-command-v1',fs)
def minor(v):return str(int((v*100).quantize(Decimal(1),rounding=ROUND_HALF_UP)))
item=[g(10),g(11),'Sim Oʻzbek 👨‍👩‍👦','Elektr','metr',g(12),'Asosiy','2.5','40','20','UZS',g(13),'-2.5']
def sale(items=None,fx='12000',cash='2000',card='1000',rate='1.8'):
    items=copy.deepcopy(items if items is not None else [item])
    with localcontext() as ctx:
        ctx.prec=100
        total=sum(Decimal(i[7])*Decimal(i[8]) for i in items)
        cost=sum(Decimal(i[7])*Decimal(i[9])*(Decimal(fx) if i[10]=='USD' else Decimal(1)) for i in items)
        fee=(Decimal(card)*Decimal(rate)/100).quantize(Decimal(1),rounding=ROUND_HALF_UP)
    return [g(4),'1',minor(total),minor(cost),cash,card,str(fee),rate,fx,'DEBT',str(len(items))]+sum(items,[])
def wire(fs):return pack('debt-sale-v1',fs)
def event(sw,fs):
    p=['sale_open',g(1),g(2),g(5),g(3),fs[0],sha(sw),fs[2],fs[4],fs[5],fs[1],'2030-01-01']
    return [g(5),g(5),'sale_open',g(3),g(1),g(2),g(20),'1',fs[1],cmd(p),sha(cmd(p)),'0','0','0','',fs[0],fs[0],g(5),str(int(fs[2])-int(fs[4])-int(fs[5])),p[11],'Ali','0']
cp=['customer',g(1),g(2),g(3),'Ali','','','1']
cw=pack('debt-customer-v1',[g(3),g(1),g(20),cmd(cp),sha(cmd(cp))])
def envelope(fs,customer=cw):
    sw=wire(fs);return [g(5),g(1),customer,pack('debt-event-v1',event(sw,fs)),sw]
rows=[]
def add(name,fs=None,valid=False,op='sale',raw=None,store=None):
    w=raw if raw is not None else pack('debt-sale-v1' if op=='sale' else 'debt-envelope-v1',fs)
    rows.append(dict(id=name,op=op,store=store or g(1),wire=w,expected={'hash':sha(w)} if valid else {'error':'invalid'}))
def change(fs,index,value):
    q=fs.copy();q[index]=value;return q
s=sale();add('uzs-sale',s,True);add('full-opening',envelope(s),True,'envelope');add('existing-customer',envelope(s,''),True,'envelope')
add('customer-only',[g(3),g(1),cw,'',''],True,'envelope')
p=['payment',g(1),g(2),g(6),g(3),'3000','1000','20','2','','12000']
pw=pack('debt-event-v1',[g(6),g(6),'payment',g(3),g(1),g(2),g(20),'2','2',cmd(p),sha(cmd(p)),'3000','1000','20','12000']+['']*6+['1',g(4),'-4000'])
add('payment',[g(6),g(1),'',pw,''],True,'envelope');add('payment-with-customer',[g(6),g(1),cw,pw,''],True,'envelope')
usd=item.copy();usd[9]='0.001';usd[10]='USD';u=sale([usd]);add('historical-usd-cost',u,True);add('usd-full-opening',envelope(u),True,'envelope')
add('no-fx-needed',sale(fx='0'),True)
for name,quantity,price,cost in [('tiny-quantity','0.00000001','100000000','0'),('half-tiyin','1','0.005','0.005'),('below-half','1','0.00499999','0'),('above-half','1','0.00500001','0.00500001'),('large-precise','999999999999','90.07199255','0'),('fractional-cost','3.33333333','10.12345678','1.23456789')]:
    i=item.copy();i[7:10]=[quantity,price,cost];i[12]='-'+quantity
    q=sale([i],cash='0',card='0');add(name,q,name!='below-half');
    if name!='below-half': add(name+'-envelope',envelope(q),True,'envelope')
i=item.copy();i[7:10]=['1','0.004','0.004'];i[12]='-1';j=i.copy();j[0]=g(30);j[11]=g(31)
add('aggregate-before-rounding',sale([i,j],cash='0',card='0'),True)
j=item.copy();j[0]=g(30);j[11]=g(31);j[7]='1.25';j[12]='-1.25'
add('same-product-two-lines',sale([item,j]),True);add('frozen-order',sale([j,item]),True)
for card,rate in [('1','50'),('1','49.99999999'),('1','50.00000001'),('25','2'),('9999','100')]:add('fee-'+card+'-'+rate,sale(cash='0',card=card,rate=rate),True)
for name,idx,v in [('wrong-guid',0,g(0)),('negative-time',1,'-1'),('sum-mismatch',2,'10001'),('cost-mismatch',3,'5001'),('negative-cash',4,'-1'),('cash-over-total',4,'10000'),('card-over-total',5,'10000'),('fee-mismatch',6,'19'),('fee-over-card',6,'1001'),('negative-fee',6,'-1'),('fee-rate-over-100',7,'101'),('no-items',10,'0'),('too-many-items',10,'1001'),('negative-count',10,'-1'),('noninteger-count',10,'1.0'),('cash-sale',9,'CASH'),('return-sale',9,'RETURN')]:add(name,change(s,idx,v))
for idx,name in [(7,'fee'),(8,'fx'),(18,'quantity'),(19,'price'),(20,'cost')]:
    for v in ['01','-0','-1','1.0','1e2','NaN','Infinity','1.000000001','1000000000000','1\n','+1']:
        add(name+'-noncanonical-'+repr(v),change(s,idx,v))
for name,idx,v in [('zero-item-guid',11,g(0)),('bad-product-guid',12,'1-1-1-1-1'),('empty-name',13,''),('blank-name',13,'\u0085'),('name-control',13,'bad\nname'),('long-name',13,'x'*257),('long-category',14,'x'*257),('empty-unit',15,''),('long-unit',15,'x'*33),('bad-warehouse',16,g(0)),('empty-warehouse-name',17,''),('zero-quantity',18,'0'),('bad-currency',21,'EUR'),('bad-stock-op',22,g(0)),('positive-stock',23,'2.5'),('wrong-stock',23,'-2.4'),('noncanonical-stock',23,'-2.50')]:add(name,change(s,idx,v))
add('unknown-usd-fx',change(u,8,'0'))
add('duplicate-item',sale([item,item]))
j=item.copy();j[0]=g(30);add('duplicate-stock-op',sale([item,j]))
j=item.copy();j[0]=g(30);j[11]=g(31)
q=sale([item,j]);q[11+13]='bad';add('bad-second-item',q)
i=item.copy();i[7:10]=['999999999999','999999999999','999999999999'];i[12]='-'+i[7];q=sale([i],cash='0',card='0');q[2]=str(2**63-1);q[3]=str(2**63-1);add('aggregate-overflow',q)
add('truncated-item',s[:-1]);add('extra-item-field',s+['hidden']);add('sale-unknown-version',raw=wire(s).replace('debt-sale-v1','debt-sale-v2'))
e=envelope(s)
for name,idx,v in [('envelope-id-mismatch',0,g(99)),('envelope-zero-id',0,g(0)),('other-store',1,g(99)),('missing-opening-sale',4,''),('missing-opening-event',3,''),('missing-everything',2,'')]:
    q=change(e,idx,v)
    if name=='missing-everything':q[3:]=['','']
    add(name,q,op='envelope')
add('wrong-expected-store',e,op='envelope',store=g(99))
add('payment-smuggled-sale',[g(6),g(1),cw,pw,wire(s)],op='envelope')
add('customer-smuggled-sale',[g(3),g(1),cw,'',wire(s)],op='envelope')
cp2=cp.copy();cp2[3]=g(33);other=pack('debt-customer-v1',[g(33),g(1),g(20),cmd(cp2),sha(cmd(cp2))]);add('wrong-customer',change(e,2,other),op='envelope')
# Individually valid sale and event, but their binding no longer matches.
for name,idx,v in [('changed-sale-id',0,g(44)),('changed-sale-time',1,'2'),('changed-cash',4,'1000'),('changed-fee-rate',7,'0'),('changed-historical-name',13,'New name'),('changed-stock-op',22,g(55))]:
    q=change(s,idx,v)
    if idx==7:q[6]='0'
    add(name,change(e,4,wire(q)),op='envelope')
add('other-valid-basket',change(e,4,wire(u)),op='envelope')
for name,w in [('trailing-garbage',pack('debt-envelope-v1',e)+'x'),('unknown-version',pack('debt-envelope-v2',e)),('extra-field',pack('debt-envelope-v1',e+['hidden'])),('space',pack('debt-envelope-v1',e).replace(',',', ',1)),('bad-utf8','["debt-envelope-v1","/w=="]'),('bad-base64','["debt-envelope-v1","MQ"]'),('object','{}')]:add('frame-'+name,raw=w,op='envelope')
# Generated large cases avoid duplicating multi-MiB strings in git.
for op in ['oversize-sale','oversize-envelope']:rows.append(dict(id=op,op=op,store=g(1),expected={'error':'invalid'}))
for op in ['max-items','max-items-plus-one','oversize-unicode-sale']:
    rows.append(dict(id=op,op=op,store=g(1),wire=wire(s),expected={'accepted':True} if op=='max-items' else {'error':'invalid'}))
output=json.dumps(rows,ensure_ascii=False,indent=2)+'\n'
(ROOT/'tests/debt/envelope-fixtures.json').write_text(output)
(ROOT/'app/src/androidTest/assets/debt-envelope-fixtures.json').write_text(output)
print(f'{len(rows)} envelope fixtures')
