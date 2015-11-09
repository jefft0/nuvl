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
  }
}
