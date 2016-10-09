using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Nuvl
{
  public class Wikidata
  {
    public class Item
    {
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

      public readonly int Id;
      public int[] instanceOf_ = null;
      public HashSet<int> hasInstance_ = null;
      public int[] subclassOf_ = null;
      public HashSet<int> hasSubclass_ = null;
      public int[] partOf_ = null;
      public HashSet<int> hasPart_ = null;
      public int[] location_ = null;
      public int[] locatedInTheAdministrativeTerritorialEntity_ = null;
      public int[] locatedInTimeZone_ = null;
      public HashSet<int> debugRootClasses_ = null;
      public bool hasSubclassOfLoop_ = false;
      public bool hasPartOfLoop_ = false;
      private string label_;
      private bool labelHasId_ = false;

      public static ICollection<int> getInstanceOf(Item item) { return item.instanceOf_; }
      public static void setInstanceOf(Item item, int[] values) { item.instanceOf_ = values; }
      public static ICollection<int> getHasInstance(Item item) { return item.hasInstance_; }
      public static ICollection<int> getSubclassOf(Item item) { return item.subclassOf_; }
      public static void setSubclassOf(Item item, int[] values) { item.subclassOf_ = values; }
      public static ICollection<int> getHasSubclass(Item item) { return item.hasSubclass_; }
      public static ICollection<int> getPartOf(Item item) { return item.partOf_; }
      public static void setPartOf(Item item, int[] values) { item.partOf_ = values; }
      public static ICollection<int> getHasPart(Item item) { return item.hasPart_; }
      public static ICollection<int> getLocation(Item item) { return item.location_; }
      public static void setLocation(Item item, int[] values) { item.location_ = values; }
      public static ICollection<int> getLocatedInTheAdministrativeTerritorialEntity(Item item) { return item.locatedInTheAdministrativeTerritorialEntity_; }
      public static void setLocatedInTheAdministrativeTerritorialEntity(Item item, int[] values) { item.locatedInTheAdministrativeTerritorialEntity_ = values; }
      public static ICollection<int> getLocatedInTimeZone(Item item) { return item.locatedInTimeZone_; }
      public static void setLocatedInTimeZone(Item item, int[] values) { item.locatedInTimeZone_ = values; }

      public delegate void SetHasLoop(Item item, bool hasLoop);
      public static void setHasSubclassOfLoop(Item item, bool hasLoop) { item.hasSubclassOfLoop_ = hasLoop; }
      public static void setHasPartOfLoop(Item item, bool hasLoop) { item.hasPartOfLoop_ = hasLoop; }

      public delegate bool GetHasLoop(Item item);
      public static bool getHasSubclassOfLoop(Item item) { return item.hasSubclassOfLoop_; }
      public static bool getHasPartOfLoop(Item item) { return item.hasPartOfLoop_; }
    }

    public class Property
    {
      public Property(int id, string enLabel)
      {
        Id = id;
        label_ = enLabel;
      }

      public string
      getEnLabel() { return label_; }

      public string
      getEnLabelOrId()
      {
        if (label_.Length == 0)
          return "P" + Id;
        else
          return label_;
      }

      public int[] subpropertyOf_ = null;
      public Datatype datatype_ = Datatype.WikibaseItem;
      public readonly int Id;
      private string label_;

      public static ICollection<int> getSubpropertyOf(Property property) { return property.subpropertyOf_; }
      public static void setSubpropertyOf(Property property, int[] values) { property.subpropertyOf_ = values; }
    }

    public enum Datatype
    {
      WikibaseItem, WikibaseProperty, GlobeCoordinate, Quantity, Time, Url,
      String, MonolingualText, CommonsMedia, ExternalIdentifier, MathematicalExpression
    }

    public static readonly Dictionary<Datatype, string> DatatypeString = new Dictionary<Datatype, string> {
      { Datatype.WikibaseItem, "wikibase-item" },
      { Datatype.WikibaseProperty, "wikibase-property" },
      { Datatype.GlobeCoordinate, "globe-coordinate" },
      { Datatype.Quantity, "quantity" },
      { Datatype.Time, "time" },
      { Datatype.Url, "url" },
      { Datatype.String, "string" },
      { Datatype.MonolingualText, "monolingualtext" },
      { Datatype.CommonsMedia, "commonsMedia" },
      { Datatype.ExternalIdentifier, "external-id" },
      { Datatype.MathematicalExpression, "math" }
    };

    public static Datatype 
    getDatatypeFromString(string datatypeString)
    {
      foreach (var entry in DatatypeString) {
        if (entry.Value == datatypeString)
          return entry.Key;
      }

      throw new Exception("Unrecognized Datatype string: " + datatypeString);
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
      Console.Out.WriteLine(startTime);
      using (var file = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read)) {
        using (var gzip = new GZipStream(file, CompressionMode.Decompress)) {
          using (var reader = new StreamReader(gzip /* , Encoding.ASCII */)) {
            string line;
            while ((line = reader.ReadLine()) != null) {
              ++nLines;
              if (nLines % 10000 == 0)
                Console.Out.Write("\rnLines " + nLines);

              processLine(line, nLines);
            }
            Console.Out.WriteLine("");
          }
        }
      }

      Console.Out.WriteLine("elapsed " + (DateTime.Now - startTime));
      Console.Out.WriteLine("nLines " + nLines);

      foreach (var message in messages_)
        Console.Out.WriteLine(message);
      Console.Out.WriteLine("");

      Console.Out.Write("Writing dump files ...");
      using (var file = new StreamWriter(@"c:\temp\itemEnLabels.tsv")) {
        foreach (var entry in items_) {
          // Json-encode the value, omitting surrounding quotes.
          var jsonString = jsonSerializer_.Serialize(entry.Value.getEnLabel());
          file.WriteLine(entry.Key + "\t" + jsonString.Substring(1, jsonString.Length - 2));
        }
      }

      using (var file = new StreamWriter(@"c:\temp\propertyEnLabels.tsv")) {
        foreach (var entry in properties_) {
          // Json-encode the value, omitting surrounding quotes.
          var jsonString = jsonSerializer_.Serialize(entry.Value.getEnLabel());
          file.WriteLine(entry.Key + "\t" + jsonString.Substring(1, jsonString.Length - 2));
        }
      }

      dumpProperty(items_, Item.getInstanceOf, @"c:\temp\instanceOf.tsv");
      dumpProperty(items_, Item.getSubclassOf, @"c:\temp\subclassOf.tsv");
      dumpProperty(items_, Item.getPartOf, @"c:\temp\partOf.tsv");
      dumpProperty(items_, Item.getLocation, @"c:\temp\location.tsv");
      dumpProperty(items_, Item.getLocatedInTheAdministrativeTerritorialEntity, @"c:\temp\locatedInTheAdministrativeTerritorialEntity.tsv");
      dumpProperty(items_, Item.getLocatedInTimeZone, @"c:\temp\locatedInTimeZone.tsv");

      dumpProperty(properties_, Property.getSubpropertyOf, @"c:\temp\propertySubpropertyOf.tsv");

      using (var file = new StreamWriter(@"c:\temp\propertyDatatype.tsv")) {
        foreach (var entry in properties_)
          file.WriteLine(entry.Key + "\t" + DatatypeString[entry.Value.datatype_]);
      }

      Console.Out.WriteLine(" done.");

      Console.Out.Write("Finding instances, subclasses and parts ...");
      setHasInstanceHasSubclassAndHasPart();
      Console.Out.WriteLine(" done.");
    }

    private static void dumpProperty<T>
      (Dictionary<int, T> dictionary, GetIntArray<T> getPropertyValues, string filePath)
    {
      using (var file = new StreamWriter(filePath)) {
        foreach (var entry in dictionary) {
          if (getPropertyValues(entry.Value) != null) {
            file.Write(entry.Key);
            foreach (var value in getPropertyValues(entry.Value))
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

      using (var file = new StreamReader(@"c:\temp\itemEnLabels.tsv")) {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
          ++nLines;
          if (nLines % 100000 == 0)
            Console.Out.Write("\rN itemEnLabels lines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          if (!items_.ContainsKey(id))
            // Decode the Json value.
            items_[id] = new Wikidata.Item(id, jsonSerializer_.Deserialize<string>("\"" + splitLine[1] + "\""));
        }
        Console.Out.WriteLine("");
      }

      using (var file = new StreamReader(@"c:\temp\propertyEnLabels.tsv")) {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
          ++nLines;
          if (nLines % 100000 == 0)
            Console.Out.Write("\rN propertyEnLabels lines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          if (!properties_.ContainsKey(id))
            // Decode the Json value.
            properties_[id] = new Property(id, jsonSerializer_.Deserialize<string>("\"" + splitLine[1] + "\""));
        }
        Console.Out.WriteLine("");
      }

      using (var file = new StreamReader(@"c:\temp\propertyDatatype.tsv")) {
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
          ++nLines;
          if (nLines % 100000 == 0)
            Console.Out.Write("\rN propertyDatatype lines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var id = Int32.Parse(splitLine[0]);
          properties_[id].datatype_ = getDatatypeFromString(splitLine[1]);
        }
        Console.Out.WriteLine("");
      }

      loadPropertyFromDump(@"c:\temp\instanceOf.tsv", items_, Item.setInstanceOf, "instance of");
      loadPropertyFromDump(@"c:\temp\subclassOf.tsv", items_, Item.setSubclassOf, "subclass of");
      loadPropertyFromDump(@"c:\temp\partOf.tsv", items_, Item.setPartOf, "part of");
      loadPropertyFromDump(@"c:\temp\location.tsv", items_, Item.setLocation, "location");
      loadPropertyFromDump(@"c:\temp\locatedInTheAdministrativeTerritorialEntity.tsv", items_, Item.setLocatedInTheAdministrativeTerritorialEntity, "located in the administrative territorial entity");
      loadPropertyFromDump(@"c:\temp\locatedInTimeZone.tsv", items_, Item.setLocatedInTimeZone, "located in time zone");

      loadPropertyFromDump(@"c:\temp\propertySubpropertyOf.tsv", properties_, Property.setSubpropertyOf, "subproperty of");

      Console.Out.Write("Finding instances, subclasses and parts ...");
      setHasInstanceHasSubclassAndHasPart();
      Console.Out.WriteLine(" done.");

      Console.Out.WriteLine("Load elapsed " + (DateTime.Now - startTime));
    }

    private void loadPropertyFromDump<T>
      (string filePath, Dictionary<int, T> dictionary, SetIntArray<T> setPropertyValues, string propertyLabel)
    {
      using (var file = new StreamReader(filePath)) {
        var valueSet = new HashSet<int>();
        var nLines = 0;
        string line;
        while ((line = file.ReadLine()) != null) {
          ++nLines;
          if (nLines % 100000 == 0)
            Console.Out.Write("\rN " + propertyLabel + " lines " + nLines);

          var splitLine = line.Split(new char[] { '\t' });
          var obj = dictionary[Int32.Parse(splitLine[0])];

          valueSet.Clear();
          for (int i = 1; i < splitLine.Length; ++i)
            valueSet.Add(Int32.Parse(splitLine[i]));
          setPropertyValues(obj, setToArray(valueSet));
        }
        Console.Out.WriteLine("");
      }
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
    processLine(string line, int nLines)
    {
      // Assume one item or property per line.

      // Skip blank lines and the open/close of the outer list.
      if (line.Length == 0 || line[0] == '[' || line[0] == ']' || line[0] == ',')
        return;

      string itemPattern;
      if (nLines == 1)
        itemPattern = "^\\[{\"type\":\"item\",\"id\":\"Q(\\d+)";
      else
        itemPattern =    "^{\"type\":\"item\",\"id\":\"Q(\\d+)";
      var match = Regex.Match(line, itemPattern);
      if (match.Success)
        processItem(line, Int32.Parse(match.Groups[1].Value));
      else {
        match = Regex.Match(line, "^{\"type\":\"property\",\"datatype\":\"([\\w-]+)\",\"id\":\"P(\\d+)");
        if (match.Success)
          processProperty(line, Int32.Parse(match.Groups[2].Value), match.Groups[1].Value);
        else
          throw new Exception
          ("Line " + nLines + " not an item or property: " + line.Substring(0, Math.Min(75, line.Length)));
      }
    }

    private void
    processItem(string line, int id)
    {
      if (items_.ContainsKey(id))
        Console.Out.WriteLine("\r>>>>>> Already have item " + items_[id]);
      var enLabel = getEnLabel(line);
      var item = new Item(id, enLabel);
      items_[id] = item;

      item.instanceOf_ = setToArray(getPropertyValues(item, "instance of", line, 31));
      item.subclassOf_ = setToArray(getPropertyValues(item, "subclass of", line, 279));
      item.partOf_ = setToArray(getPropertyValues(item, "part of", line, 361));
      item.location_ = setToArray(getPropertyValues(item, "location", line, 276));
      item.locatedInTheAdministrativeTerritorialEntity_ = setToArray(getPropertyValues(item, "located in the administrative territorial entity", line, 131));
      item.locatedInTimeZone_ = setToArray(getPropertyValues(item, "located in time zone", line, 421));
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
    processProperty(string line, int id, string datatypeString)
    {
      var enLabel = getEnLabel(line);
      if (enLabel == "")
        messages_.Add("No enLabel for property P" + id);
      if (properties_.ContainsKey(id))
        messages_.Add("Already have property P" + id + " \"" + properties_[id] + "\". Got \"" + enLabel + "\"");
      var property = new Property(id, enLabel);
      properties_[id] = property;

      property.subpropertyOf_ = setToArray(getPropertyValues(null, "subproperty of", line, 1647, true));
      property.datatype_ = getDatatypeFromString(datatypeString);
    }

    private HashSet<int>
    getPropertyValues(object obj, string propertyName, string line, int propertyId, bool objIsProperty)
    {
      var valueSet = new HashSet<int>();

      foreach (Match match in Regex.Matches
        (line, "\"mainsnak\":{\"snaktype\":\"value\",\"property\":\"P" + propertyId +
        "\",\"datavalue\":{\"value\":{\"entity-type\":\"" + (objIsProperty ? "property" : "item") + 
        "\",\"numeric-id\":(\\d+)")) {
        var value = Int32.Parse(match.Groups[1].Value);
        if (objIsProperty)
          // TODO: Check for property self reference.
          valueSet.Add(value);
        else {
          if (value != ((Item)obj).Id)
            valueSet.Add(value);
          else
            messages_.Add("Item is " + propertyName + " itself: " + obj);
        }
      }

      if (valueSet.Count == 0)
        return null;
      else
        return valueSet;
    }

    private HashSet<int>
    getPropertyValues(Item item, string propertyName, string line, int propertyId)
    {
      return getPropertyValues(item, propertyName, line, propertyId, false);
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
      // Find the end quote, skipping escaped characters.
      var quoteOrBackslash = new char[] { '\"', '\\' };
      var iEndQuote = iEnLabelStart;
      while (true) {
        iEndQuote = line.IndexOfAny(quoteOrBackslash, iEndQuote);
        if (line[iEndQuote] == '\"')
          break;
        else
          // Backslash.
          iEndQuote += 2;
      }

      // Include the surrounding quotes.
      var jsonString = line.Substring(iEnLabelStart - 1, (iEndQuote - iEnLabelStart) + 2);
      // Decode the Json value.
      return jsonSerializer_.Deserialize<string>(jsonString);
    }

    private void addAllTransitivePropertyValues
      (HashSet<Item> allPropertyValues, Item item, GetIntArray<Item> getPropertyValues, Item.GetHasLoop getHasLoop)
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
    getPropertyValuesAsSortedItems(int id, GetIntArray<Item> getPropertyValues)
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
    getIndirectPropertyValuesAsSortedItems(int id, GetIntArray<Item> getPropertyValues, Item.GetHasLoop getHasLoop)
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
    public Dictionary<int, Property> properties_ = new Dictionary<int, Property>();

    private Dictionary<int, Item[]> cachedIndirectSubclassOf_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasDirectSubclass_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasIndirectSubclass_ = new Dictionary<int, Item[]>();

    private Dictionary<int, Item[]> cachedIndirectInstanceOf_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasDirectInstance_ = new Dictionary<int, Item[]>();

    private Dictionary<int, Item[]> cachedIndirectPartOf_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasDirectPart_ = new Dictionary<int, Item[]>();
    private Dictionary<int, Item[]> cachedHasIndirectPart_ = new Dictionary<int, Item[]>();

    private static JavaScriptSerializer jsonSerializer_ = new JavaScriptSerializer();

    public delegate void SetIntArray<T>(T obj, int[] values);
    public delegate ICollection<int> GetIntArray<T>(T obj);
  }
}
