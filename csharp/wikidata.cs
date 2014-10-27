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
      public int[] instanceOf_ = null;
      public int[] subclassOf_ = null;
      public HashSet<int> hasSubclass_ = null;
      public HashSet<int> debugRootClasses_ = null;
      public bool hasSubclassOfLoop_ = false;
      private string label_;
      private bool labelHasId_ = false;

      public Item(int id, string enLabel)
      {
        Id = id;
        label_ = enLabel;
      }

      public class StringComparer : IComparer<Item>
      {
        public int
        Compare(Wikidata.Item item1, Wikidata.Item item2) { return String.Compare(item1.EnLabelWithId, item2.EnLabelWithId); }
      }

      public void 
      addHasSubclass(int id)
      {
        if (hasSubclass_ == null)
          hasSubclass_ = new HashSet<int>();
        hasSubclass_.Add(id);
      }

      public string
      getEnLabel()
      {
        if (!labelHasId_)
          return label_;
        else {
          // Need to strip the id.
          if (label_.StartsWith("Q") && !label_.Contains(" "))
            return "";
          else
            return label_.Substring(0, label_.LastIndexOf(" ("));
        }
      }

      public string 
      EnLabelWithId
      {
        get
        {
          if (!labelHasId_) {
            // Add the Id to label_.
            label_ = (label_ == "" ? "Q" + Id : label_ + " (Q" + Id + ")");
            labelHasId_ = true;
          }

          return label_;
        }
      }

      public override string
      ToString()
      {
        return EnLabelWithId;
      }
    }

    /// <summary>
    /// Return a sorted array of all values for subclass of recursively,
    /// minus the items that are direct values of subclass of.
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[]
    indirectSubclassOf(int id)
    {
      Item[] result;
      if (!cachedIndirectSubclassOf_.TryGetValue(id, out result))
      {
        var item = items_[id];
        if (item.subclassOf_ == null)
          result = new Item[0];
        else
        {
          var resultSet = new HashSet<Item>();
          addAllSubclassOf(resultSet, item);

          // Remove direct subclass of.
          foreach (var value in item.subclassOf_)
            resultSet.Remove(items_[value]);

          result = setToArray(resultSet);
          Array.Sort(result, new Item.StringComparer());
          cachedIndirectSubclassOf_[id] = result;
        }
      }

      return result;
    }

    private void addAllSubclassOf(HashSet<Item> allSubclassOf, Item item)
    {
      if (item.subclassOf_ == null)
        return;
      foreach (var subclassId in item.subclassOf_)
      {
        var value = items_[subclassId];
        allSubclassOf.Add(value);
        if (!value.hasSubclassOfLoop_)
          addAllSubclassOf(allSubclassOf, value);
      }
    }

    /// <summary>
    /// Return a sorted array of all Item which are subclass of id (direct).
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[] 
    hasDirectSubclass(int id)
    {
      Item[] result;
      if (!cachedHasDirectSubclass_.TryGetValue(id, out result))
      {
        var item = items_[id];

        if (item.hasSubclass_ == null)
          result = new Item[0];
        else
        {
          result = new Item[item.hasSubclass_.Count];
          var i = 0;
          foreach (var value in item.hasSubclass_)
            result[i++] = items_[value];

          Array.Sort(result, new Item.StringComparer());
        }

        cachedHasDirectSubclass_[id] = result;
      }

      return result;
    }

    /// <summary>
    /// Return a sorted array of all items which are subclass of id recursively,
    /// minus the items that are direct subclass of id.
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[]
    hasIndirectSubclass(int id)
    {
      Item[] result;
      if (!cachedHasIndirectSubclass_.TryGetValue(id, out result))
      {
        var item = items_[id];
        var resultSet = new HashSet<Item>();
        addAllHasSubclass(resultSet, item);

        // Remove direct has subclass.
        if (item.hasSubclass_ != null)
        {
          foreach (var subclassId in item.hasSubclass_)
            resultSet.Remove(items_[subclassId]);
        }

        result = setToArray(resultSet);
        Array.Sort(result, new Item.StringComparer());
        cachedHasIndirectSubclass_[id] = result;
      }

      return result;
    }

    private void
    addAllHasSubclass(HashSet<Item> allHasSubclass, Item item)
    {
      if (item.hasSubclass_ != null)
      {
        foreach (var subclassId in item.hasSubclass_)
        {
          var subclass = items_[subclassId];
          allHasSubclass.Add(subclass);
          if (!subclass.hasSubclassOfLoop_)
            addAllHasSubclass(allHasSubclass, subclass);
        }
      }
    }

    public void
    dumpFromGZip(string gzipFilePath)
    {
      var nLines = 0;

      var startTime = DateTime.Now;
      System.Console.Out.WriteLine(startTime);
      using (var file = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read))
      {
        using (var gzip = new GZipStream(file, CompressionMode.Decompress))
        {
#if false // byte line buffer instead of ReadLine.
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
                System.Console.Out.Write("\rnLines " + nLines);

              processLine(line);
            }
            System.Console.Out.WriteLine("");
          }
