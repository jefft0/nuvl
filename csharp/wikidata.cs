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
      public HashSet<int> hasInstance_ = null;
      public int[] subclassOf_ = null;
      public HashSet<int> hasSubclass_ = null;
      public int[] partOf_ = null;
      public HashSet<int> hasPart_ = null;
      public HashSet<int> debugRootClasses_ = null;
      public bool hasSubclassOfLoop_ = false;
      public bool hasPartOfLoop_ = false;
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
      addHasInstance(int id)
      {
        if (hasInstance_ == null)
          hasInstance_ = new HashSet<int>();
        hasInstance_.Add(id);
      }
      public void
      addHasSubclass(int id)
      {
        if (hasSubclass_ == null)
          hasSubclass_ = new HashSet<int>();
        hasSubclass_.Add(id);
      }
      public void
      addHasPart(int id)
      {
        if (hasPart_ == null)
          hasPart_ = new HashSet<int>();
        hasPart_.Add(id);
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
            if (label_.Length == 0)
              // Don't use up memory with just the Q ID string.
              return "Q" + Id;
            else {
              // Add the Id to label_.
              labelHasId_ = true;
              label_ = label_ + " (Q" + Id + ")";
            }
          }

          return label_;
        }
      }

      public override string
      ToString()
      {
        return EnLabelWithId;
      }

      public delegate ICollection<int> GetPropertyValues(Item item);
      public static ICollection<int> getInstanceOf(Item item) { return item.instanceOf_; }
      public static ICollection<int> getHasInstance(Item item) { return item.hasInstance_; }
      public static ICollection<int> getSubclassOf(Item item) { return item.subclassOf_; }
      public static ICollection<int> getHasSubclass(Item item) { return item.hasSubclass_; }
      public static ICollection<int> getPartOf(Item item) { return item.partOf_; }
      public static ICollection<int> getHasPart(Item item) { return item.hasPart_; }

      public delegate void SetHasLoop(Item item, bool hasLoop);
      public static void setHasSubclassOfLoop(Item item, bool hasLoop) { item.hasSubclassOfLoop_ = hasLoop; }
      public static void setHasPartOfLoop(Item item, bool hasLoop) { item.hasPartOfLoop_ = hasLoop; }

      public delegate bool GetHasLoop(Item item);
      public static bool getHasSubclassOfLoop(Item item) { return item.hasSubclassOfLoop_; }
      public static bool getHasPartOfLoop(Item item) { return item.hasPartOfLoop_; }
    }

    /// <summary>
    /// Return a sorted array of all Item which are instance of id (direct).
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[]
    hasDirectInstance(int id)
    {
      Item[] result;
      if (!cachedHasDirectInstance_.TryGetValue(id, out result)) {
        result = getPropertyValuesAsSortedItems(id, Item.getHasInstance);
        cachedHasDirectInstance_[id] = result;
      }

      return result;
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
      if (!cachedHasDirectSubclass_.TryGetValue(id, out result)) {
        result = getPropertyValuesAsSortedItems(id, Item.getHasSubclass);
        cachedHasDirectSubclass_[id] = result;
      }

      return result;
    }

    /// <summary>
    /// Return a sorted array of all Item which are part of id (direct).
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[]
    hasDirectPart(int id)
    {
      Item[] result;
      if (!cachedHasDirectPart_.TryGetValue(id, out result)) {
        result = getPropertyValuesAsSortedItems(id, Item.getHasPart);
        cachedHasDirectPart_[id] = result;
      }

      return result;
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
      if (!cachedIndirectSubclassOf_.TryGetValue(id, out result)) {
        result = getIndirectPropertyValuesAsSortedItems(id, Item.getSubclassOf, Item.getHasSubclassOfLoop);
        cachedIndirectSubclassOf_[id] = result;
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
      if (!cachedHasIndirectSubclass_.TryGetValue(id, out result)) {
        result = getIndirectPropertyValuesAsSortedItems(id, Item.getHasSubclass, Item.getHasSubclassOfLoop);
        cachedHasIndirectSubclass_[id] = result;
      }

      return result;
    }

    /// <summary>
    /// Return a sorted array of all values for part of recursively,
    /// minus the items that are direct values of part of.
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[]
    indirectPartOf(int id)
    {
      Item[] result;
      if (!cachedIndirectPartOf_.TryGetValue(id, out result)) {
        result = getIndirectPropertyValuesAsSortedItems(id, Item.getPartOf, Item.getHasPartOfLoop);
        cachedIndirectPartOf_[id] = result;
      }

      return result;
    }

    /// <summary>
    /// Return a sorted array of all items which are part of id recursively,
    /// minus the items that are direct part of id.
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[]
    hasIndirectPart(int id)
    {
      Item[] result;
      if (!cachedHasIndirectPart_.TryGetValue(id, out result)) {
        result = getIndirectPropertyValuesAsSortedItems(id, Item.getHasPart, Item.getHasPartOfLoop);
        cachedHasIndirectPart_[id] = result;
      }

      return result;
    }

    /// <summary>
    /// Return a sorted array of all values for instance of plus their
    /// subclass of recursively, minus the items that are direct values of instance of.
    /// </summary>
    /// <param name="id">The Item id.</param>
    /// <returns>The array of Item, sorted byte ToString().</returns>
    public Item[]
    indirectInstanceOf(int id)
    {
      Item[] result;
      if (!cachedIndirectInstanceOf_.TryGetValue(id, out result)) {
        Item item;
        if (!items_.TryGetValue(id, out item) || item.instanceOf_ == null)
          result = new Item[0];
        else {
          var resultSet = new HashSet<Item>();

          // Add subclass of.
          foreach (var valueId in item.instanceOf_) {
            Item value;
            if (items_.TryGetValue(valueId, out value))
              addAllTransitivePropertyValues(resultSet, value, Item.getSubclassOf, Item.getHasSubclassOfLoop);
          }

          // Remove the direct classes from instance of.
          foreach (var valueId in item.instanceOf_) {
            Item value;
            if (items_.TryGetValue(valueId, out value))
              resultSet.Remove(value);
          }

          result = setToArray(resultSet);
          Array.Sort(result, new Item.StringComparer());
          cachedIndirectInstanceOf_[id] = result;
        }
      }

      return result;
    }

    public void
    dumpFromGZip(string gzipFilePath)
    {
      var nLines = 0;

      var startTime = DateTime.Now;
      System.Console.Out.WriteLine(startTime);
      using (var file = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read)) {
        using (var gzip = new GZipStream(file, CompressionMode.Decompress)) {
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
          using (var reader = new StreamReader(gzip /* , Encoding.ASCII */)) {
            string line;
            while ((line = reader.ReadLine()) != null) {
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

      using (var file = new StreamWriter(@"c:\temp\itemEnLabels.tsv")) {
        foreach (var entry in items_)
          file.WriteLine(entry.Key + "\t" + entry.Value.getEnLabel());
      }

      using (var file = new StreamWriter(@"c:\temp\propertyEnLabels.tsv")) {
        foreach (var entry in propertyEnLabels_)
          file.WriteLine(entry.Key + "\t" + entry.Value);
      }

      using (var file = new StreamWriter(@"c:\temp\instanceOf.tsv")) {
        foreach (var entry in items_) {
          if (entry.Value.instanceOf_ != null) {
            file.Write(entry.Key);
            foreach (var value in entry.Value.instanceOf_)
              file.Write("\t" + value);
            file.WriteLine("");
          }
        }
      }

      using (var file = new StreamWriter(@"c:\temp\subclassOf.tsv")) {
        foreach (var entry in items_) {
          if (entry.Value.subclassOf_ != null) {
            file.Write(entry.Key);
            foreach (var value in entry.Value.subclassOf_)
              file.Write("\t" + value);
            file.WriteLine("");
          }
        }
      }

      using (var file = new StreamWriter(@"c:\temp\partOf.tsv")) {
        foreach (var entry in items_) {
          if (entry.Value.partOf_ != null) {
            file.Write(entry.Key);
            foreach (var value in entry.Value.partOf_)
              file.Write("\t" + value);
            file.WriteLine("");
          }
        }
      }

      System.Console.Out.Write("Finding instances, subclasses and parts ...");
      setHasInstanceHasSubclassAndHasPart();
      System.Console.Out.WriteLine(" done.");
    }

    public void
    loadFromDump()
    {
      var startTime = DateTime.Now;

      using (var file = new StreamReader(@"c:\temp\itemEnLabels.tsv")) {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
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

      using (var file = new StreamReader(@"c:\temp\propertyEnLabels.tsv")) {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.Write("\rnPropertyLabelsLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          propertyEnLabels_[id] = splitLine[1];
        }
        System.Console.Out.WriteLine("");
      }

      using (var file = new StreamReader(@"c:\temp\instanceOf.tsv")) {
        var valueSet = new HashSet<int>();
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
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

      using (var file = new StreamReader(@"c:\temp\subclassOf.tsv")) {
        var valueSet = new HashSet<int>();
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
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

      using (var file = new StreamReader(@"c:\temp\partOf.tsv")) {
        var valueSet = new HashSet<int>();
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
          ++nLines;
          if (nLines % 100000 == 0)
            System.Console.Out.Write("\rnpartOfLines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var item = items_[Int32.Parse(splitLine[0])];

          valueSet.Clear();
          for (int i = 1; i < splitLine.Length; ++i)
            valueSet.Add(Int32.Parse(splitLine[i]));
          item.partOf_ = new int[valueSet.Count];
          valueSet.CopyTo(item.partOf_);
        }
        System.Console.Out.WriteLine("");
      }

      System.Console.Out.Write("Finding instances, subclasses and parts ...");
      setHasInstanceHasSubclassAndHasPart();
      System.Console.Out.WriteLine(" done.");

      System.Console.Out.WriteLine("Load elapsed " + (DateTime.Now - startTime));
    }

    private void setHasInstanceHasSubclassAndHasPart()
    {
      foreach (var item in items_.Values) {
        if (item.instanceOf_ != null) {
          foreach (var id in item.instanceOf_) {
            Item value;
            if (items_.TryGetValue(id, out value))
              value.addHasInstance(item.Id);
          }
        }

        if (item.subclassOf_ != null) {
          foreach (var id in item.subclassOf_) {
            Item value;
            if (items_.TryGetValue(id, out value))
              value.addHasSubclass(item.Id);
          }
        }

        if (item.partOf_ != null) {
          foreach (var id in item.partOf_) {
            Item value;
            if (items_.TryGetValue(id, out value))
              value.addHasPart(item.Id);
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
      else {
        var propertyPrefix = "{\"id\":\"P";
        if (line.StartsWith(propertyPrefix))
          processProperty(line, propertyPrefix.Length);
        else
          throw new Exception
          ("Not an item or property: " + line.Substring(25));
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

      item.instanceOf_ = setToArray(getPropertyValues(item, "instance of", line, 31));
      item.subclassOf_ = setToArray(getPropertyValues(item, "subclass of", line, 279));
      item.partOf_ = setToArray(getPropertyValues(item, "subclass of", line, 361));
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

    private HashSet<int>
    getPropertyValues(Item item, string propertyName, string line, int propertyId)
    {
      var propertyPrefix =
        "\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P" + propertyId + 
        "\",\"datatype\":\"wikibase-item\",\"datavalue\":{\"value\":{\"entity-type\":\"item\",\"numeric-id\":";
      var valueSet = new HashSet<int>();
      var iProperty = 0;
      while (true) {
        iProperty = line.IndexOf(propertyPrefix, iProperty);
        if (iProperty < 0)
          break;

        iProperty += propertyPrefix.Length;
        var value = getInt(line, iProperty, '}');
        if (value >= 0) {
          if (value != item.Id)
            valueSet.Add(value);
          else
            messages_.Add("Item is " + propertyName + " itself: " + item);
        }
      }

      if (valueSet.Count == 0)
        return null;
      else
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

    private void addAllTransitivePropertyValues
      (HashSet<Item> allPropertyValues, Item item, Item.GetPropertyValues getPropertyValues, Item.GetHasLoop getHasLoop)
    {
      var propertyValues = getPropertyValues(item);
      if (propertyValues != null) {
        foreach (var valueId in propertyValues) {
          Item value;
          if (items_.TryGetValue(valueId, out value)) {
            allPropertyValues.Add(value);
            if (!getHasLoop(value))
              addAllTransitivePropertyValues(allPropertyValues, value, getPropertyValues, getHasLoop);
          }
        }
      }
    }

    private Item[]
    getPropertyValuesAsSortedItems(int id, Item.GetPropertyValues getPropertyValues)
    {
      Item item;
      if (!items_.TryGetValue(id, out item))
        return new Item[0];

      var propertyValues = getPropertyValues(item);
      if (propertyValues == null)
        return new Item[0];

      var result = new Item[propertyValues.Count];
      var i = 0;
      foreach (var value in propertyValues)
        result[i++] = items_[value];

      Array.Sort(result, new Item.StringComparer());

      return result;
    }

    private Item[] 
    getIndirectPropertyValuesAsSortedItems(int id, Item.GetPropertyValues getPropertyValues, Item.GetHasLoop getHasLoop)
    {
      Item item;
      if (!items_.TryGetValue(id, out item))
        return new Item[0];

      var resultSet = new HashSet<Item>();
      addAllTransitivePropertyValues(resultSet, item, getPropertyValues, getHasLoop);

      // Remove direct propertyValues.
      var directPropertyValues = getPropertyValues(item);
      if (directPropertyValues != null) {
        foreach (var valueId in directPropertyValues) {
          Item value;
          if (items_.TryGetValue(valueId, out value))
            resultSet.Remove(value);
        }
      }

      var result = setToArray(resultSet);
      Array.Sort(result, new Item.StringComparer());

      return result;
    }

    public List<string> messages_ = new List<string>();
    public Dictionary<int, Item> items_ = new Dictionary<int, Item>();
    public Dictionary<int, string> propertyEnLabels_ = new Dictionary<int, string>();

    private Dictionary<int, Item[]> cachedIndirectSubclassOf_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasDirectSubclass_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasIndirectSubclass_ = new Dictionary<int, Item[]>();

    private Dictionary<int, Item[]> cachedIndirectInstanceOf_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasDirectInstance_ = new Dictionary<int, Item[]>();

    private Dictionary<int, Item[]> cachedIndirectPartOf_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasDirectPart_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasIndirectPart_ = new Dictionary<int, Item[]>();
  }
}
