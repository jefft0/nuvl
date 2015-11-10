using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Net;
using System.Threading;

namespace Nuvl
{
  public class MediaWiki
  {
    public MediaWiki(string host, string userName, string password)
    {
      host_ = host;

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
    }

    /// <summary>
    /// From the host MediaWiki, fetch the wiki text with the pageTitle.
    /// </summary>
    /// <param name="pageTitle">The wiki page title.</param>
    /// <returns>The wiki text, or null if not found.</returns>
    public string fetchText(string pageTitle)
    {
      var jsonCode = client_.DownloadString
        ("http://" + host_ + "/w/api.php?action=query&prop=revisions&rvprop=content&format=json&titles=" + 
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
            return (string)revision["*"];
        }

        break;
      }

      return null;
    }

    public void setText(string pageTitle, string text)
    {
      editHelper("text", pageTitle, text);
    }

    public void appendText(string pageTitle, string text)
    {
      editHelper("appendText", pageTitle, text);
    }

    /// <summary>
    /// Get the edit token for pageTitle, then send the edit command with the editParameters
    /// </summary>
    /// <param name="parameter">The edit parameter such as "text" or "appendText".</param>
    /// <param name="pageTitle">The page title. This URL encodes the value.</param>
    /// <param name="text">The text. This URL encodes the value.</param>
    private void editHelper(string parameter, string pageTitle, string text)
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
             WebUtility.UrlEncode(pageTitle) + "&recreate&" + parameter + "=" +
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

    private static double getNowMilliseconds()
    {
      return DateTime.Now.Ticks / 10000.0;
    }

    private string host_;
    private WebClient client_;
    private double lastEditMilliseconds_ = 0;
    private const double minEditMilliseconds_ = 2000;
  }
}
