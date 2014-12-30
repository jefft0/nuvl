using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace StoreBlobs
{
  class StoreBlobs
  {
    static void Main(string[] args)
    {
      processFile(@"C:\temp\cam6-15.mp4");
    }

    static void processFile(string sourceFilePath)
    {
      Console.Out.Write(sourceFilePath + " .");
      var base64 = toBase64(getFileSha256(sourceFilePath));

      var blobsDirectory = @"C:\public\blobs";
      var blobFileDirectory = blobsDirectory + @"\sha256\" +
        base64.Substring(0, 2).ToLower() + @"\" +
        base64.Substring(2, 2).ToLower();
      var blobFilePath = blobFileDirectory + @"\" + base64 + ".dat";

      if (!Directory.Exists(blobFileDirectory))
        Directory.CreateDirectory(blobFileDirectory);

      Console.Out.Write(".");
      copyBlob(sourceFilePath, blobFilePath);
      Console.Out.WriteLine(". " + base64);
    }

    static void copyBlob(string sourceFilePath, string blobFilePath)
    {
      // TODO: Check if blobFilePath exists and is the same file.

      var tempFilePath = blobFilePath + ".temp";
      File.Copy(sourceFilePath, tempFilePath);

      if (File.Exists(blobFilePath))
        File.Delete(blobFilePath);
      File.Move(tempFilePath, blobFilePath);
    }

    static byte[] getFileSha256(string filePath)
    {
      using (var fileStream = File.OpenRead(filePath))
        return SHA256Managed.Create().ComputeHash(fileStream);
    }

    static string toBase64(byte[] binary)
    {
      return System.Convert.ToBase64String(binary)
        .Replace("=", "").Replace('+', '-').Replace('/', '_');
    }
  }
}
