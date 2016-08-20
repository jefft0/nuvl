using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Nuvl
{
  public class SmwWikidataSync
  {
    public SmwWikidataSync(Wikidata wikidata, string host, string userName, string password, string pagesFilePath)
    {
      wikidata_ = wikidata;
      mediaWiki_ = new MediaWiki(host, userName, password, pagesFilePath);

      resyncPageInfo();
    }

    public class PageInfo
    {
      public int itemId_;
      public int propertyId_;
    }

    
    /// <summary>
    /// Read the MediaWiki XML dump and Update the local list of pages with newer entries. 
    /// </summary>
    /// <param name="gzipFilePath">The path of the gzip XML dump.</param>
    public void
    mergeXmlDump(string gzipFilePath) { mediaWiki_.mergeXmlDump(gzipFilePath); }

    public void
    syncProperties()
    {
      try {
        var renamedProperties = new List<string[]>();
        var question = "Rename the following properties?\r\n";

        // Do moves first in case a property ID was renamed to a new name, but a new property was
        // created with the old name.
        foreach (var entry in wikidata_.properties_) {
          string expectedTitle = "Property:" + MediaWiki.mediaWikiNormalize(entry.Value.getEnLabelOrId());
          string propertyIdPageTitle;
          if (propertyIdPageTitle_.TryGetValue(entry.Key, out propertyIdPageTitle)) {
            if (propertyIdPageTitle != expectedTitle) {
              // Debug: Check for SMW references to propertyIdPageTitle.
              renamedProperties.Add(new string[] { propertyIdPageTitle, expectedTitle });
              question += "P" + entry.Key + " " + propertyIdPageTitle + " -> " + expectedTitle + "\r\n";
            }
          }
        }

        if (renamedProperties.Count > 0) {
          if (MessageBox.Show(question, "Rename properties?", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;

          foreach (var entry in renamedProperties) {
            // Debug: First mediawiki_.fetchPage to see if the page was already moved.
            Console.Out.WriteLine("Rename " + entry[0] + " -> " + entry[1]);
            mediaWiki_.movePage(entry[0], entry[1], "Renamed in Wikidata");
          }

          resyncPageInfo();
        }

#if false
        // Now look for deleted properties.
        foreach (var entry in propertyIdPageTitle_) {
          if (!wikidata_.properties_.ContainsKey(entry.Key)) {
            // Debug: Check for SMW references to entry.Value.
            Console.Out.WriteLine("Debug: Was removed from Wikidata: P" + entry.Key + " " + entry.Value);
            mediaWiki_.deletePage(entry.Value, "P" + entry.Key + " was deleted from Wikidata");
          }
        }
        // resyncPageInfo();
#endif

        // Now look for new properties.
        foreach (var entry in wikidata_.properties_) {
          string expectedTitle = "Property:" + MediaWiki.mediaWikiNormalize(entry.Value.getEnLabelOrId());
          string propertyIdPageTitle;
          if (!propertyIdPageTitle_.TryGetValue(entry.Key, out propertyIdPageTitle)) {
            Console.Out.WriteLine("New in Wikidata: " + expectedTitle);
            // Debug: First mediawiki_.fetchPage to see if the page was already created.
            mediaWiki_.setText(expectedTitle, getPropertyText(entry.Key));
          }
        }

        // TODO: Check for changed text.
      }
      finally {
        resyncPageInfo();
      }
    }

    /// <summary>
    /// Clear pageInfo_ and propertyIdPageTitle_, then set them from mediaWiki_.getPages().
    /// </summary>
    private void resyncPageInfo()
    {
      pageInfo_.Clear();
      propertyIdPageTitle_.Clear();

      foreach (var entry in mediaWiki_.getPages()) {
        if (entry.Value.getText() == null)
          // The page is deleted.
          continue;

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

    /// <summary>
    /// Get the SMW text for wikidata_.properties_[propertyId].
    /// </summary>
    /// <param name="propertyId">The wikidata property ID.</param>
    /// <returns>The SMW text.</returns>
    private string
    getPropertyText(int propertyId)
    {
      var property = wikidata_.properties_[propertyId];

      var text = "{{WikidataProperty|" + propertyId + "}}\n[[has type::" + getSmwType(property.datatype_) + "| ]]\n";
      if (property.subpropertyOf_ != null) {
        foreach (var subpropertyOf in property.subpropertyOf_) {
          // TODO: Check for subproperty loops.
          if (wikidata_.properties_.ContainsKey(subpropertyOf))
            text += "[[subproperty of::" + wikidata_.properties_[subpropertyOf].getEnLabelOrId() + "| ]]\n";
        }
      }

      return text;
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

    private Wikidata wikidata_;
    private MediaWiki mediaWiki_;
    private Dictionary<string, PageInfo> pageInfo_ = new Dictionary<string, PageInfo>();
    private Dictionary<int, string> propertyIdPageTitle_ = new Dictionary<int, string>();
  }
}
