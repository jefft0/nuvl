using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Nuvl
{
  public class SmwWikidataSync
  {
    public SmwWikidataSync(string host, string userName, string password, string pagesFilePath)
    {
      mediaWiki_ = new MediaWiki(host, userName, password, pagesFilePath);

      foreach (var entry in mediaWiki_.getPages()) {
        var pageInfo = new PageInfo();
        pageInfo.itemId_ = getItemId(entry.Value.getText(), entry.Key);
        pageInfo.propertyId_ = getPropertyId(entry.Value.getText(), entry.Key);

        if (entry.Key.StartsWith("Property:")) {
          if (pageInfo.itemId_ > 0)
            throw new Exception
              ("Item ID " + pageInfo.itemId_ + " in property page " + entry.Key);

          if (pageInfo.propertyId_ >= 0) {
            if (propertyIdPageTitle_.ContainsKey(pageInfo.propertyId_))
              throw new Exception
                ("Property ID " + pageInfo.propertyId_ + " in multiple pages \"" +
                 propertyIdPageTitle_[pageInfo.propertyId_] + " and " +
                 entry.Key);

            propertyIdPageTitle_[pageInfo.propertyId_] = entry.Key;
          }
        }
        else {
          if (pageInfo.propertyId_ > 0)
          throw new Exception
            ("Property ID " + pageInfo.propertyId_ + " in non-property page " + entry.Key);
        }

        pageInfo_[entry.Key] = pageInfo;
      }
    }

    public class PageInfo
    {
      public int itemId_;
      public int propertyId_;
    }

    /// <summary>
    /// Get the value of WikidataItem in wikiText.
    /// </summary>
    /// <param name="wikiText">The wiki text.</param>
    /// <param name="pageTitle">The page title for displaying an error.</param>
    /// <returns>The result value, or -1 if not found.</returns>
    /// <exception cref="Exception">If multiple values are found (an unusual
    /// situation which must be resolved immediately).</exception>
    private static int
    getItemId(string wikiText, string pageTitle)
    {
      var result = -1;

      foreach (Match match in Regex.Matches(wikiText, "{{\\w*WikidataItem\\w*\\|(\\d+)}}")) {
        if (result >= 0)
          throw new Exception("Multiple WikidataItem in page " + pageTitle);
        result = Int32.Parse(match.Groups[1].Value);
      }

      return result;
    }

    /// <summary>
    /// Get the value of WikidataProperty in wikiText.
    /// </summary>
    /// <param name="wikiText">The wiki text.</param>
    /// <param name="pageTitle">The page title for displaying an error.</param>
    /// <returns>The result value, or -1 if not found.</returns>
    /// <exception cref="Exception">If multiple values are found (an unusual
    /// situation which must be resolved immediately).</exception>
    private static int
    getPropertyId(string wikiText, string pageTitle)
    {
      var result = -1;

      foreach (Match match in Regex.Matches(wikiText, "{{\\w*WikidataProperty\\w*\\|(\\d+)}}")) {
        if (result >= 0)
          throw new Exception("Multiple WikidataProperty in page " + pageTitle);
        result = Int32.Parse(match.Groups[1].Value);
      }

      return result;
    }

    public static string 
    getSmwType(Wikidata.Datatype datatype)
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

    /* debug private */ public MediaWiki mediaWiki_;
    private Dictionary<string, PageInfo> pageInfo_ = new Dictionary<string, PageInfo>();
    public Dictionary<int, string> propertyIdPageTitle_ = new Dictionary<int, string>();
  }
}
