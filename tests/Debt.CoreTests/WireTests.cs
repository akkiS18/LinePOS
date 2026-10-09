using System.Text.Json.Nodes;
using PosElectro.Desktop.Debt;

static class WireTests
{
    public static void Run(string input,string output) {
        var fixtures=JsonNode.Parse(File.ReadAllText(input))!.AsArray();var results=new JsonObject();
        foreach(var item in fixtures) {
            var f=item!;string store=f["store"]!.GetValue<string>();JsonNode result;
            try {
                string kind=f["op"]!.GetValue<string>();
                if(kind=="peer") {
                    DebtWire.RequirePeer(store,f["peer"]!.GetValue<string>(),f["capabilities"]!.AsArray().Select(c=>c!.GetValue<string>()));
                    result=new JsonObject { ["accepted"]=true };
                } else {
                    string wire=kind=="oversize"?new string('x',DebtWire.MaxPacketChars+1):f["wire"]!.GetValue<string>();
                    string encoded;
                    if(kind=="customer")encoded=DebtWire.EncodeCustomer(DebtWire.DecodeCustomer(wire,store),store);
                    else {
                        var decoded=DebtWire.DecodeEvent(wire,store);encoded=DebtWire.EncodeEvent(decoded,store);
                        if(decoded.Lines.Count>0) {
                            bool blocked=false;
                            try { ((IList<DebtLine>)decoded.Lines)[0]=new DebtLine("tampered",1); }
                            catch(NotSupportedException){blocked=true;}
                            if(!blocked)throw new Exception("Decoded lines are mutable");
                        }
                    }
                    if(encoded!=wire)throw new Exception("Non-canonical roundtrip");
                    result=new JsonObject { ["hash"]=DebtWire.Fingerprint(encoded) };
                }
            } catch(ArgumentException) { result=new JsonObject{["error"]="invalid"}; }
            catch(OverflowException) { result=new JsonObject{["error"]="invalid"}; }
            if(!JsonNode.DeepEquals(result,f["expected"]))throw new Exception($"Wire {f["id"]}: expected {f["expected"]}, got {result}");
            results.Add(f["id"]!.GetValue<string>(),result);
        }
        File.WriteAllText(output,results.ToJsonString());Console.WriteLine($"C#: {fixtures.Count} shared wire fixtures passed");
    }
}
