using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PosElectro.Desktop.Sync;
public sealed record WifiHttpRequest(string Method,string Target,Dictionary<string,string> Headers,string Body)
{
    public static async Task<WifiHttpRequest> Read(Stream stream,CancellationToken token)
    {
        using var header=new MemoryStream();var one=new byte[1];int terminator=0;
        while(terminator<4)
        {
            if(await stream.ReadAsync(one,token)!=1)throw new ArgumentException("HTTP sarlavhasi tugallanmagan.");
            header.WriteByte(one[0]);if(header.Length>16384)throw new ArgumentException("HTTP sarlavhasi juda katta.");
            terminator=(terminator,one[0]) switch{(0,13)=>1,(1,10)=>2,(2,13)=>3,(3,10)=>4,(_,13)=>1,_=>0};
        }
        var lines=Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");var first=lines[0].Split(' ');
        if(first.Length!=3||!first[1].StartsWith('/')||first[1].StartsWith("//"))throw new ArgumentException("HTTP manzili noto'g'ri.");
        var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in lines[1..]) { if(line.Length==0)continue;var parts=line.Split(':',2);if(parts.Length!=2||!headers.TryAdd(parts[0].Trim(),parts[1].Trim()))throw new ArgumentException("HTTP sarlavhasi noto'g'ri yoki takrorlangan."); }
        if(headers.ContainsKey("Transfer-Encoding"))throw new ArgumentException("Chunked so'rovlar qo'llanmaydi; Content-Length yuboring.");
        int length=0;if(headers.TryGetValue("Content-Length",out var value)&&(!int.TryParse(value,out length)||length<0||length>8*1024*1024))throw new ArgumentException("So'rov hajmi 8 MB dan oshmasligi kerak.");
        var body=new byte[length];int offset=0;
        while(offset<length){int read=await stream.ReadAsync(body.AsMemory(offset),token);if(read==0)throw new ArgumentException("HTTP body tugallanmagan.");offset+=read;}
        return new WifiHttpRequest(first[0],first[1],headers,new UTF8Encoding(false,true).GetString(body));
    }
}
