using System;
using System.IO;
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
#if false // Debug all done!
      var fileInventory0 = readBlobFileInventory();
      var videoInventory0 = getCameraVideoInventory(fileInventory0);

      // Count.
      var nVideos = 0;
      foreach (var dayEntry in videoInventory0) {
        foreach (var timeEntry in dayEntry.Value) {
          foreach (var cameraEntry in timeEntry.Value)
            ++nVideos;
        }
      }

      var videoNumber = 0;
      foreach (var dayEntry in videoInventory0) {
        var day = dayEntry.Key;

        foreach (var timeEntry in dayEntry.Value) {
          var time = timeEntry.Key;

          foreach (var cameraEntry in timeEntry.Value) {
            ++videoNumber;

            // We'll finish the line below
            Console.Out.Write(videoNumber + " of " + nVideos + ": ");

            var cameraNumber = cameraEntry.Key;
            var blobName = cameraEntry.Value.BlobName;

            var base64 = blobName.Substring(7);
            var blobFileSubPath = @"sha256\" +
              blobUpperBang(base64.Substring(0, 2)) + @"\" +
              blobUpperBang(base64.Substring(2, 2));
            var blobFileDirectory = Path.Combine(blobsDirectoryPath_, blobFileSubPath);
            var blobFilePath = Path.Combine(blobFileDirectory, blobUpperBang(blobName) + ".dat");

            var toDirectory = getPublicCamerasDirectoryPath(day);
            if (!Directory.Exists(toDirectory))
              Directory.CreateDirectory(toDirectory);
            var toFilePath = Path.Combine(toDirectory, "camera" + cameraNumber + "." + 
              day.ToString("yyyyMMdd") + "_" + time.ToString("hhmmss") + ".mp4");

            if (videoNumber < 60500) { // camera3.20140518_060006
              Console.Out.WriteLine("skip " + toFilePath);
              continue;
            }

            File.Move(blobFilePath, toFilePath);

            Console.Out.WriteLine(toFilePath);
          }
        }
      }
      if (true) return;
#endif
#if true
      var fileInventory0 = readBlobFileInventory();
      var videoInventory0 = getCameraVideoInventory(fileInventory0);

      // Count.
      var fromDirectory = @"N:\BlueIris\New";
      var nVideos = 0;
      foreach (var fileInfo in new DirectoryInfo(fromDirectory).GetFiles()) {
        int camera;
        DateTime date;
        TimeSpan time;
        string fileExtension;
        if (!parseCameraFileName(fileInfo.Name, out camera, out date, out time, out fileExtension))
          continue;
        if (fileExtension != "mp4")
          continue;

        ++nVideos;
      }

      var videoNumber = 0;
      foreach (var fileInfo in new DirectoryInfo(fromDirectory).GetFiles()) {
        int cameraNumber;
        DateTime day;
        TimeSpan time;
        string fileExtension;
        if (!parseCameraFileName(fileInfo.Name, out cameraNumber, out day, out time, out fileExtension))
          continue;
        if (fileExtension != "mp4")
          continue;

        ++videoNumber;

        // We'll finish the line below
        Console.Out.Write(videoNumber + " of " + nVideos + ": " + fileInfo.Name + " -> ");

        var toDirectory = getPublicCamerasDirectoryPath(day);
        if (!Directory.Exists(toDirectory))
          Directory.CreateDirectory(toDirectory);
        var toFilePath = Path.Combine(toDirectory, "camera" + cameraNumber + "." +
          day.ToString("yyyyMMdd") + "_" + time.ToString("hhmmss") + ".mp4");

        File.Move(fileInfo.FullName, toFilePath);

        Console.Out.WriteLine(toFilePath);
      }
      if (true) return;
