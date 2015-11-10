using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Nuvl
{
  class SmwWikidataSync
  {
    public static int getWikidataItemId(string wikiText)
    {
      var match = Regex.Match(wikiText, "{{\\w*WikidataItem\\w*\\|(\\d+)}}");
      if (match.Success)
        return Int32.Parse(match.Groups[1].Value);
      else
        return -1;
    }

    public static int getWikidataPropertyId(string wikiText)
    {
      var match = Regex.Match(wikiText, "{{\\w*WikidataProperty\\w*\\|(\\d+)}}");
      if (match.Success)
        return Int32.Parse(match.Groups[1].Value);
      else
        return -1;
    }

    public static string getSmwType(string wikidataDatatype)
    {
      if (wikidataDatatype == "wikibase-item")
        return "Page";
      else if (wikidataDatatype == "globe-coordinate")
        return "Geographic coordinate";
      else if (wikidataDatatype == "quantity")
        return "Number";
      else if (wikidataDatatype == "time")
        return "Date";
      else if (wikidataDatatype == "url")
        return "URL";
      // TODO: wikibase-property?
      else
        // Includes string, monolingualtext, commonsMedia.
        return "Text";
    }
  }
}
