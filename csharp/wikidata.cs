using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Nuvl
{
  class Wikidata
  {
    public class Item
    {
      public readonly int Id;
      public readonly string EnLabel;
      public int[] instanceOf_ = null;
      public int[] subclassOf_ = null;
      public HashSet<int> rootClasses_ = null;
      public bool hasSubclassOfLoop_ = true;

      public Item(int id, string enLabel)
      {
        Id = id;
        EnLabel = enLabel;
      }

      public override string
      ToString()
      {
        return EnLabel == "" ? "Q" + Id : EnLabel + " (Q" + Id + ")";
      }
    }

    public void
    dumpFromGZip(string gzipFilePath)
    {
      var nLines = 0;

      var startTime = System.DateTime.Now;
      System.Console.Out.WriteLine(startTime);
      using (var file = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read))
      {
        using (var gzip = new GZipStream(file, CompressionMode.Decompress))
        {
#if false
          var input = new byte[100000000];
          var partialLineLength = 0;
          var nBytesRead = 0;
          while ((nBytesRead = gzip.Read(input, partialLineLength, input.Length - partialLineLength)) > 0)
          {
            var inputLength = partialLineLength + nBytesRead;
            var iLineStart = 0;
            while (iLineStart < inputLength)
            {
              var iNewline = Array.IndexOf(input, (byte)'\n', iLineStart, inputLength - iLineStart);
              if (iNewline < 0)
              {
                // Shift the partial line to the beginning of the input.
                partialLineLength = inputLength - iLineStart;
                Array.Copy(input, iLineStart, input, 0, partialLineLength);

                break;
              }

              ++nLines;
              if (nLines % 10000 == 0)
                System.Console.Out.WriteLine("nLines " + nLines + ", nItems ");

              processLine(Encoding.ASCII.GetString(input, iLineStart, iNewline - iLineStart));

              iLineStart = iNewline + 1;
            }
          }
#else
          using (var reader = new StreamReader(gzip /* , Encoding.ASCII */))
          {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
              ++nLines;
              if (nLines % 10000 == 0)
                System.Console.Out.WriteLine("nLines " + nLines);

              processLine(line);
            }
          }
#endif
        }
      }

      System.Console.Out.WriteLine("elapsed " + (System.DateTime.Now - startTime));
      System.Console.Out.WriteLine("nLines " + nLines);

      foreach (var message in messages_)
        System.Console.Out.WriteLine(message);
      System.Console.Out.WriteLine("");

      using (var file = new StreamWriter(@"c:\temp\itemEnLabels.tsv"))
      {
        foreach (var entry in items_)
          file.WriteLine(entry.Key + "\t" + entry.Value.EnLabel);
      }

      using (var file = new StreamWriter(@"c:\temp\propertyEnLabels.tsv"))
      {
        foreach (var entry in propertyEnLabels_)
          file.WriteLine(entry.Key + "\t" + entry.Value);
      }

      using (var file = new StreamWriter(@"c:\temp\instanceOf.tsv"))
      {
        foreach (var entry in items_)
        {
          if (entry.Value.instanceOf_ != null)
          {
            file.Write(entry.Key);
            foreach (var value in entry.Value.instanceOf_)
              file.Write("\t" + value);
            file.WriteLine("");
          }
        }
      }

      using (var file = new StreamWriter(@"c:\temp\subclassOf.tsv"))
      {
        foreach (var entry in items_)
        {
          if (entry.Value.subclassOf_ != null)
          {
            file.Write(entry.Key);
            foreach (var value in entry.Value.subclassOf_)
              file.Write("\t" + value);
            file.WriteLine("");
          }
        }
      }
    }

    public void
    loadFromDump()
    {
      var startTime = System.DateTime.Now;
      System.Console.Out.WriteLine(startTime);

      using (var file = new StreamReader(@"c:\temp\itemEnLabels.tsv"))
      {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null)
        {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.WriteLine("nItemEnLabelsLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          if (!items_.ContainsKey(id))
            items_[id] = new Wikidata.Item(id, splitLine[1]);
        }
      }

      using (var file = new StreamReader(@"c:\temp\propertyEnLabels.tsv"))
      {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null)
        {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.WriteLine("nPropertyLabelsLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          propertyEnLabels_[id] = splitLine[1];
        }
      }

      using (var file = new StreamReader(@"c:\temp\instanceOf.tsv"))
      {
        var valueSet = new HashSet<int>();
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null)
        {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.WriteLine("nInstanceOfLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var item = items_[Int32.Parse(splitLine[0])];

          valueSet.Clear();
          for (int i = 1; i < splitLine.Length; ++i)
            valueSet.Add(Int32.Parse(splitLine[i]));
          item.instanceOf_ = new int[valueSet.Count];
          valueSet.CopyTo(item.instanceOf_);
        }
      }

      using (var file = new StreamReader(@"c:\temp\subclassOf.tsv"))
      {
        var valueSet = new HashSet<int>();
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null)
        {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.WriteLine("nsubclassOfLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var item = items_[Int32.Parse(splitLine[0])];

          valueSet.Clear();
          for (int i = 1; i < splitLine.Length; ++i)
            valueSet.Add(Int32.Parse(splitLine[i]));
          item.subclassOf_ = new int[valueSet.Count];
          valueSet.CopyTo(item.subclassOf_);
        }
      }

      System.Console.Out.WriteLine("Load elapsed " + (System.DateTime.Now - startTime));
    }

    private void
    processLine(string line)
    {
      // Assume one item or property per line.

      // Skip blank lines and the open/close of the outer list.
      if (line.Length == 0 || line[0] == '[' || line[0] == ']')
        return;

      var itemPrefix = "{\"id\":\"Q";
      if (line.StartsWith(itemPrefix))
        processItem(line, itemPrefix.Length);
      else
      {
        var propertyPrefix = "{\"id\":\"P";
        if (line.StartsWith(propertyPrefix))
          processProperty(line, propertyPrefix.Length);
        else
          throw new Exception
          ("Not an item of property: " + line.Substring(25));
      }
    }

    private void
    processItem(string line, int iIdStart)
    {
      var id = getInt(line, iIdStart, '\"');
      if (id < 0)
        return;

      var enLabel = getEnLabel(line);
      if (items_.ContainsKey(id))
        messages_.Add("Already have item Q" + id + " \"" + items_[id].EnLabel + "\". Got \"" + enLabel + "\"");
      var item = new Item(id, enLabel);
      items_[id] = item;

      item.instanceOf_ = getPropertyValues
        (line,
         "\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P31\",\"datatype\":\"wikibase-item\",\"datavalue\":{\"value\":{\"entity-type\":\"item\",\"numeric-id\":");
      item.subclassOf_ = getPropertyValues
        (line,
         "\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P279\",\"datatype\":\"wikibase-item\",\"datavalue\":{\"value\":{\"entity-type\":\"item\",\"numeric-id\":");
    }

    private void
    processProperty(string line, int iIdStart)
    {
      var id = getInt(line, iIdStart, '\"');
      if (id < 0)
        return;

      var enLabel = getEnLabel(line);
      if (propertyEnLabels_.ContainsKey(id))
        messages_.Add("Already have property P" + id + " \"" + propertyEnLabels_[id] + "\". Got \"" + enLabel + "\"");
      propertyEnLabels_[id] = enLabel;
    }

    private static int[]
    getPropertyValues(string line, string propertyPrefix)
    {
      var valueSet = new HashSet<int>();
      var iProperty = 0;
      while (true)
      {
        iProperty = line.IndexOf(propertyPrefix, iProperty);
        if (iProperty < 0)
          break;

        iProperty += propertyPrefix.Length;
        var value = getInt(line, iProperty, '}');
        if (value >= 0)
          valueSet.Add(value);
      }

      if (valueSet.Count == 0)
        return null;

      var result = new int[valueSet.Count];
      valueSet.CopyTo(result);
      return result;
    }

    private static int
    getInt(string line, int iStart, char endChar)
    {
      var iEndChar = line.IndexOf(endChar, iStart);
      if (iEndChar < 0)
        return -1;
      var debug = line.Substring(iStart, iEndChar - iStart);
      return Int32.Parse(line.Substring(iStart, iEndChar - iStart));
    }

    private static string
    getEnLabel(string line)
    {
      var iLabelsStart = line.IndexOf("\"labels\":{\"");
      if (iLabelsStart < 0)
        return "";

      // Debug: Problem if a label has "}}".
      var iLabelsEnd = line.IndexOf("}}", iLabelsStart);
      if (iLabelsEnd < 0)
        return "";

      var enPrefix = "en\":{\"language\":\"en\",\"value\":\"";
      var iEnStart = line.IndexOf(enPrefix, iLabelsStart, iLabelsEnd - iLabelsStart);
      if (iEnStart < 0)
        return "";

      var iEnLabelStart = iEnStart + enPrefix.Length;
      var iEndQuote = line.IndexOf('\"', iEnLabelStart);
      if (iEndQuote < 0)
        return "";

      return line.Substring(iEnLabelStart, iEndQuote - iEnLabelStart);
    }

    public List<string> messages_ = new List<string>();
    public Dictionary<int, Item> items_ = new Dictionary<int, Item>();
    public Dictionary<int, string> propertyEnLabels_ = new Dictionary<int, string>();
  }
}
