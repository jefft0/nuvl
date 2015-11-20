using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Nuvl
{
  public class SmwWikidataSync
  {
    public SmwWikidataSync(string gzipFilePath)
    {
      using (var file = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read)) {
        using (var gzip = new GZipStream(file, CompressionMode.Decompress)) {
          using (var reader = XmlReader.Create(gzip)) {
            while (reader.ReadToFollowing("page")) {
              using (var page = reader.ReadSubtree()) {
                page.ReadToFollowing("title");
                var title = page.ReadElementContentAsString();
                Console.Out.WriteLine("title: " + title);

                // Get the timestamp and text of the last revision.
                DateTime utcTimeStamp = new DateTime();
                string text = null;
                while (page.ReadToFollowing("revision")) {
                  page.ReadToFollowing("timestamp");
                  utcTimeStamp = DateTime.Parse(page.ReadElementContentAsString()).ToUniversalTime();
                  page.ReadToFollowing("text");
                  text = page.ReadElementContentAsString();
                }
              }
            }
          }
        }
      }
    }

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

    public static string getSmwType(Wikidata.Datatype datatype)
    {
      if (datatype == Wikidata.Datatype.WikibaseItem || datatype == Wikidata.Datatype.WikibaseProperty)
        return "Page";
      else if (datatype == Wikidata.Datatype.GlobeCoordinate)
        return "Geographic coordinate";
      else if (datatype == Wikidata.Datatype.Quantity)
        return "Number";
      else if (datatype == Wikidata.Datatype.Time)
        return "Date";
      else if (datatype == Wikidata.Datatype.Url)
        return "URL";
      else
        // Includes String, MonolingualText, CommonsMedia.
        return "Text";
    }
  }
}
