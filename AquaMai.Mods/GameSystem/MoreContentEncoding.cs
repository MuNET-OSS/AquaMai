using System.IO;
using System.IO.Compression;
using Net;
using AquaMai.Config.Attributes;
using HarmonyLib;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "更多Content-Encoding格式",
    en: """
        Enables support for decompressing data encoded with formats other than deflate, such as gzip.
        Useful when the server uses CDNs such as Cloudflare that do not support deflate.
        """,
    zh: """
        开启后游戏将支持解压非deflate格式的数据包，比如Gzip;
        用于服务器使用Cloudflare等不支持deflate格式的CDN;
        """,
    defaultOn: false)]

public class MoreContentEncoding
{
    private const int BufferSize = 1024;  
  
    [HarmonyPrefix]  
    [HarmonyPatch(typeof(NetHttpClient), "Decompress")]  
    public static bool PreDecompress(NetHttpClient __instance)  
    {  
        var traverse = Traverse.Create(__instance);  
        var temporaryStream = traverse.Field<MemoryStream>("_temporaryStream").Value;  
        var memoryStream = traverse.Field<MemoryStream>("_memoryStream").Value;  
        var buffer = traverse.Field<byte[]>("_buffer").Value;  
      
        memoryStream.SetLength(0L);  
        if (temporaryStream.Length == 0L)  
        {  
            return false;  
        }  
      
        var raw = temporaryStream.ToArray();  
       
        //  - 0x1F 0x8B            -> gzip
        //  - 0x78 ?? + valid zlib -> zlib (the stock format: zlib header + raw deflate + adler32)
        //  - otherwise            -> treat as plaintext
        if (raw.Length >= 2 && raw[0] == 0x1F && raw[1] == 0x8B)  
        {  
            using var gz = new GZipStream(new MemoryStream(raw, writable: false), CompressionMode.Decompress);  
            CopyTo(gz, memoryStream, buffer);  
        }  
        else if (raw.Length >= 6 && raw[0] == 0x78 && TryInflateZlib(raw, memoryStream, buffer))  
        {  
            // zlib 成功解压(
        }  
        else  
        {  
            memoryStream.Write(raw, 0, raw.Length);  
        }  
      
        memoryStream.Seek(0L, SeekOrigin.Begin);  
        temporaryStream.Seek(0L, SeekOrigin.Begin);  
        temporaryStream.SetLength(0L);  
        return false;  
    }  
      
    private static bool TryInflateZlib(byte[] raw, MemoryStream output, byte[] buffer)  
    {  
        var startLength = output.Length;  
        try  
        {  
            // 跳过 2 字节的 zlib 头部，忽略末尾的 4 字节 Adler32 校验和。
            using var input = new MemoryStream(raw, 2, raw.Length - 6, writable: false);  
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);  
            CopyTo(deflate, output, buffer);  
            return true;  
        }  
        catch  
        {   
            output.SetLength(startLength);  
            return false;  
        }  
    }  
      
    private static void CopyTo(Stream from, Stream to, byte[] buffer)  
    {  
        while (true)  
        {  
            var count = from.Read(buffer, 0, BufferSize);  
            if (count <= 0) break;  
            to.Write(buffer, 0, count);  
        }  
    }
}