#endif
        }
      }

      System.Console.Out.WriteLine("elapsed " + (DateTime.Now - startTime));
      System.Console.Out.WriteLine("nLines " + nLines);

      foreach (var message in messages_)
        System.Console.Out.WriteLine(message);
      System.Console.Out.WriteLine("");

      using (var file = new StreamWriter(@"c:\temp\itemEnLabels.tsv"))
      {
        foreach (var entry in items_)
          file.WriteLine(entry.Key + "\t" + entry.Value.getEnLabel());
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
      var startTime = DateTime.Now;

      using (var file = new StreamReader(@"c:\temp\itemEnLabels.tsv"))
      {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null)
        {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.Write("\rnItemEnLabelsLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          if (!items_.ContainsKey(id))
            items_[id] = new Wikidata.Item(id, splitLine[1]);
        }
        System.Console.Out.WriteLine("");
      }

      using (var file = new StreamReader(@"c:\temp\propertyEnLabels.tsv"))
      {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null)
        {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.Write("\rnPropertyLabelsLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          propertyEnLabels_[id] = splitLine[1];
        }
        System.Console.Out.WriteLine("");
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
            System.Console.Out.Write("\rnInstanceOfLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var item = items_[Int32.Parse(splitLine[0])];

          valueSet.Clear();
          for (int i = 1; i < splitLine.Length; ++i)
            valueSet.Add(Int32.Parse(splitLine[i]));
          item.instanceOf_ = new int[valueSet.Count];
          valueSet.CopyTo(item.instanceOf_);
        }
        System.Console.Out.WriteLine("");
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
            System.Console.Out.Write("\rnsubclassOfLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var item = items_[Int32.Parse(splitLine[0])];

          valueSet.Clear();
          for (int i = 1; i < splitLine.Length; ++i)
            valueSet.Add(Int32.Parse(splitLine[i]));
          item.subclassOf_ = new int[valueSet.Count];
          valueSet.CopyTo(item.subclassOf_);
        }
        System.Console.Out.WriteLine("");
      }

      setHasSubclass();

      System.Console.Out.WriteLine("Load elapsed " + (DateTime.Now - startTime));
    }

    private void setHasSubclass()
    {
      foreach (var item in items_.Values)
      {
        if (item.subclassOf_ != null)
        {
          foreach (var id in item.subclassOf_)
          {
            Item value;
            if (items_.TryGetValue(id, out value))
              value.addHasSubclass(item.Id);
          }
        }
      }
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
        messages_.Add("Already have item id " + items_[id] + ". Got \"" + enLabel + "\"");
      var item = new Item(id, enLabel);
      items_[id] = item;

      item.instanceOf_ = setToArray(getPropertyValues
        (line,
         "\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P31\",\"datatype\":\"wikibase-item\",\"datavalue\":{\"value\":{\"entity-type\":\"item\",\"numeric-id\":"));

      var subclassOf = getPropertyValues
        (line,
         "\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P279\",\"datatype\":\"wikibase-item\",\"datavalue\":{\"value\":{\"entity-type\":\"item\",\"numeric-id\":");
      if (subclassOf != null && subclassOf.Contains(id))
      {
        messages_.Add("Item is subclass of itself: " + enLabel + " (Q" + id + ")");
        subclassOf.Remove(id);
      }
      item.subclassOf_ = setToArray(subclassOf);
    }

    private static T[] setToArray<T>(HashSet<T> set)
    {
      if (set == null)
        return null;

      var result = new T[set.Count];
      set.CopyTo(result);
      return result;
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

    private static HashSet<int>
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

      return valueSet;
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
    private Dictionary<int, Item[]> cachedIndirectSubclassOf_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasDirectSubclass_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasIndirectSubclass_ = new Dictionary<int, Item[]>();
  }
}
