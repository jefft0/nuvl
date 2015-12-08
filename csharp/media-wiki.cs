using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using System.Net;
using System.Threading;

namespace Nuvl
{
  public class MediaWiki
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="host">The host of the MediaWiki site. If null, disable access to the site.</param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    /// <param name="pagesFilePath"></param>
    public MediaWiki(string host, string userName, string password, string pagesFilePath)
    {
      pagesFilePath_ = pagesFilePath;

      if (host != null) {
        // Log in.
        host_ = host;
        client_ = new CookieAwareWebClient();
        var login1JsonCode = client_.UploadString
          ("http://" + host_ + "/w/api.php?action=login&format=json&lgname=" +
           WebUtility.UrlEncode(userName) + "&lgpassword=" + WebUtility.UrlEncode(password), "");

        var login1Json = jsonSerializer_.Deserialize<Dictionary<string, Object>>(login1JsonCode);
        var login1 = (Dictionary<string, Object>)login1Json["login"];
        var loginToken = (string)login1["token"];

        var login2JsonCode = client_.UploadString
          ("http://" + host_ + "/w/api.php?action=login&format=json&lgname=" +
           WebUtility.UrlEncode(userName) + "&lgpassword=" + WebUtility.UrlEncode(password) +
           "&lgtoken=" + WebUtility.UrlEncode(loginToken), "");
        var login2Json = jsonSerializer_.Deserialize<Dictionary<string, Object>>(login2JsonCode);
        var login2 = (Dictionary<string, Object>)login2Json["login"];
        var result = (string)login2["result"];

        if (result != "Success")
          throw new Exception("Bad login result: " + result);

        // Get the modify tokens.
        var tokenJsonCode = client_.DownloadString
          ("http://" + host_ + "/w/api.php?action=tokens&type=edit|move|delete&format=json");
        var tokenJson = jsonSerializer_.Deserialize<Dictionary<string, Object>>(tokenJsonCode);
        var tokens = (Dictionary<string, Object>)tokenJson["tokens"];
        editToken_ = (string)tokens["edittoken"];
        moveToken_ = (string)tokens["movetoken"];
        deleteToken_ = (string)tokens["deletetoken"];
      }

      readPages();
    }

    public class Page
    {
      public Page(DateTime utcTimeStamp, string text)
      {
        utcTimeStamp_ = utcTimeStamp;
        text_ = text;
      }

      public DateTime getUtcTimeStamp() { return utcTimeStamp_; }

      public string getText() { return text_; }

      private DateTime utcTimeStamp_;
      private string text_;
    }

    public Dictionary<string, Page>
    getPages() { return pages_; }

    /// <summary>
    /// From the host MediaWiki, fetch the wiki page info with the pageTitle.
    /// </summary>
    /// <param name="pageTitle">The wiki page title.</param>
    /// <returns>A Page object with the wiki text and time stamp, or null if not found.</returns>
    public Page 
    fetchPage(string pageTitle)
    {
      if (host_ == null)
        throw new Exception("Cannot access the page because host is null");

      var jsonCode = client_.DownloadString
        ("http://" + host_ + "/w/api.php?action=query&prop=revisions&rvprop=content|timestamp&format=json&titles=" + 
         WebUtility.UrlEncode(pageTitle));

      var json = jsonSerializer_.Deserialize<Dictionary<string, Object>>(jsonCode);
      var query = (Dictionary<string, Object>)json["query"];
      var pages = (Dictionary<string, Object>)query["pages"];
      // Use a loop to get the first entry.
      foreach (Dictionary<string, Object> page in pages.Values) {
        if (page.ContainsKey("revisions")) {
          var revision = (Dictionary<string, Object>)((System.Collections.ArrayList)page["revisions"])[0];
          if (revision.ContainsKey("*"))
            return new Page(DateTime.Parse((string)revision["timestamp"]).ToUniversalTime(), (string)revision["*"]);
        }

        break;
      }

      return null;
    }

    /// <summary>
    /// Get the edit token for pageTitle, then send the edit command with the editParameters
    /// </summary>
    /// <param name="pageTitle">The page title. This URL encodes the value.</param>
    /// <param name="text">The text. This URL encodes the value.</param>
    public void
    setText(string pageTitle, string text)
    {
      if (host_ == null)
        throw new Exception("Cannot access the page because host is null");

      doEditDelay();

      pageTitle = mediaWikiNormalize(pageTitle);
      var responseJsonCode = client_.UploadString
        ("http://" + host_ + "/w/api.php?action=edit&format=json&title=" +
         WebUtility.UrlEncode(pageTitle) + "&bot&recreate&text=" +
         WebUtility.UrlEncode(text) + "&token=" + WebUtility.UrlEncode(editToken_), "");
      var responseJson = jsonSerializer_.Deserialize<Dictionary<string, Object>>(responseJsonCode);
      var edit = (Dictionary<string, Object>)responseJson["edit"];
      var result = (string)edit["result"];

      if (result != "Success")
        throw new Exception("Bad edit result: " + result);

      // Update the local cache.
      pages_[pageTitle] = new Page(getUtcNow(), text);
      writePages();
    }

