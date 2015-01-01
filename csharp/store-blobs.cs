using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Dictionary<day, SortedDictionary<time, Dictionary<cameraNumber, blobName>>>.
using VideoInventory = System.Collections.Generic.Dictionary<System.DateTime, System.Collections.Generic.SortedDictionary<System.TimeSpan, System.Collections.Generic.Dictionary<int, string>>>;
// Dictionary<filePath, blobName>.
using FileInventory = System.Collections.Generic.Dictionary<string, string>;

namespace StoreBlobs
{
  class StoreBlobs
  {
    static void 
    Main(string[] args)
    {
#if true
      var fileInventory = readFileInventory();
      var videoInventory = getCameraVideoInventory(fileInventory);
      var monthPagePath = makeMonthIndexPage(videoInventory, 2014, 11);
      //storeFile(monthPagePath);
      makeVideosIndexPage(fileInventory, videoInventory);
#endif
#if false
      var re = new Regex("^camera(\\d{1})\\.(\\d{4})(\\d{2})(\\d{2})_(\\d{2})(\\d{2})(\\d{2})\\.mp4$");

      foreach (var directoryPath in new string[] {
                 @"G:\cameras\2014\camera3",
                 @"G:\cameras\2014\camera4",
                 @"G:\cameras\2014\camera5",
                 @"G:\cameras\2014\camera6"
               }) {
        foreach (var fileName in new DirectoryInfo(directoryPath).GetFiles()) {
          var match = re.Match(fileName.ToString());
          if (!match.Success)
            continue;

          var year = Int32.Parse(match.Groups[2].Value);
          var month = Int32.Parse(match.Groups[3].Value);
          var day = Int32.Parse(match.Groups[4].Value);
          if (!(year == 2014 && month == 11))
            continue;

          storeFile(directoryPath + @"\" + fileName);
        }
      }
#endif
    }

    static string 
    storeFile(string sourceFilePath)
    {
      Console.Out.Write(sourceFilePath + " .");
      var base64 = toBase64(readFileSha256(sourceFilePath));
      var blobName = "sha256-" + base64;

      var blobsDirectory = @"C:\public\blobs";
      var blobFileDirectory = blobsDirectory + @"\sha256\" +
        base64.Substring(0, 2).ToLower() + @"\" +
        base64.Substring(2, 2).ToLower();
      var blobFilePath = blobFileDirectory + @"\" + blobName + ".dat";

      Console.Out.Write(".");
      if (!Directory.Exists(blobFileDirectory))
        Directory.CreateDirectory(blobFileDirectory);
      copyBlob(sourceFilePath, blobFilePath);

      // Update the inventory.
      Console.Out.Write(".");
      using (var file = new StreamWriter(inventoryFilePath_, true))
        file.WriteLine(blobName + "\t" + sourceFilePath);

      Console.Out.WriteLine(" " + blobName);

      return blobName;
    }

    static void 
    copyBlob(string sourceFilePath, string blobFilePath)
    {
      // TODO: Check if blobFilePath exists and is the same file.

      var tempFilePath = blobFilePath + ".temp";
      File.Copy(sourceFilePath, tempFilePath);

      if (File.Exists(blobFilePath))
        File.Delete(blobFilePath);
      File.Move(tempFilePath, blobFilePath);
    }

    static byte[] 
    readFileSha256(string filePath)
    {
      using (var file = File.OpenRead(filePath))
        return SHA256Managed.Create().ComputeHash(file);
    }

    static string 
    toBase64(byte[] binary)
    {
      return System.Convert.ToBase64String(binary)
        .Replace("=", "").Replace('+', '-').Replace('/', '_');
    }

    static string blobNameToUri(string blobName)
    {
      if (blobName.StartsWith("sha256-"))
        return ("ni:///sha-256;" + blobName.Substring(7));
      else
        return null;
    }

    static FileInventory
    readFileInventory()
    {
      var tab = new char[] { '\t' };
      var result = new FileInventory();

      using (var file = new StreamReader(inventoryFilePath_)) {
        var line = "";
        while ((line = file.ReadLine()) != null) {
          var splitLine = line.Split(tab);
          var blobName = splitLine[0];
          var filePath = splitLine[1];

          result[filePath] = blobName;
        }
      }

      return result;
    }

    static VideoInventory
    getCameraVideoInventory(FileInventory fileInventory)
    {
      var re = new Regex("^camera(\\d{1})\\.(\\d{4})(\\d{2})(\\d{2})_(\\d{2})(\\d{2})(\\d{2})\\.mp4$");
      var result = new VideoInventory();

      foreach (var entry in fileInventory) {
        var filePath = entry.Key;
        var blobName = entry.Value;

        var match = re.Match(Path.GetFileName(filePath));
        if (!match.Success)
          continue;

        var camera = Int32.Parse(match.Groups[1].Value);
        var year = Int32.Parse(match.Groups[2].Value);
        var month = Int32.Parse(match.Groups[3].Value);
        var day = Int32.Parse(match.Groups[4].Value);
        var hour = Int32.Parse(match.Groups[5].Value);
        var minute = Int32.Parse(match.Groups[6].Value);
        var second = Int32.Parse(match.Groups[7].Value);

        var date = new DateTime(year, month, day);
        var time = new TimeSpan(hour, minute, second);

        SortedDictionary<TimeSpan, Dictionary<int, string>> timeSet;
        if (!result.TryGetValue(date, out timeSet)) {
          timeSet = new SortedDictionary<TimeSpan, Dictionary<int, string>>();
          result[date] = timeSet;
        }

        Dictionary<int, string> cameraSet;
        if (!timeSet.TryGetValue(time, out cameraSet)) {
          cameraSet = new Dictionary<int, string>();
          timeSet[time] = cameraSet;
        }

        cameraSet[camera] = blobName;
      }

      return result;
    }

    static string
    getMonthIndexPageFilePath(int year, int month)
    {
      return tempDirectory_ + @"\" + "video index " + year + "-" + month.ToString("D2") + ".html";
    }

    static string
    makeMonthIndexPage(VideoInventory videoInventory, int year, int month)
    {
      var firstOfMonth = new DateTime(year, month, 1);
      var daysInMonth = DateTime.DaysInMonth(year, month);
      var monthName = firstOfMonth.ToString("MMMM");
      var cameraBackgroundColor = new string[] { 
        "", "", "", "rgb(255, 255, 255);", "rgb(255, 255, 204);", "rgb(255, 204, 204);", "rgb(204, 255, 255);"};
      var cameraName = new string[] { 
        "", "", "", "Living<br>Room", "Bed<br>Room", "Bath<br>Room", "Bath<br>Room"};

      // Get the days for the month.
      var daySet = new Dictionary<int, SortedDictionary<TimeSpan, Dictionary<int, string>>>();
      foreach (var entry in videoInventory) {
        if (entry.Key.Year == year && entry.Key.Month == month)
          daySet[entry.Key.Day] = entry.Value;
      }

      // We make a grid with 7 columns. The week starts on a Monday.
      // Get the grid index of the first of the month.
      var day1GridIndex = (int)firstOfMonth.DayOfWeek - 1 % 7;
      if (day1GridIndex < 0)
        day1GridIndex += 7;

      var filePath = getMonthIndexPageFilePath(year, month);
      using (var file = new StreamWriter(filePath)) {
        file.Write(
@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01 Transitional//EN"">
<html>
<head>
  <meta content=""text/html; charset=ISO-8859-1""
 http-equiv=""Content-Type"">
  <title>Jeff's House Videos - " + monthName + " " + year + @"</title>
</head>
<body>
<a href=""http://data.thefirst.org"">Home</a>
<h1>" + monthName + " " + year + @"</h1>
<br>
<table style=""text-align: left;"" border=""1"" cellpadding=""0""
 cellspacing=""0"">
  <tbody>
    <tr>
      <td style=""vertical-align: top;"">Monday</td>
      <td style=""vertical-align: top;"">Tuesday</td>
      <td style=""vertical-align: top;"">Wednesday</td>
      <td style=""vertical-align: top;"">Thursday</td>
      <td style=""vertical-align: top;"">Friday</td>
      <td style=""vertical-align: top;"">Saturday</td>
      <td style=""vertical-align: top;"">Sunday</td>
    </tr>
");
        var haveMoreDays = true;
        for (var iRow = 0; haveMoreDays; ++iRow) {
          // Start the week.
          file.WriteLine("    <tr>");

          for (var iColumn = 0; iColumn < 7; ++iColumn) {
            var gridIndex = 7 * iRow + iColumn;
            var day = 1 + gridIndex - day1GridIndex;
            if (day >= daysInMonth)
              haveMoreDays = false;

            if (day < 1 || day > daysInMonth) {
              // Not a day. Leave blank.
              file.WriteLine("      <td><br></td>");
              continue;
            }

            // Show the day.
            file.Write(
@"      <td style=""vertical-align: top;""><a name=""" + day + @"""/><b>" + day + @"</b><br>
");

            SortedDictionary<TimeSpan, Dictionary<int, string>> timeSet;
            if (!daySet.TryGetValue(day, out timeSet)) {
              // No videos for today. Just finish the cell.
              file.WriteLine("      </td>");
              continue;
            }

            // Make the table with each camera and the times.
            file.Write(
@"      <table style=""text-align: left;"" border=""0"" cellpadding=""3"" cellspacing=""0"">
        <tbody>
          <tr>
");
            // Write the camera names.
            for (var camera = 3; camera <= 6; ++camera)
              file.WriteLine(@"            <td style=""vertical-align: top; background-color: " +
                cameraBackgroundColor[camera] + @""">" + cameraName[camera] + "<br>cam" + camera + "<br></td>");
            file.Write(
@"          </tr>
          <tr>
");

            // Write the camera times.
            for (var camera = 3; camera <= 6; ++camera) {
              file.WriteLine(@"            <td style=""vertical-align: top; background-color: " +
                cameraBackgroundColor[camera] + @""">");
              foreach (var entry in timeSet) {
                string blobName;
                if (!entry.Value.TryGetValue(camera, out blobName))
                  // No video for the camera at this time.
                  file.WriteLine("              <br>");
                else
                  file.WriteLine(@"              <a href=""" + blobNameToUri(blobName) + @""">" + 
                    entry.Key.Hours.ToString("D2") + ":" + entry.Key.Minutes.ToString("D2") +
                    (entry.Key.Seconds != 0 ? ":" + entry.Key.Seconds.ToString("D2") : "") + "</a><br>");
              }
              file.WriteLine("            </td>");
            }

            // Finish the table with the camera names and times.
            file.Write(
@"          </tr>
        </tbody>
      </table>
      </td>
");
          }

          // Finish the week.
          file.WriteLine("    </tr>");
        }

        // Finish the page.
        file.Write(
@"  </tbody>
</table>
</body>
</html>
");
      }

      return filePath;
    }

    static string
    makeVideosIndexPage(FileInventory fileInventory, VideoInventory videoInventory)
    {
      var filePath = tempDirectory_ + @"\videos-index.html";
      using (var file = new StreamWriter(filePath)) {
        // Start the page.
        file.Write(
@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01 Transitional//EN"">
<html>
<head>
  <meta content=""text/html; charset=ISO-8859-1""
 http-equiv=""Content-Type"">
  <title>Jeff's House Videos Index</title>
</head>
<body>
<a href=""http://data.thefirst.org"">Home</a>
<h1>Jeff's House Videos Index</h1>
You must use Firefox with the ""ni"" add-on. To install it, download<br>
<a
 href=""https://github.com/jefft0/nuvl/raw/master/ni-protocol/firefox/ni-protocol.xpi"">https://github.com/jefft0/nuvl/raw/master/ni-protocol/firefox/ni-protocol.xpi</a><br>
In Firefox, open Tools &gt; Add-ons. In the ""gear"" or ""wrench"" menu,
click Install Add-on From File and open ni-protocol.xpi. Restart
Firefox.<br>
<br>
After installing the ""ni"" add-on. Click on a date below to see the
videos. Each is about 200 MB, but should start streaming in Firefox.<br>
");

        // Get the years for which we have videos.
        var yearSet = new SortedSet<int>();
        foreach (var entry in videoInventory)
          yearSet.Add(entry.Key.Year);

        foreach (var year in yearSet) {
          // Start the year table.
          file.Write(
@"<h2>" + year + @"</h2>
<table style=""text-align: left;"" border=""1"" cellpadding=""2"" cellspacing=""0"">
  <tbody>
");

          file.WriteLine("    <tr>");
          for (var month = 1; month <= 6; ++month)
            writeMonthIndexCell(fileInventory, videoInventory, year, month, file);
          file.WriteLine("    </tr>");

          file.WriteLine("    <tr>");
          for (var month = 7; month <= 12; ++month)
            writeMonthIndexCell(fileInventory, videoInventory, year, month, file);
          file.WriteLine("    </tr>");

          // Finish the year table.
          file.Write(
@"  </tbody>
</table>
");
        }

        // Finish the page.
        file.Write(
@"</body>
</html>
");
      }

      return filePath;
    }

    private static void 
    writeMonthIndexCell(FileInventory fileInventory, VideoInventory videoInventory, int year, int month, StreamWriter file)
    {
      string monthBlobName;
      if (!fileInventory.TryGetValue(getMonthIndexPageFilePath(year, month), out monthBlobName))
        return;

      // Get the days in the month that have a video.
      var daySet = new HashSet<int>();
      foreach (var entry in videoInventory) {
        if (entry.Key.Year == year && entry.Key.Month == month)
          daySet.Add(entry.Key.Day);
      }
      if (daySet.Count == 0)
        return;

      var firstOfMonth = new DateTime(year, month, 1);
      var daysInMonth = DateTime.DaysInMonth(year, month);
      var monthName = firstOfMonth.ToString("MMMM");
      var monthUri = blobNameToUri(monthBlobName);

      // We make a grid with 7 columns. The week starts on a Monday.
      // Get the grid index of the first of the month.
      var day1GridIndex = (int)firstOfMonth.DayOfWeek - 1 % 7;
      if (day1GridIndex < 0)
        day1GridIndex += 7;

      // Start the cell and month table.
      file.Write(
@"      <td style=""vertical-align: top;"">
      <table style=""text-align: left;"" border=""0"" cellpadding=""2"" cellspacing=""0"">
        <tbody>
          <tr>
            <td colspan=""7""
 style=""text-align: center; vertical-align: top;""><a href=""" +
                monthUri + @"?ct=text/html"">" + monthName + " " + year + @"</a><br>
            </td>
          </tr>
          <tr>
            <td style=""vertical-align: top;"">M<br></td>
            <td style=""vertical-align: top;"">T<br></td>
            <td style=""vertical-align: top;"">W<br></td>
            <td style=""vertical-align: top;"">T<br></td>
            <td style=""vertical-align: top;"">F<br></td>
            <td style=""vertical-align: top;"">S<br></td>
            <td style=""vertical-align: top;"">S<br></td>
          </tr>
");
      var haveMoreDays = true;
      for (var iRow = 0; haveMoreDays; ++iRow) {
        // Start the week.
        file.WriteLine("          <tr>");

        for (var iColumn = 0; iColumn < 7; ++iColumn) {
          var gridIndex = 7 * iRow + iColumn;
          var day = 1 + gridIndex - day1GridIndex;
          if (day >= daysInMonth)
            haveMoreDays = false;

          if (day < 1 || day > daysInMonth) {
            // Not a day. Leave blank.
            file.WriteLine("            <td><br></td>");
            continue;
          }

          // Show the day.
          if (daySet.Contains(day))
            file.WriteLine(@"            <td style=""vertical-align: top;""><a href=""" +
              monthUri + "?ct=text/html#" + day + @""">" + day + "</td>");
          else
            // No videos for the day.
            file.WriteLine(@"            <td style=""vertical-align: top;"">" + day + "</td>");
        }

        // Finish the week.
        file.WriteLine("          </tr>");
      }

      // Finish the month table and cell.
      file.Write(
@"        </tbody>
      </table>
      </td>
");
    }

    static string inventoryFilePath_ = @"C:\public\blobs\inventory.tsv";
    static string tempDirectory_ = @"C:\temp";
  }
}
