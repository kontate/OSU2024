/*
// This is written by @tokoroten-lab from https://qiita.com/tokoroten-lab/items/b865edaa0e3018cb5e55
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Compression;

//public class Compressor : MonoBehaviour
public class Compressor
{
    public static byte[] Compress(byte[] rawData)
    {
        byte[] result = null;

        using (MemoryStream compressedStream = new MemoryStream())
        {
            using (GZipStream gZipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                gZipStream.Write(rawData, 0, rawData.Length);
            }
            result = compressedStream.ToArray();
        }

        return result;
    }

    public static byte[] Decompress(byte[] compressedData)
    {
        byte[] result = null;

        using (MemoryStream compressedStream = new MemoryStream(compressedData))
        {
            using (MemoryStream decompressedStream = new MemoryStream())
            {
                using (GZipStream gZipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    gZipStream.CopyTo(decompressedStream);
                }
                result = decompressedStream.ToArray();
            }
        }

        return result;
    }
}
*/

// This is written by @chamaton from https://shamaton.orz.hm/blog/archives/544
using System.IO;
using System.IO.Compression;
 
public class Compressor {
  
  public static byte[] Compress(byte[] source) {
    MemoryStream ms = new MemoryStream();
    DeflateStream CompressedStream = new DeflateStream(ms, CompressionMode.Compress, true);
 
    CompressedStream.Write(source, 0, source.Length);
    CompressedStream.Close();
    return ms.ToArray();
  }
 
  public static byte[] Decompress(byte[] source) {
    MemoryStream ms = new MemoryStream(source);
    MemoryStream ms2 = new MemoryStream();
 
    DeflateStream CompressedStream = new DeflateStream(ms, CompressionMode.Decompress);
 
    while (true) {
      int rb = CompressedStream.ReadByte();
      if (rb == -1) {
        break;
      }
      ms2.WriteByte((byte)rb);
    }
    return ms2.ToArray();
  }
}

