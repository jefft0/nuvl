package store_videos;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileNotFoundException;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.Writer;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.format.DateTimeFormatter;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import java.util.SortedMap;
import java.util.TreeMap;
import java.util.TreeSet;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class StoreVideos {
  public static void main(String[] args) throws IOException, InterruptedException
  {
    int sleepSeconds = 15;
    System.out.print("Sleeping for " + sleepSeconds + " seconds ...");
    Thread.sleep(sleepSeconds * 1000);
    System.out.println(" done.");

    Map<String, String> fileInventory = readFileInventory
      (filestoreInventoryFilePath_);
    Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> videoInventory =
      getCameraVideoInventory(fileInventory);
    LocalDateTime now = LocalDateTime.now();
    Set<LocalDate> newVideoDates = storeNewVideos(videoInventory, now);

    // Refresh the inventory.
    fileInventory = readFileInventory(filestoreInventoryFilePath_);
    videoInventory = getCameraVideoInventory(fileInventory);

    // Get the months that have been modified with new videos.
    Set<LocalDate> modifiedMonths = new HashSet<>();
/*
    for (LocalDate date : videoInventory.keySet()) { // To make all month indexes.
*/
    for (LocalDate date : newVideoDates) {
      if (!date.isBefore(now.toLocalDate()))
        // We don't update the video index for today's videos.
        continue;

      modifiedMonths.add(LocalDate.of(date.getYear(), date.getMonthValue(), 1));
    }

    if (modifiedMonths.size() > 0) {
      // Update the index pages.
      for (LocalDate month : modifiedMonths)
        writeMonthIndexPage(videoInventory, month.getYear(), month.getMonthValue());

      writeVideosIndexPage(fileInventory, videoInventory);
    }

    writeMainIndexPage(videoInventory, now.toLocalDate());
  }

  static Set<LocalDate>
  storeNewVideos
    (Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> videoInventory,
     LocalDateTime now) throws IOException, InterruptedException
  {
    Set<LocalDate> newVideoDates = new HashSet<>();
    LocalDateTime startOfHour = LocalDateTime.of
      (now.getYear(), now.getMonthValue(), now.getDayOfMonth(), now.getHour(), 0);

    for (File file : new File(newVideosDirectoryPath_).listFiles()) {
      int[] cameraNumber = new int[1];
      LocalDate[] date = new LocalDate[1];
      LocalTime[] time = new LocalTime[1];
      String[] fileExtension = new String[1];
      if (!parseCameraFileName
          (file.getAbsolutePath(), cameraNumber, date, time, fileExtension))
        continue;
      if (!fileExtension[0].equals("mp4"))
        continue;
      if (!LocalDateTime.of(date[0], time[0]).isBefore(startOfHour))
        // Skip videos being recorded this hour.
        continue;

      SortedMap<LocalTime, Map<Integer, BlobNameAndType>> timeSet =
        videoInventory.getOrDefault(date[0], null);
      if (timeSet != null) {
        Map<Integer, BlobNameAndType> cameraSet =
          timeSet.getOrDefault(time[0], null);
        if (cameraSet != null) {
          if (cameraSet.containsKey(cameraNumber[0]))
            // Already stored the video.
            continue;
        }
      }

      System.out.print(file.getAbsolutePath() + " .");
      String toSubDirectory = date[0].format(DateTimeFormatter.ofPattern
        ("yyyy/yyyyMM/yyyyMMdd"));
      File toFileSubPath = new File(toSubDirectory, "camera" + cameraNumber[0] + "." +
        date[0].format(DateTimeFormatter.ofPattern("yyyyMMdd")) + "_" +
        time[0].format(DateTimeFormatter.ofPattern("HHmmss")) + ".mp4");
      File toFile = new File(camerasDirectoryPath_, toFileSubPath.getPath());

      // Copy to public/cameras.
      if (!toFile.getParentFile().exists())
        Files.createDirectory(toFile.getParentFile().toPath());
      safeCopyFile(file.getAbsolutePath(), toFile.getAbsolutePath());

      // Add to IPFS.
      System.out.print(".");
      ipfsFilestoreAdd(toFile.getAbsolutePath());
      System.out.println(" done.");

      newVideoDates.add(date[0]);
    }

    return newVideoDates;
  }

  /**
   * Invoke "ipfs filestore add filePath" and update the inventory file.
   * @param filePath The file path to add.
   * @return The multihash, or null if didn't get a result.
   */
  static String
  ipfsFilestoreAdd(String filePath) throws IOException, InterruptedException
  {
    Process process = Runtime.getRuntime().exec
      (ipfsExecutable_ + " filestore add -q " + filePath);
    BufferedReader reader = new BufferedReader
      (new InputStreamReader(process.getInputStream()));
    String multihash = null;
    String line;
    while ((line = reader.readLine()) != null)
      multihash = line;
    process.waitFor();

    if (multihash == null)
      throw new Error("No output from filestore add " + filePath);

    // Update the inventory file. Using the same format as the cache file of
    // ipfs/go-ipfs/filestore/examples/add-dir .
    try (FileWriter file = new FileWriter(filestoreInventoryFilePath_, true);
         BufferedWriter writer = new BufferedWriter(file)) {
      writer.write
        (multihash + " " + String.format("%.3f", getFileLastModified(filePath)) +
         " " + filePath);
      writer.newLine();
    }

    return multihash;
  }

  /**
   * Run "stat filePath" and return the line starting with "Modify: ".
   * @param filePath The file path to stat.
   * @return The output starting with "Modify: ".
   */
  static String
  getFileStatModified(String filePath) throws IOException, InterruptedException
  {
    Process process = Runtime.getRuntime().exec("stat " + filePath);
    BufferedReader reader = new BufferedReader
      (new InputStreamReader(process.getInputStream()));
    String modified = null;
    String line;
    while ((line = reader.readLine()) != null) {
      if (line.startsWith("Modify: "))
        modified = line.substring("Modify: ".length());
    }
    process.waitFor();

    if (modified == null)
      throw new Error("No output from stat " + filePath);
    return modified;
  }

  /**
   * Parse the output from getFileStatModified(filePath) and return the
   * fraction from the file modified time.
   * @param filePath The file path to stat.
   * @return The fraction.
   */
  static double
  getFileLastModifiedFraction(String filePath) throws IOException, InterruptedException
  {
    Matcher matcher= decimalFractionPattern_.matcher(getFileStatModified(filePath));
    if (!matcher.find())
      return 0.0;
    return Double.parseDouble(matcher.group(0));
  }

  /**
   * Get the last modified time of filePath including the fraction of a second.
   * @param filePath The file path.
   * @return The modified time in seconds including the fraction.
   */
  static double
  getFileLastModified(String filePath) throws IOException, InterruptedException
  {
    return Math.floor(((double)new File(filePath).lastModified()) / 1000) +
      getFileLastModifiedFraction(filePath);
  }

  static void
  safeCopyFile(String sourceFilePath, String toFilePath) throws IOException
  {
    File tempFile = new File(toFilePath + ".temp");
    Files.copy
      (new File(sourceFilePath).toPath(), tempFile.toPath(),
       StandardCopyOption.REPLACE_EXISTING);

    File toFile = new File(toFilePath);
    Files.deleteIfExists(toFile.toPath());
    tempFile.renameTo(toFile);
  }

  /**
   * Read inventoryFilePath and return the inventory.
   * @param inventoryFilePath The cache file to read, containing
   * "hash timestamp filePath".
   * @return A Map where the key is the filePath and the value is the hash.
   */
  static Map<String, String>
  readFileInventory(String inventoryFilePath)
    throws FileNotFoundException, IOException
  {
    Map<String, String> result = new HashMap<>();

    try (FileReader file = new FileReader(inventoryFilePath);
         BufferedReader reader = new BufferedReader(file)) {
      String line;
      while ((line = reader.readLine()) != null) {
        String[] splitLine = line.split(" ", 3);
        String blobName = splitLine[0];
        String filePath = splitLine[2];

        result.put(filePath, blobName);
      }
    }

    return result;
  }

  static boolean
  parseCameraFileName
    (String filePath, int[] cameraNumber, LocalDate[] date, LocalTime[] time,
     String[] fileExtension)
  {
    Matcher matcher = cameraFileNamePattern_.matcher(new File(filePath).getName());
    if (!matcher.find())
      return false;

    cameraNumber[0] = Integer.parseInt(matcher.group(1));
    int year = Integer.parseInt(matcher.group(2));
    int month = Integer.parseInt(matcher.group(3));
    int day = Integer.parseInt(matcher.group(4));
    int hour = Integer.parseInt(matcher.group(5));
    int minute = Integer.parseInt(matcher.group(6));
    int second = Integer.parseInt(matcher.group(7));

    date[0] = LocalDate.of(year, month, day);
    time[0] = LocalTime.of(hour, minute, second);
    fileExtension[0] = matcher.group(8);

    return true;
  }

  // Map<day, SortedMap<time, Map<cameraNumber, BlobNameAndType(blobName, contentType)>>>.
  static Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>>
  getCameraVideoInventory(Map<String, String> fileInventory)
  {
    Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> result =
      new HashMap<>();

    for (Map.Entry<String, String> entry : fileInventory.entrySet()) {
      String filePath = entry.getKey();
      String blobName = entry.getValue();

      int[] cameraNumber = new int[1];
      LocalDate[] date = new LocalDate[1];
      LocalTime[] time = new LocalTime[1];
      String[] fileExtension = new String[1];
      if (!parseCameraFileName
          (filePath, cameraNumber, date, time, fileExtension))
        continue;

      SortedMap<LocalTime, Map<Integer, BlobNameAndType>> timeSet =
        result.getOrDefault(date[0], null);
      if (timeSet == null) {
        timeSet = new TreeMap<>();
        result.put(date[0], timeSet);
      }

      Map<Integer, BlobNameAndType> cameraSet = timeSet.getOrDefault(time[0], null);
      if (cameraSet == null) {
        cameraSet = new HashMap<>();
        timeSet.put(time[0], cameraSet);
      }

      cameraSet.put
        (cameraNumber[0], new BlobNameAndType(blobName, contentTypes_.get(fileExtension[0])));
    }

    return result;
  }

  static String
  getMonthIndexPageFilePath(int year, int month)
  {
    return new File
      (wwwrootDirectoryPath_,
       "videos-index-" + year + "-" + String.format("%02d", month) + ".html").getAbsolutePath();
  }

  static String
  writeMonthIndexPage
    (Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> videoInventory,
     int year, int month) throws IOException
  {
    LocalDate firstOfMonth = LocalDate.of(year, month, 1);
    int daysInMonth = firstOfMonth.plusMonths(1).plusDays(-1).getDayOfMonth();
    String monthName = firstOfMonth.format(DateTimeFormatter.ofPattern("MMMM"));

    // Get the days for the month.
    Map<Integer, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> daySet =
      new HashMap<>();
    for (Map.Entry<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> entry : videoInventory.entrySet()) {
      if (entry.getKey().getYear() == year &&
          entry.getKey().getMonthValue() == month)
        daySet.put(entry.getKey().getDayOfMonth(), entry.getValue());
    }

    // We make a grid with 7 columns. The week starts on a Monday.
    // Get the grid index of the first of the month.
    int day1GridIndex = (firstOfMonth.getDayOfWeek().getValue() - 1) % 7;
    if (day1GridIndex < 0)
      day1GridIndex += 7;

    String filePath = getMonthIndexPageFilePath(year, month);
    try (FileWriter file = new FileWriter(filePath);
         BufferedWriter writer = new BufferedWriter(file)) {
      writer.write(
"<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\">\n" +
"<html>\n" +
"<head>\n" +
"  <meta content=\"text/html; charset=ISO-8859-1\"\n" +
" http-equiv=\"Content-Type\">\n" +
"  <title>Jeff's House Videos - " + monthName + " " + year + "</title>\n" +
"</head>\n" +
"<body>\n" +
"<a href=\"http://data.thefirst.org\">Home</a>\n" +
"<h1>" + monthName + " " + year + "</h1>\n" +
"<br>\n" +
"<table style=\"text-align: left;\" border=\"1\" cellpadding=\"0\"\n" +
" cellspacing=\"0\">\n" +
"  <tbody>\n" +
"    <tr>\n" +
"      <td style=\"vertical-align: top;\">Monday</td>\n" +
"      <td style=\"vertical-align: top;\">Tuesday</td>\n" +
"      <td style=\"vertical-align: top;\">Wednesday</td>\n" +
"      <td style=\"vertical-align: top;\">Thursday</td>\n" +
"      <td style=\"vertical-align: top;\">Friday</td>\n" +
"      <td style=\"vertical-align: top;\">Saturday</td>\n" +
"      <td style=\"vertical-align: top;\">Sunday</td>\n" +
"    </tr>\n");
      boolean haveMoreDays = true;
      for (int iRow = 0; haveMoreDays; ++iRow) {
        // Start the week.
        writer.write("    <tr>\n");

        for (int iColumn = 0; iColumn < 7; ++iColumn) {
          int gridIndex = 7 * iRow + iColumn;
          int day = 1 + gridIndex - day1GridIndex;
          if (day >= daysInMonth)
            haveMoreDays = false;

          if (day < 1 || day > daysInMonth) {
            // Not a day. Leave blank.
            writer.write("      <td><br></td>\n");
            continue;
          }

          // Start a cell for the day.
          writer.write(
"      <td style=\"vertical-align: top;\"><a name=\"" + day + "\"/><b>" + day + "</b><br>\n");

          SortedMap<LocalTime, Map<Integer, BlobNameAndType>> timeSet =
            daySet.getOrDefault(day, null);
          if (timeSet != null)
            // Only show the table if there are videos for today.
            writeDayVideosTable(writer, timeSet);

          // Finish the cell for the day.
          writer.write("      </td>\n");
        }

        // Finish the week.
        writer.write("    </tr>\n");
      }

      // Finish the page.
      writer.write(
"  </tbody>\n" +
"</table>\n" +
"</body>\n" +
"</html>\n");
    }

    return filePath;
  }

  static void writeDayVideosTable
    (Writer writer, SortedMap<LocalTime, Map<Integer, BlobNameAndType>> timeSet) throws IOException
  {
    String[] cameraBackgroundColor = new String[] {
      "", "", "", "rgb(255, 255, 255);", "rgb(255, 255, 204);", "rgb(255, 204, 204);", "rgb(204, 255, 255);"};
    String[] cameraName = new String[] {
      "", "", "", "Living<br>Room", "Bed<br>Room", "Bath<br>Room", "Bath<br>Room"};

    // Make the table with each camera and the times.
    writer.write(
"      <table style=\"text-align: left;\" border=\"0\" cellpadding=\"3\" cellspacing=\"0\">\n" +
"        <tbody>\n" +
"          <tr>\n");
    // Write the camera names.
    for (int camera = 3; camera <= 6; ++camera)
      writer.write(
"            <td style=\"vertical-align: top; background-color: " +
        cameraBackgroundColor[camera] + "\">" + cameraName[camera] +
        "<br>cam" + camera + "<br></td>\n");
    writer.write(
"          </tr>\n" +
"          <tr>\n");

    // Write the camera times.
    for (int camera = 3; camera <= 6; ++camera) {
      writer.write(
"            <td style=\"vertical-align: top; background-color: " +
        cameraBackgroundColor[camera] + "\">\n");
      for (Map.Entry<LocalTime, Map<Integer, BlobNameAndType>> entry : timeSet.entrySet()) {
        BlobNameAndType blobNameAndType = entry.getValue().getOrDefault
          (camera, null);
        if (blobNameAndType == null)
          // No video for the camera at this time.
          writer.write("              <br>\n");
        else {
          writer.write("              <a href=\"" +
            "fs:/ipfs/" + blobNameAndType.BlobName + "\">" +
            String.format("%02d", entry.getKey().getHour()) + ":" +
            String.format("%02d", entry.getKey().getMinute()) +
            (entry.getKey().getSecond() != 0 ?
              ":" + String.format("%02d", entry.getKey().getSecond()) : "") +
            "</a><br>\n");
        }
      }
     writer.write("            </td>\n");
    }

    // Finish the table with the camera names and times.
    writer.write(
"          </tr>\n" +
"        </tbody>\n" +
"      </table>\n");
  }

  static void
  writeVideosIndexPage
    (Map<String, String> fileInventory,
     Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> videoInventory) throws IOException
  {
    try (FileWriter file = new FileWriter(videosIndexPagePath_);
         BufferedWriter writer = new BufferedWriter(file)) {
      // Start the page.
      writer.write(
"<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\">\n" +
"<html>\n" +
"<head>\n" +
"  <meta content=\"text/html; charset=ISO-8859-1\"\n" +
" http-equiv=\"Content-Type\">\n" +
"  <title>Jeff's House Videos Index</title>\n" +
"</head>\n" +
"<body>\n" +
"<a href=\"http://data.thefirst.org\">Home</a>\n" +
"<h1>Jeff's House Videos Index</h1>\n" +
"Click on a date below then click on a time to see a\n" +
"video. Each is about 200 MB, but should start streaming in Firefox.\n");

      // Get the years for which we have videos.
      TreeSet<Integer> yearSet = new TreeSet<>(Collections.reverseOrder());
      for (Map.Entry<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> entry : videoInventory.entrySet())
        yearSet.add(entry.getKey().getYear());

      for (int year : yearSet) {
        // Start the year table.
        writer.write(
"<h2>" + year + "</h2>\n" +
"<table style=\"text-align: left;\" border=\"1\" cellpadding=\"2\" cellspacing=\"0\">\n" +
"  <tbody>\n");

        writer.write("    <tr>\n");
        for (int month = 1; month <= 6; ++month)
          writeMonthIndexCell(fileInventory, videoInventory, year, month, writer);
        writer.write("    </tr>\n");

        writer.write("    <tr>\n");
        for (int month = 7; month <= 12; ++month)
          writeMonthIndexCell(fileInventory, videoInventory, year, month, writer);
        writer.write("    </tr>\n");

        // Finish the year table.
        writer.write(
"  </tbody>\n" +
"</table>\n");
      }

      // Finish the page.
      writer.write(
"</body>\n" +
"</html>");
    }
  }

  static void
  writeMonthIndexCell
    (Map<String, String> fileInventory,
     Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> videoInventory,
     int year, int month, Writer writer) throws IOException
  {
    String monthUri = new File(getMonthIndexPageFilePath(year, month)).getName();

    // Get the days in the month that have a video.
    Set<Integer> daySet = new HashSet<>();
    for (Map.Entry<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> entry : videoInventory.entrySet()) {
      if (entry.getKey().getYear() == year && entry.getKey().getMonthValue() == month)
        daySet.add(entry.getKey().getDayOfMonth());
    }
    if (daySet.size() == 0)
      return;

    LocalDate firstOfMonth = LocalDate.of(year, month, 1);
    int daysInMonth = firstOfMonth.plusMonths(1).plusDays(-1).getDayOfMonth();
    String monthName = firstOfMonth.format(DateTimeFormatter.ofPattern("MMMM"));

    // We make a grid with 7 columns. The week starts on a Monday.
    // Get the grid index of the first of the month.
    int day1GridIndex = (firstOfMonth.getDayOfWeek().getValue() - 1) % 7;
    if (day1GridIndex < 0)
      day1GridIndex += 7;

    // Start the cell and month table.
    writer.write(
"      <td style=\"vertical-align: top;\">\n" +
"      <table style=\"text-align: left;\" border=\"0\" cellpadding=\"2\" cellspacing=\"0\">\n" +
"        <tbody>\n" +
"          <tr>\n" +
"            <td colspan=\"7\"" +
" style=\"text-align: center; vertical-align: top;\"><a href=\"" +
                monthUri + "\">" + monthName + " " + year + "</a><br>\n" +
"            </td>\n" +
"          </tr>\n" +
"          <tr>\n" +
"            <td style=\"vertical-align: top;\">M<br></td>\n" +
"            <td style=\"vertical-align: top;\">T<br></td>\n" +
"            <td style=\"vertical-align: top;\">W<br></td>\n" +
"            <td style=\"vertical-align: top;\">T<br></td>\n" +
"            <td style=\"vertical-align: top;\">F<br></td>\n" +
"            <td style=\"vertical-align: top;\">S<br></td>\n" +
"            <td style=\"vertical-align: top;\">S<br></td>\n" +
"          </tr>\n");
    boolean haveMoreDays = true;
    for (int iRow = 0; haveMoreDays; ++iRow) {
      // Start the week.
      writer.write("          <tr>\n");

      for (int iColumn = 0; iColumn < 7; ++iColumn) {
        int gridIndex = 7 * iRow + iColumn;
        int day = 1 + gridIndex - day1GridIndex;
        if (day >= daysInMonth)
          haveMoreDays = false;

        if (day < 1 || day > daysInMonth) {
          // Not a day. Leave blank.
          writer.write("            <td><br></td>\n");
          continue;
        }

        // Show the day.
        if (daySet.contains(day))
          writer.write
            ("            <td style=\"vertical-align: top;\"><a href=\"" +
             monthUri + "#" + day + "\">" + day + "</td>\n");
        else
          // No videos for the day.
          writer.write
            ("            <td style=\"vertical-align: top;\">" + day + "</td>\n");
      }

      // Finish the week.
      writer.write("          </tr>\n");
    }

    // Finish the month table and cell.
    writer.write(
"        </tbody>\n" +
"      </table>\n" +
"      </td>\n");
  }

  static void
  writeMainIndexPage
    (Map<LocalDate, SortedMap<LocalTime, Map<Integer, BlobNameAndType>>> videoInventory,
     LocalDate today) throws IOException
  {
    String videosIndexPageUri = new File(videosIndexPagePath_).getName();

    try (FileWriter file = new FileWriter(new File(wwwrootDirectoryPath_, "index.html"));
         BufferedWriter writer = new BufferedWriter(file)) {
      writer.write(
"<!DOCTYPE HTML PUBLIC \"html\">\n<html>\n<head>\n" +
"  <meta http-equiv=\"content-type\" content=\"text/html; charset=windows-1252\">\n" +
"  <title>data.thefirst.org</title>\n</head>\n" +
"<body>\n" +
"<h1>Welcome to data.thefirst.org</h1>\n" +
"You must use Firefox with the <a" +
" href=\"https://addons.mozilla.org/en-US/firefox/addon/ipfs-gateway-redirect/\">IPFS add-on</a>.\n" +
"Click the IPFS icon in the Firefox toolbar and click \"Open Preferences\". Set \"Custom Gateway Host\" to \"data.thefirst.org\".<br><br>\n" +
"<a href=\"" + videosIndexPageUri + "\">All videos by date</a><br><br>\n" +
"Videos for today, " + today.format(DateTimeFormatter.ofPattern("d MMMM, yyyy")) + ":<br>\n");

      SortedMap<LocalTime, Map<Integer, BlobNameAndType>> timeSet =
        videoInventory.getOrDefault(today, null);
      if (timeSet == null)
        writer.write("(no videos yet)<br>\n");
      else
        writeDayVideosTable(writer, timeSet);

      writer.write(
"</body>\n</html>\n");
    }
  }

  /**
   * A BlobNameAndType has a blob name like
   * "QmRkea8uweXqfUhjPi6zXQ2SinXFwsovqsZQvzsKRLJY4n" and a content type like
   * "video/mp4".
   */
  static class BlobNameAndType
  {
    public BlobNameAndType(String blobName, String contentType)
    {
      BlobName = blobName;
      ContentType = contentType;
    }

    public final String BlobName;
    public final String ContentType;
  }

  static final String publicDirectoryPath_ = "/public";
  static final String camerasDirectoryPath_ = new File
    (publicDirectoryPath_, "cameras").getAbsolutePath();
  static final String ipfsConfigDirectoryPath_ = "/home/jeff/.ipfs";
  static final String filestoreInventoryFilePath_ = new File
    (ipfsConfigDirectoryPath_, "cameras.cache").getAbsolutePath();
  static final String wwwrootDirectoryPath_ = "/var/www/html";
  static final String videosIndexPagePath_ = new File
    (wwwrootDirectoryPath_, "videos-index.html").getAbsolutePath();
  static final String newVideosDirectoryPath_ = "/media/CAMERAS/New";
  static final String ipfsExecutable_ = "/home/jeff/work/go/bin/ipfs";
  static final Pattern cameraFileNamePattern_ = Pattern.compile
    ("^camera(\\d{1})\\.(\\d{4})(\\d{2})(\\d{2})_(\\d{2})(\\d{2})(\\d{2})\\.(mp4|avi)$");
  static final Pattern decimalFractionPattern_ = Pattern.compile("\\.\\d*");
  static final Map<String, String> contentTypes_ = new HashMap<>();
  static {
    contentTypes_.put("mp4", "video/mp4");
    contentTypes_.put("avi", "video/avi");
  }
}