#endif

      var initialInventorySize = new FileInfo(blobInventoryFilePath_).Length;
      var fileInventory = readBlobFileInventory();
      var videoInventory = getCameraVideoInventory(fileInventory);
      var now = DateTime.Now;
      var newVideoDates = storeNewVideos(videoInventory, now);

      // Refresh the inventory.
      fileInventory = readBlobFileInventory();
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
          storeFile(writeMonthIndexPage(videoInventory, month.Year, month.Month), fileInventory);

        writeVideosIndexPage(fileInventory, videoInventory);
        storeFile(videosIndexPagePath_, fileInventory);
      }

      writeMainIndexPage(fileInventory, videoInventory, now);
      // Sanity check the inventory file size.
      if (new FileInfo(blobInventoryFilePath_).Length < initialInventorySize)
        throw new Exception
          ("ERROR: The inventory file has gotten smaller and may have been overwritten.");
      else
        // Back up the inventory file.
        File.Copy(blobInventoryFilePath_, oneDriveInventoryFilePath_, true);
    }

    private static HashSet<DateTime>
    storeNewVideos(VideoInventory videoInventory, DateTime now)
    {
      var newVideoDates = new HashSet<DateTime>();
      var startOfHour = now.Date.AddHours(now.Hour);

      var directoryPath = @"D:\BlueIris\New";
      foreach (var fileInfo in new DirectoryInfo(directoryPath).GetFiles()) {
        int camera;
        DateTime date;
        TimeSpan time;
        string fileExtension;
        if (!parseCameraFileName(fileInfo.Name, out camera, out date, out time, out fileExtension))
          continue;
        if ((date + time) >= startOfHour)
          // Skip videos being recorded this hour.
          continue;

        SortedDictionary<TimeSpan, Dictionary<int, BlobNameAndType>> timeSet;
        if (videoInventory.TryGetValue(date, out timeSet)) {
          Dictionary<int, BlobNameAndType> cameraSet;
          if (timeSet.TryGetValue(time, out cameraSet)) {
            if (cameraSet.ContainsKey(camera))
              // Already stored the video.
              continue;
          }
        }

        storeFile(fileInfo.FullName, null);
        newVideoDates.Add(date);
      }

      return newVideoDates;
    }

    static string
    storeFile(string sourceFilePath, Dictionary<string, string> fileInventory)
    {
      Console.Out.Write(sourceFilePath + " .");
      var base64 = toBase64(readFileSha256(sourceFilePath));
      var blobName = "sha256-" + base64;

      var blobFileSubPath = @"sha256\" +
        blobUpperBang(base64.Substring(0, 2)) + @"\" +
        blobUpperBang(base64.Substring(2, 2));
      var blobFileDirectory = Path.Combine(blobsDirectoryPath_, blobFileSubPath);
      var blobFilePath = Path.Combine(blobFileDirectory, blobUpperBang(blobName) + ".dat");

      Console.Out.Write(".");
      if (!Directory.Exists(blobFileDirectory))
        Directory.CreateDirectory(blobFileDirectory);
      copyBlob(sourceFilePath, blobFilePath);
      // Sync to OneDrive.
      Console.Out.Write(".");
      syncFile(new FileInfo(blobFilePath), Path.Combine(oneDriveBlobsDirectoryPath_, blobFileSubPath), false);

      // Update the inventory.
      Console.Out.Write(".");
      using (var file = new StreamWriter(blobInventoryFilePath_, true))
        file.WriteLine(blobName + "\t" + sourceFilePath);

      Console.Out.WriteLine(" " + blobName);

      if (fileInventory != null)
        fileInventory[sourceFilePath] = blobName;

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

    static string blobNameToUri(string blobName, string contentType)
    {
      if (blobName.StartsWith("sha256-"))
        return ("ni:///sha-256;" + blobName.Substring(7) + "?ct=" + contentType);
      else
        return null;
    }

    static Dictionary<string, string>
    readBlobFileInventory()
    {
      var tab = new char[] { '\t' };
      var result = new Dictionary<string, string>();

      using (var file = new StreamReader(blobInventoryFilePath_)) {
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
    parseCameraFileName(string filePath, out int camera, out DateTime date, out TimeSpan time, out string fileExtension)
    {
      var match = cameraFileNameRegex_.Match(Path.GetFileName(filePath));
      if (!match.Success) {
        camera = 0;
        date = new DateTime();
        time = new TimeSpan();
        fileExtension = null;
        return false;
      }

      camera = Int32.Parse(match.Groups[1].Value);
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

        int camera;
        DateTime date;
        TimeSpan time;
        string fileExtension;
        if (!parseCameraFileName(filePath, out camera, out date, out time, out fileExtension))
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

        cameraSet[camera] = new BlobNameAndType(blobName, contentTypes_[fileExtension]);
      }

      return result;
    }

    static string
    getMonthIndexPageFilePath(int year, int month)
    {
      return tempDirectoryPath_ + @"\" + "video index " + year + "-" + month.ToString("D2") + ".html";
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
              blobNameToUri(blobNameAndType.BlobName, blobNameAndType.ContentType) + @""">" +
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
      var monthUri = blobNameToUri(monthBlobName, "text/html");

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
    writeMainIndexPage(Dictionary<string, string> fileInventory, VideoInventory videoInventory, DateTime now)
    {
      var videosIndexPageBlobName = fileInventory[videosIndexPagePath_];
      var today = now.Date;

      using (var file = new StreamWriter(@"C:\inetpub\wwwroot\index.htm")) {
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
You must use Firefox with the ""ni"" add-on. To install it, save ni-protocol.xpi to your computer:<br>
<a
 href=""https://github.com/jefft0/nuvl/raw/master/ni-protocol/firefox/ni-protocol.xpi"">https://github.com/jefft0/nuvl/raw/master/ni-protocol/firefox/ni-protocol.xpi</a><br>
Start Firefox and drag ni-protocol.xpi into Firefox. Follow the instructions and restart Firefox.<br>
<br>
<a href=""" + blobNameToUri(videosIndexPageBlobName, "text/html") + @""">All videos by date</a><br><br>
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

    static void syncFile(FileInfo fromFileInfo, string toDirectoryPath, bool verbose)
    {
      var toFileInfo = new FileInfo(Path.Combine(toDirectoryPath, fromFileInfo.Name));
      if (!toFileInfo.Exists) {
        if (verbose)
          Console.Out.WriteLine("Copy " + fromFileInfo.FullName);
        Directory.CreateDirectory(toDirectoryPath);
        fromFileInfo.CopyTo(toFileInfo.FullName);
      }
    }

    /**
     * Replaces all upper-case characters C with C!. Windows filenames are
     * case-insensitive, so this is used to make upper-case letters distinct.
     */
    static string blobUpperBang(string value)
    {
      var result = new StringBuilder();
      foreach (var c in value) {
        result.Append(c);
        if (c >= 'A' && c <= 'Z')
          result.Append('!');
      }

      return result.ToString();
    }

    static string getPublicCamerasDirectoryPath(DateTime day)
    {
      return Path.Combine(publicDirectoryPath_, @"cameras\" + day.Year + @"\" + 
        day.ToString("yyyyMM") + @"\" + day.ToString("yyyyMMdd"));
    }

    static string publicDirectoryPath_ = @"C:\public";
    static string blobsDirectoryPath_ = Path.Combine(publicDirectoryPath_, "blobs");
    static string blobInventoryFilePath_ = Path.Combine(blobsDirectoryPath_, "inventory.tsv");
    static string oneDrivePublicDirectoryPath_ = @"C:\Users\jeff\OneDrive\public";
    static string oneDriveBlobsDirectoryPath_ = Path.Combine(oneDrivePublicDirectoryPath_, "blobs");
    static string oneDriveInventoryFilePath_ = Path.Combine(oneDriveBlobsDirectoryPath_, "inventory.tsv");
    static string tempDirectoryPath_ = @"C:\temp";
    static string videosIndexPagePath_ = tempDirectoryPath_ + @"\videos-index.html";
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
