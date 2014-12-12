var http = require("http");
fs = require('fs');

var server = http.createServer(function(request, response) {
  if (request.method != "GET")
    return;

  // Match the .well-known URL. Ignore an ending query or hashref.
  var re = /^\/\.well-known\/ni\/sha-1\/([\w]{27,27})\s*((\?|#).*)?$/ ;
  var match = request.url.match(re);

  if (!match) {
    response.writeHead(400, {"Content-Type": "text/html"});
    response.write("<!DOCTYPE \"html\">");
    response.write("<html><head><title>404 Not Found</title></head>");
    response.write("<body><h1>Not Found</h1><p>The requested URL ");
    response.write(request.url);
    response.write(" was not found on this server.</p></body></html>");
    response.end();
    return;
  }

  var hashAlgorithm = "sha1";
  var hexHash = new Buffer(match[1], "base64").toString("hex");

  var firstByte = parseInt(hexHash.substr(0, 2), 16);
  var driveLetter;
  if (firstByte < 128)
    driveLetter = "F";
  else
    driveLetter = "J";
  blobPath = driveLetter + ":\\Camlistore\\blobs\\sha1\\" + 
    hexHash.substr(0, 2) + "\\" + hexHash.substr(2, 2) + "\\" + 
    hashAlgorithm + "-" + hexHash + ".dat"; 

  fs.readFile(blobPath, function (err, data) {
    if (err) 
      // TODO: Return an error page.
        return;

    //response.writeHead(200, {"Content-Type": "application/octet-stream"});
    response.writeHead(200, {"Content-Type": "text/plain"}); // Debug: For demo.
    response.write(data);
    response.end();
  });
});
 
server.listen(80);
console.log("Server is listening");
