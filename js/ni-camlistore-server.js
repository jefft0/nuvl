var http = require("http");
fs = require('fs');

var server = http.createServer(function(request, response) {
  if (request.method != "GET")
    return;

  if (request.url == "/") {
    sendIndexPage(response);
    return;
  }

  // Match the .well-known URL. Ignore an ending query or hashref.
  var re = /^\/\.well-known\/ni\/sha-1\/([\w\-_]{27,27})\s*((\?|#).*)?$/ ;
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
  var base64 = match[1].replace(/-/g, "+").replace(/_/g, "/");
  var hexHash = new Buffer(base64, "base64").toString("hex");

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

    response.writeHead(200, {"Content-Type": "application/octet-stream"});
    response.write(data);
    response.end();
  });
});
 
function sendIndexPage(response)
{
  response.writeHead(200, {"Content-Type": "text/html"});
  response.write("<!DOCTYPE \"html\">");
  response.write("<html><head><title>data.thefirst.org</title></head><body>\n");
  response.write("<h1>Welcome to data.thefirst.org</h1>\n");
  response.write('You must use Firefox with the "ni" extension. To install it, download<br>\n');
  response.write('<a href="https://github.com/jefft0/nuvl/raw/master/ni-protocol/firefox/ni-protocol.xpi">https://github.com/jefft0/nuvl/raw/master/ni-protocol/firefox/ni-protocol.xpi</a><br>\n');
  response.write('In Firefox, open Tools &gt; Add-ons. In the "gear" or "wrench" menu, click Install Add-on From File and open ni-protocol.xpi. Restart Firefox.<br><br>\n');
  response.write('See <a href="ni://data.thefirst.org/sha-1;zc8SkD6W6iy7bMfD59tmQ80rmsE?ct=application/camlistore">Ranis Party Visit videos</a><br>\n');
  response.write("</body></html>");
  response.end();
}

server.listen(80);
console.log("Server is listening");
