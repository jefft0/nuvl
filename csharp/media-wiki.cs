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
    public MediaWiki(string host, string userName, string password, string pagesFilePath)
    {
      host_ = host;
      pagesFilePath_ = pagesFilePath;

      client_ = new CookieAwareWebClient();
      var login1JsonCode = client_.UploadString
        ("http://" + host_ + "/w/api.php?action=login&format=json&lgname=" +
         WebUtility.UrlEncode(userName) + "&lgpassword=" + WebUtility.UrlEncode(password), "");

      var serializer = new JavaScriptSerializer();
      var login1Json = serializer.Deserialize<Dictionary<string, Object>>(login1JsonCode);
      var login1 = (Dictionary<string, Object>)login1Json["login"];
      var loginToken = (string)login1["token"];

      var login2JsonCode = client_.UploadString
        ("http://" + host_ + "/w/api.php?action=login&format=json&lgname=" + 
         WebUtility.UrlEncode(userName) + "&lgpassword=" + WebUtility.UrlEncode(password) + 
         "&lgtoken=" + WebUtility.UrlEncode(loginToken), "");
      var login2Json = serializer.Deserialize<Dictionary<string, Object>>(login2JsonCode);
      var login2 = (Dictionary<string, Object>)login2Json["login"];
      var result = (string)login2["result"];

      if (result != "Success")
        throw new Exception("Bad login result: " + result);

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
      var jsonCode = client_.DownloadString
        ("http://" + host_ + "/w/api.php?action=query&prop=revisions&rvprop=content|timestamp&format=json&titles=" + 
         WebUtility.UrlEncode(pageTitle));

      var serializer = new JavaScriptSerializer();
      var json = serializer.Deserialize<Dictionary<string, Object>>(jsonCode);
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

    public void 
    setText(string pageTitle, string text)
    {
      editHelper("text", pageTitle, text);
    }

    public void 
    appendText(string pageTitle, string text)
    {
      editHelper("appendtext", pageTitle, text);
    }

    /// <summary>
    /// Read the MediaWiki XML dump and Update the local list of pages with newer entries. 
    /// </summary>
    /// <param name="gzipFilePath">The path of the gzip XML dump.</param>
    public void 
    readXmlDump(string gzipFilePath)
    {
      using (var file = new FileStream(gzipFilePath, FileMode.Open, FileAccess.Read)) {
        using (var gzip = new GZipStream(file, CompressionMode.Decompress)) {
          using (var reader = XmlReader.Create(gzip)) {
            while (reader.ReadToFollowing("page")) {
              using (var page = reader.ReadSubtree()) {
                page.ReadToFollowing("title");
                var title = mediaWikiCapitalize(page.ReadElementContentAsString());

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
              }
            }
          }
        }
      }

      writePages();
    }


    /// <summary>
    /// Get the edit token for pageTitle, then send the edit command with the editParameters
    /// </summary>
    /// <param name="parameter">The edit parameter such as "text" or "appendText".</param>
    /// <param name="pageTitle">The page title. This URL encodes the value.</param>
    /// <param name="text">The text. This URL encodes the value.</param>
    private void 
    editHelper(string parameter, string pageTitle, string text)
    {
      int sleepMilliseconds = (int)(minEditMilliseconds_ - (getNowMilliseconds() - lastEditMilliseconds_));
      if (sleepMilliseconds > 0)
        Thread.Sleep(sleepMilliseconds);
      lastEditMilliseconds_ = getNowMilliseconds();

      var tokenJsonCode = client_.DownloadString
        ("http://" + host_ + 
         "/w/api.php?action=query&prop=info|revisions&intoken=edit&rvprop=timestamp&format=json&titles=" + 
         WebUtility.UrlEncode(pageTitle));
      var serializer = new JavaScriptSerializer();
      var tokenJson = serializer.Deserialize<Dictionary<string, Object>>(tokenJsonCode);
      var query = (Dictionary<string, Object>)tokenJson["query"];
      var pages = (Dictionary<string, Object>)query["pages"];
      string edittoken = "";
      // Use a loop to get the first entry.
      foreach (Dictionary<string, Object> page in pages.Values) {
        if (page.ContainsKey("edittoken")) {
          edittoken = ((string)page["edittoken"]);

          var responseJsonCode = client_.UploadString
            ("http://" + host_ + "/w/api.php?action=edit&format=json&title=" +
             WebUtility.UrlEncode(pageTitle) + "&bot&recreate&" + parameter + "=" +
             WebUtility.UrlEncode(text) + "&token=" + WebUtility.UrlEncode(edittoken), "");
          var responseJson = serializer.Deserialize<Dictionary<string, Object>>(responseJsonCode);
          var edit = (Dictionary<string, Object>)responseJson["edit"];
          var result = (string)edit["result"];

          if (result != "Success")
            throw new Exception("Bad edit result: " + result);
          return;
        }

        break;
      }

      throw new Exception("Can't get page data.");
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
      using (var file = new StreamWriter(pagesFilePath_)) {
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
    }

    /// <summary>
    /// Capitalize the first string and each substring following a colon.
    /// </summary>
    /// <param name="value">The string to capitalize.</param>
    /// <returns>The capitalized string</returns>
    private static string 
    mediaWikiCapitalize(string value)
    {
      string[] splitValue = value.Split(Colon);
      for (int i = 0; i < splitValue.Length; ++i)
        splitValue[i] = splitValue[i].Substring(0, 1).ToUpper() + splitValue[i].Substring(1);

      return String.Join(":", splitValue);
    }

    private string host_;
    private string pagesFilePath_;
    private WebClient client_;
    private double lastEditMilliseconds_ = 0;
    private Dictionary<string, Page> pages_ = new Dictionary<string, Page>();

    private const double minEditMilliseconds_ = 2000;
    private static JavaScriptSerializer jsonSerializer_ = new JavaScriptSerializer();
    private static char[] Colon = new char[] { ':' };
  }
}
