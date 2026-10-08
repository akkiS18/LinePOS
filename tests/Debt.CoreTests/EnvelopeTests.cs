using System.Text.Json.Nodes;
using PosElectro.Desktop.Debt;

static class EnvelopeTests
{
    public static void Run(string input,string output) {
        var fixtures=JsonNode.Parse(File.ReadAllText(input))!.AsArray();var results=new JsonObject();
        foreach(var item in fixtures) {
            var f=item!;var store=f["store"]!.GetValue<string>();JsonNode result;
            try {
                var op=f["op"]!.GetValue<string>();
                var wire=op.StartsWith("oversize-") && op!="oversize-unicode-sale"?new string('x',(op=="oversize-sale"?DebtWire.MaxPacketChars:DebtEnvelope.MaxEnvelopeChars)+1):f["wire"]!.GetValue<string>();
                string encoded;
                if(op=="max-items" || op=="max-items-plus-one" || op=="oversize-unicode-sale") {
                    var s=DebtEnvelope.DecodeSale(wire);int count=op=="max-items-plus-one"?1001:1000;
                    var items=Enumerable.Range(1,count).Select(n=>s.Items[0] with{Guid=$"00000000-0000-0000-0000-{n:D12}",StockOperationGuid=$"00000000-0000-0000-0001-{n:D12}",
                        ProductName=op=="oversize-unicode-sale"?new string('界',256):s.Items[0].ProductName,
                        Category=op=="oversize-unicode-sale"?new string('界',256):s.Items[0].Category,
                        WarehouseName=op=="oversize-unicode-sale"?new string('界',256):s.Items[0].WarehouseName}).ToArray();
                    encoded=DebtEnvelope.EncodeSale(s with{TotalMinor=s.TotalMinor*count,CostMinor=s.CostMinor*count,Items=items});
                    if(DebtEnvelope.DecodeSale(encoded).Items.Count!=count)throw new Exception("Lost items");
                    result=new JsonObject{["accepted"]=true};
                } else {
                    if(op=="sale" || op=="oversize-sale") {
                        var s=DebtEnvelope.DecodeSale(wire);encoded=DebtEnvelope.EncodeSale(s);
                        bool blocked=false;
                        try { ((IList<DebtSaleItem>)s.Items)[0]=s.Items[0] with{ProductName="tampered"}; }
                        catch(NotSupportedException){blocked=true;}
                        if(!blocked)throw new Exception("Mutable sale items");
                    } else encoded=DebtEnvelope.Encode(DebtEnvelope.Decode(wire,store),store);
                    if(encoded!=wire)throw new Exception("Noncanonical envelope roundtrip");
                    result=new JsonObject{["hash"]=DebtWire.Fingerprint(encoded)};
                }
            } catch(ArgumentException){result=new JsonObject{["error"]="invalid"};}
            catch(OverflowException){result=new JsonObject{["error"]="invalid"};}
            if(!JsonNode.DeepEquals(result,f["expected"]))throw new Exception($"Envelope {f["id"]}: expected {f["expected"]}, got {result}");
            results.Add(f["id"]!.GetValue<string>(),result);
        }
        File.WriteAllText(output,results.ToJsonString());Console.WriteLine($"C#: {fixtures.Count} shared envelope fixtures passed");
    }
}
