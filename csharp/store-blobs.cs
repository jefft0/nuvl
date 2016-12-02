using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StoreBlobs
{
  // Dictionary<day, SortedDictionary<time, Dictionary<cameraNumber, new BlobNameAndType(blobName, contentType)>>>.
  using VideoInventory = Dictionary<DateTime, SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>>>;

  class VideoFile
  {
    public VideoFile(string filePath, DateTime day, TimeSpan time, int cameraNumber)
    {
      FilePath = filePath;
      Day = day;
      Time = time;
      CameraNumber = cameraNumber;
    }

    public readonly string FilePath;
    public readonly DateTime Day;
    public readonly TimeSpan Time;
    public readonly int CameraNumber;
  }

  class StoreBlobs
  {
    static void
    Main(string[] args)
    {
#if false // Refresh the file inventory.
      using (var output = new StreamWriter(filestoreInventoryFilePath_)) {
        using (var file = new StreamReader(@"c:\temp\ls-files.out")) {
          var line = "";
          while ((line = file.ReadLine()) != null) {
            var splitLine = line.Split(new char[] { ' ' });
            output.WriteLine(splitLine[1] + "\t" + splitLine[0]);
          }
        }
      }
      if (true) return;
#endif

      var fileInventory = readFileInventory(filestoreInventoryFilePath_);
      var videoInventory = getCameraVideoInventory(fileInventory);
      var now = DateTime.Now;
      var newVideoDates = storeNewVideos(videoInventory, now);

#if false // Don't make HTML files.
      // Refresh the inventory.
      fileInventory = readFileInventory(filestoreInventoryFilePath_);
      videoInventory = getCameraVideoInventory(fileInventory);

      // Get the months that have been modified with new videos.
      var modifiedMonths = new HashSet<DateTime>();
      foreach (var date in newVideoDates) {
        if (date >= now.Date)
          // We don't update the video index for today's videos.
          continue;

        modifiedMonths.Add(new DateTime(date.Year, date.Month, 1));
      }

      if (modifiedMonths.Count > 0) {
        // Update the index pages.
        foreach (var month in modifiedMonths)
          writeMonthIndexPage(videoInventory, month.Year, month.Month);

        writeVideosIndexPage(fileInventory, videoInventory);
      }

      writeMainIndexPage(videoInventory, now);
#endif
    }

    private static HashSet<DateTime>
    storeNewVideos(VideoInventory videoInventory, DateTime now)
    {
      var newVideoDates = new HashSet<DateTime>();
      var startOfHour = now.Date.AddHours(now.Hour);

      var directoryPath = @"D:\BlueIris\New";
      foreach (var fileInfo in new DirectoryInfo(directoryPath).GetFiles()) {
        int cameraNumber;
        DateTime date;
        TimeSpan time;
        string fileExtension;
        if (!parseCameraFileName(fileInfo.Name, out cameraNumber, out date, out time, out fileExtension))
          continue;
        if (fileExtension != "mp4")
          continue;
        if ((date + time) >= startOfHour)
          // Skip videos being recorded this hour.
          continue;

        SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>> timeSet;
        if (videoInventory.TryGetValue(date, out timeSet)) {
          Dictionary<int, BlobNameAndType> cameraSet;
          if (timeSet.TryGetValue(time, out cameraSet)) {
            if (cameraSet.ContainsKey(cameraNumber))
              // Already stored the video.
              continue;
          }
        }

        Console.Out.Write(fileInfo.FullName + " .");
        var toSubDirectory = date.Year + @"\" + date.ToString("yyyyMM") + @"\" + date.ToString("yyyyMMdd");
        var toFileSubPath = Path.Combine(toSubDirectory, "camera" + cameraNumber + "." +
          date.ToString("yyyyMMdd") + "_" + time.ToString("hhmmss") + ".mp4");
        var toFileInfo = new FileInfo(Path.Combine(camerasDirectoryPath_, toFileSubPath));

#if false // Don't copy to public/cameras.
        // Copy to public/cameras.
        if (!Directory.Exists(toFileInfo.DirectoryName))
          Directory.CreateDirectory(toFileInfo.DirectoryName);
        safeCopyFile(fileInfo.FullName, toFileInfo.FullName);
#endif

        if (date.Year >= 2016) {
          // Sync to OneDrive.
          Console.Out.Write(".");
          var toOneDriveFileInfo = new FileInfo(Path.Combine(oneDriveCamerasDirectoryPath_, toFileSubPath));
          if (!Directory.Exists(toOneDriveFileInfo.DirectoryName))
            Directory.CreateDirectory(toOneDriveFileInfo.DirectoryName);
          fileInfo.CopyTo(toOneDriveFileInfo.FullName, true);
        }

#if false // Don't add to IPFS
        Console.Out.Write(".");
        var startTime = DateTime.Now;
        ipfsFilestoreAdd(toFileInfo.FullName);
#else
        // Update the inventory file.
        File.AppendAllText(filestoreInventoryFilePath_, "omitted\t" + toFileInfo.FullName + "\r\n");
#endif

        Console.Out.WriteLine(" done.");

        newVideoDates.Add(date);
      }

      return newVideoDates;
    }

#if false // Don't add to IPFS
    /// <summary>
    /// Invoke "ipfs filestore add filePath" and update the inventory file.
    /// </summary>
    /// <param name="filePath">The file path to add.</param>
    /// <returns>The multihash, or null if didn't get a result.</returns>
    static string ipfsFilestoreAdd(string filePath)
    {
      var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = @"C:\work\go\bin\ipfs.exe",
          Arguments = "filestore add -q \"" + filePath + "\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        }
      };

      string multihash = null;

      process.Start();
      while (!process.StandardOutput.EndOfStream) {
        multihash = process.StandardOutput.ReadLine().Trim();
      }

      if (multihash == null)
        throw new Exception("No output from filestore add " + filePath);

      // Update the inventory file.
      File.AppendAllText(filestoreInventoryFilePath_, multihash + "\t" + filePath + "\r\n");

      return multihash;
    }
#endif

#if false // Don't copy to public/cameras.
    static void 
    safeCopyFile(string sourceFilePath, string toFilePath)
    {
      var tempFilePath = toFilePath + ".temp";
      File.Copy(sourceFilePath, tempFilePath, true);

      if (File.Exists(toFilePath))
        File.Delete(toFilePath);
      File.Move(tempFilePath, toFilePath);
    }
#endif

    /// <summary>
    /// Read inventoryFilePath and return the inventory.
    /// </summary>
    /// <param name="inventoryFilePath">The TSV file to read, containing hash\tfilePath</param>
    /// <returns>A dictionary where the key is the filePath and the value is the hash.</returns>
    static Dictionary<string, string>
    readFileInventory(string inventoryFilePath)
    {
      var tab = new char[] { '\t' };
      var result = new Dictionary<string, string>();

      using (var file = new StreamReader(inventoryFilePath)) {
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

    static bool
    parseCameraFileName(string filePath, out int cameraNumber, out DateTime date, out TimeSpan time, out string fileExtension)
    {
      var match = cameraFileNameRegex_.Match(Path.GetFileName(filePath));
      if (!match.Success) {
        cameraNumber = 0;
        date = new DateTime();
        time = new TimeSpan();
        fileExtension = null;
        return false;
      }

      cameraNumber = Int32.Parse(match.Groups[1].Value);
      var year = Int32.Parse(match.Groups[2].Value);
      var month = Int32.Parse(match.Groups[3].Value);
      var day = Int32.Parse(match.Groups[4].Value);
      var hour = Int32.Parse(match.Groups[5].Value);
      var minute = Int32.Parse(match.Groups[6].Value);
      var second = Int32.Parse(match.Groups[7].Value);

      date = new DateTime(year, month, day);
      time = new TimeSpan(hour, minute, second);
      fileExtension = match.Groups[8].Value;

      return true;
    }

    static VideoInventory
    getCameraVideoInventory(Dictionary<string, string> fileInventory)
    {
      var result = new VideoInventory();

      foreach (var entry in fileInventory) {
        var filePath = entry.Key;
        var blobName = entry.Value;

        int cameraNumber;
        DateTime date;
        TimeSpan time;
        string fileExtension;
        if (!parseCameraFileName(filePath, out cameraNumber, out date, out time, out fileExtension))
          continue;

        SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>> timeSet;
        if (!result.TryGetValue(date, out timeSet)) {
          timeSet = new SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>>();
          result[date] = timeSet;
        }

        Dictionary<int, BlobNameAndType> cameraSet;
        if (!timeSet.TryGetValue(time, out cameraSet)) {
          cameraSet = new Dictionary<int, BlobNameAndType>();
          timeSet[time] = cameraSet;
        }

        cameraSet[cameraNumber] = new BlobNameAndType(blobName, contentTypes_[fileExtension]);
      }

      return result;
    }

#if false // Don't make HTML files.
    static string
    getMonthIndexPageFilePath(int year, int month)
    {
      return Path.Combine(wwwrootDirectoryPath_, "videos-index-" + year + "-" + month.ToString("D2") + ".html");
    }

    static string
    writeMonthIndexPage(VideoInventory videoInventory, int year, int month)
    {
      var firstOfMonth = new DateTime(year, month, 1);
      var daysInMonth = DateTime.DaysInMonth(year, month);
      var monthName = firstOfMonth.ToString("MMMM");

      // Get the days for the month.
      var daySet = new Dictionary<int, SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>>>();
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

            // Start a cell for the day.
            file.Write(
@"      <td style=""vertical-align: top;""><a name=""" + day + @"""/><b>" + day + @"</b><br>
");

            SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>> timeSet;
            if (daySet.TryGetValue(day, out timeSet))
              // Only show the table if there are videos for today.
              writeDayVideosTable(file, timeSet);

            // Finish the cell for the day.
            file.WriteLine("      </td>");
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

    static void writeDayVideosTable(StreamWriter file, SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>> timeSet)
    {
      var cameraBackgroundColor = new string[] { 
        "", "", "", "rgb(255, 255, 255);", "rgb(255, 255, 204);", "rgb(255, 204, 204);", "rgb(204, 255, 255);"};
      var cameraName = new string[] { 
        "", "", "", "Living<br>Room", "Bed<br>Room", "Bath<br>Room", "Bath<br>Room"};

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
          BlobNameAndType blobNameAndType;
          if (!entry.Value.TryGetValue(camera, out blobNameAndType))
            // No video for the camera at this time.
            file.WriteLine("              <br>");
          else
            file.WriteLine(@"              <a href=""" +
              "fs:/ipfs/" + blobNameAndType.BlobName + @""">" +
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
");
    }

    static void
    writeVideosIndexPage(Dictionary<string, string> fileInventory, VideoInventory videoInventory)
    {
      using (var file = new StreamWriter(videosIndexPagePath_)) {
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
Click on a date below then click on a time to see a
video. Each is about 200 MB, but should start streaming in Firefox.
");

        // Get the years for which we have videos.
        var yearSet = new SortedSet<int>();
        foreach (var entry in videoInventory)
          yearSet.Add(entry.Key.Year);

        foreach (var year in yearSet.Reverse()) {
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
    }

    static void 
    writeMonthIndexCell(Dictionary<string, string> fileInventory, VideoInventory videoInventory, int year, int month, StreamWriter file)
    {
#if false
      string monthBlobName;
      if (!fileInventory.TryGetValue(getMonthIndexPageFilePath(year, month), out monthBlobName))
        return;
      var monthUri = blobNameToUri(monthBlobName, "text/html");
#else
      var monthUri = Path.GetFileName(getMonthIndexPageFilePath(year, month));
#endif

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
                monthUri + @""">" + monthName + " " + year + @"</a><br>
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
              monthUri + "#" + day + @""">" + day + "</td>");
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

    static void
    writeMainIndexPage(VideoInventory videoInventory, DateTime now)
    {
      var videosIndexPageUri = Path.GetFileName(videosIndexPagePath_);
      var today = now.Date;

      using (var file = new StreamWriter(Path.Combine(wwwrootDirectoryPath_, "index.htm"))) {
        file.Write(
@"<!DOCTYPE HTML PUBLIC ""html"">
<html>
<head>
  <meta http-equiv=""content-type""
 content=""text/html; charset=windows-1252"">
  <title>data.thefirst.org</title>
</head>
<body>
<h1>Welcome to data.thefirst.org</h1>
You must use Firefox with the <a
 href=""https://addons.mozilla.org/en-US/firefox/addon/ipfs-gateway-redirect/"">IPFS add-on</a>.
Click the IPFS icon in the Firefox toolbar and click ""Open Preferences"". Set ""Custom Gateway Host"" to ""data.thefirst.org"".<br>
<br>
<a href=""" + videosIndexPageUri + @""">All videos by date</a><br><br>
Videos for today, " + today.ToString("d MMMM, yyyy") + @":<br>
");

        SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>> timeSet;
        if (!videoInventory.TryGetValue(today, out timeSet))
          file.WriteLine("(no videos yet)<br>");
        else
          writeDayVideosTable(file, timeSet);

        file.Write(
@"</body>
</html>
");
      }
    }
#endif

    static string publicDirectoryPath_ = @"C:\public";
    static string camerasDirectoryPath_ = Path.Combine(publicDirectoryPath_, "cameras");
    static string oneDrivePublicDirectoryPath_ = @"C:\Users\jeff\OneDrive\public";
    static string oneDriveCamerasDirectoryPath_ = Path.Combine(oneDrivePublicDirectoryPath_, "cameras");
    static string ipfsConfigDirectoryPath_ = @"C:\Users\jeff\.ipfs";
    static string filestoreInventoryFilePath_ = Path.Combine(ipfsConfigDirectoryPath_, "filestoreInventory.tsv");
#if false // Don't make HTML files.
    static string wwwrootDirectoryPath_ = @"C:\inetpub\wwwroot";
    static string videosIndexPagePath_ = Path.Combine(wwwrootDirectoryPath_, "videos-index.html");
#endif
    static Regex cameraFileNameRegex_ = new Regex("^camera(\\d{1})\\.(\\d{4})(\\d{2})(\\d{2})_(\\d{2})(\\d{2})(\\d{2})\\.(mp4|avi)$");
    // The key is a filename extension like "mp4". The value is a content type like "video/mp4".
    static Dictionary<string, string> contentTypes_ = new Dictionary<string, string>() { { "mp4", "video/mp4" }, { "avi", "video/avi" } };
  }

  /// <summary>
  /// A BlobNameAndType has a blob name like "sha256-2nLdZ2oWoNcUqMK1blUpaZf1aIHSVMTGR7afLeyiOmo"
  /// and a content type like "video/mp4".
  /// </summary>
  class BlobNameAndType
  {
    public BlobNameAndType(string blobName, string contentType)
    {
      BlobName = blobName;
      ContentType = contentType;
    }

    public readonly string BlobName;
    public readonly string ContentType;
  }
}
