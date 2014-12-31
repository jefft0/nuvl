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
      foreach (var directoryPath in new string[] {
                 @"F:\cameras\2014\camera3",
                 @"F:\cameras\2014\camera4",
                 @"F:\cameras\2014\camera5",
                 @"F:\cameras\2014\camera6"
               })
      {
        foreach (var fileName in new DirectoryInfo(directoryPath).GetFiles())
          processFile(directoryPath + @"\" + fileName);
      }
    }

    static void processFile(string sourceFilePath)
    {
      Console.Out.Write(sourceFilePath + " .");
      var base64 = toBase64(getFileSha256(sourceFilePath));
      var blobName = "sha256-" + base64;

      var blobsDirectory = @"C:\public\blobs";
      var blobFileDirectory = blobsDirectory + @"\sha256\" +
        base64.Substring(0, 2).ToLower() + @"\" +
        base64.Substring(2, 2).ToLower();
      var blobFilePath = blobFileDirectory + @"\" + blobName + ".dat";

      Console.Out.Write(".");
      if (!Directory.Exists(blobFileDirectory))
        Directory.CreateDirectory(blobFileDirectory);
      copyBlob(sourceFilePath, blobFilePath);

      // Update the inventory.
      Console.Out.Write(".");
      var inventoryFilePath = @"C:\public\blobs\inventory.tsv";
      using (var file = new StreamWriter(inventoryFilePath, true))
        file.WriteLine(blobName + "\t" + sourceFilePath);

      Console.Out.WriteLine(" " + blobName);
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
      using (var file = File.OpenRead(filePath))
        return SHA256Managed.Create().ComputeHash(file);
    }

    static string toBase64(byte[] binary)
    {
      return System.Convert.ToBase64String(binary)
        .Replace("=", "").Replace('+', '-').Replace('/', '_');
    }
  }
}