    public void
    deletePage(string pageTitle, string reason)
    {
      if (host_ == null)
        throw new Exception("Cannot access the page because host is null");

      doEditDelay();

      pageTitle = mediaWikiNormalize(pageTitle);
      var responseJsonCode = client_.UploadString
        ("http://" + host_ + "/w/api.php?action=delete&format=json&title=" + WebUtility.UrlEncode(pageTitle) +
        (reason == null ? "" : "&reason=" + WebUtility.UrlEncode(reason)) +
        "&token=" + WebUtility.UrlEncode(deleteToken_), "");
      var responseJson = jsonSerializer_.Deserialize<Dictionary<string, Object>>(responseJsonCode);
      if (!responseJson.ContainsKey("delete"))
        throw new Exception("Bad delete result: " + responseJsonCode);
      var delete = (Dictionary<string, Object>)responseJson["delete"];
      if (!delete.ContainsKey("logid"))
        throw new Exception("Bad delete result: " + responseJsonCode);

      // Update the local cache.
      pages_.Remove(pageTitle);
      writePages();
    }

    public void
    movePage(string fromPageTitle, string toPageTitle, string reason)
    {
      if (host_ == null)
        throw new Exception("Cannot access the page because host is null");

      doEditDelay();

      fromPageTitle = mediaWikiNormalize(fromPageTitle);
      toPageTitle = mediaWikiNormalize(toPageTitle);
      var responseJsonCode = client_.UploadString
        ("http://" + host_ + "/w/api.php?action=move&format=json&from=" + WebUtility.UrlEncode(fromPageTitle) +
        "&to=" + WebUtility.UrlEncode(toPageTitle) + (reason == null ? "" : "&reason=" + WebUtility.UrlEncode(reason)) +
        "&noredirect&movetalk&ignorewarnings&token=" + WebUtility.UrlEncode(moveToken_), "");
      var responseJson = jsonSerializer_.Deserialize<Dictionary<string, Object>>(responseJsonCode);
      if (!responseJson.ContainsKey("move"))
        throw new Exception("Bad delete result: " + responseJsonCode);
      var move = (Dictionary<string, Object>)responseJson["move"];
      if (!move.ContainsKey("from"))
        throw new Exception("Bad move result: " + responseJsonCode);

      // Update the local cache.
#if false
      pages_[toPageTitle] = pages_[fromPageTitle];
#else // debug: Does the XML dump have the timestamp of the move? See "Property:NIOSH Pocket Guide ID".
      pages_[toPageTitle] = new Page(getUtcNow(), pages_[fromPageTitle].getText());
#endif
      pages_.Remove(fromPageTitle);
      writePages();
    }

    /// <summary>
    /// Read the MediaWiki XML dump and Update the local list of pages with newer entries. 
    /// </summary>
    /// <param name="gzipFilePath">The path of the gzip XML dump.</param>
    public void 
    mergeXmlDump(string gzipFilePath)
    {
      using (var file = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read)) {
        using (var gzip = new GZipStream(file, CompressionMode.Decompress)) {
          using (var reader = XmlReader.Create(gzip)) {
            while (reader.ReadToFollowing("page")) {
              using (var page = reader.ReadSubtree()) {
                page.ReadToFollowing("title");
                var title = mediaWikiNormalize(page.ReadElementContentAsString());

                // Get the timestamp and text of the last revision.
                DateTime utcTimeStamp = new DateTime();
                string text = null;
                while (page.ReadToFollowing("revision")) {
                  page.ReadToFollowing("timestamp");
                  utcTimeStamp = DateTime.Parse(page.ReadElementContentAsString()).ToUniversalTime();
                  page.ReadToFollowing("text");
                  text = page.ReadElementContentAsString();
                }

                if (pages_.ContainsKey(title)) {
                  if (utcTimeStamp > pages_[title].getUtcTimeStamp())
                    // Update to the newer page.
                    pages_[title] = new Page(utcTimeStamp, text);
                }
                else
                  // A new page.
                  pages_[title] = new Page(utcTimeStamp, text);

                // Debug: Check for deleted in the XML dump.
              }
            }
          }
        }
      }

      writePages();
    }

    /// <summary>
    /// Normalize a MediaWiki page title as follows: Capitalize the first string and each substring 
    /// following a colon, change underscore to space, and change multiple spaces to one space.
    /// </summary>
    /// <param name="value">The string to normalize.</param>
    /// <returns>The normalized string</returns>
    public static string
    mediaWikiNormalize(string value)
    {
      string[] splitValue = value.Split(Colon);
      for (int i = 0; i < splitValue.Length; ++i)
        splitValue[i] = splitValue[i].Substring(0, 1).ToUpper() + splitValue[i].Substring(1);

      var result = String.Join(":", splitValue);
      result = result.Replace("_", " ");
      result = multipleSpaces_.Replace(result, " ");

      return result;
    }

    /// <summary>
    /// Sleep as needed to make sure there are minEditMilliseconds_ from the last edit.
    /// </summary>
    private void doEditDelay()
    {
      int sleepMilliseconds = (int)(minEditMilliseconds_ - (getNowMilliseconds() - lastEditMilliseconds_));
      if (sleepMilliseconds > 0)
        Thread.Sleep(sleepMilliseconds);
      lastEditMilliseconds_ = getNowMilliseconds();
    }

    private class CookieAwareWebClient : WebClient
    {
      private CookieContainer cc = new CookieContainer();
      private string lastPage;

      protected override WebRequest GetWebRequest(System.Uri address)
      {
        WebRequest R = base.GetWebRequest(address);
        if (R is HttpWebRequest) {
          HttpWebRequest WR = (HttpWebRequest)R;
          WR.CookieContainer = cc;
          if (lastPage != null) {
            WR.Referer = lastPage;
          }
        }
        lastPage = address.ToString();
        return R;
      }
    }

    private static DateTime
    getUtcNow()
    {
      return DateTime.Now.ToUniversalTime();
    }

    private static double
    getNowMilliseconds()
    {
      return DateTime.Now.Ticks / 10000.0;
    }

    // Read pagesFilePath_ and set pages_.
    private void 
    readPages()
    {
      pages_.Clear();

      if (!File.Exists(pagesFilePath_))
        // Assumet this is the first run.
        return;

      using (var file = new StreamReader(pagesFilePath_)) {
        string line = file.ReadLine();
        if (line != "{")
          throw new Exception("Didn't read expected opening '{' in " + pagesFilePath_);

        while ((line = file.ReadLine()) != null) {
          if (line == "}")
            break;

          // Read the line as a single-entry dictionary.
          var json = jsonSerializer_.Deserialize<Dictionary<string,Dictionary<string, string>>>("{" + line + "}");
          foreach (var entry in json) {
            pages_[entry.Key] = new Page
              (DateTime.Parse(entry.Value["utcTimeStamp"]).ToUniversalTime(), entry.Value["text"]);

            // We processed the one entry.
            break;
          }
        }
      }
    }

    /// <summary>
    /// Save pages_ to pagesFilePath_ as Json.
    /// </summary>
    private void 
    writePages()
    {
      var tempPagesFilePath = pagesFilePath_ + ".temp";
      using (var file = new StreamWriter(tempPagesFilePath)) {
        // Start the dictionary.
        file.WriteLine("{");

        foreach (var entry in pages_) {
          file.Write(jsonSerializer_.Serialize(entry.Key));

          file.Write(":{\"utcTimeStamp\":");
          file.Write(jsonSerializer_.Serialize(entry.Value.getUtcTimeStamp().ToString("s") + "Z"));
          file.Write(",\"text\":");
          file.Write(jsonSerializer_.Serialize(entry.Value.getText()));
          file.WriteLine("}");
        }

        // Finish the dictionary.
        file.WriteLine("}");
      }

      File.Delete(pagesFilePath_);
      File.Move(tempPagesFilePath, pagesFilePath_);
    }

    private string host_ = null;
    private string pagesFilePath_;
    private WebClient client_;
    private double lastEditMilliseconds_ = 0;
    private string editToken_;
    private string moveToken_;
    private string deleteToken_;
    private Dictionary<string, Page> pages_ = new Dictionary<string, Page>();

    private const double minEditMilliseconds_ = 2000;
    private static JavaScriptSerializer jsonSerializer_ = new JavaScriptSerializer();
    private static char[] Colon = new char[] { ':' };
    private static Regex multipleSpaces_ = new Regex("\\s+");
  }
}
