/*
 * @author: Jeff Thompson
 * This is the ni protocol handler.
 * Protocol handling code derived from http://mike.kaply.com/2011/01/18/writing-a-firefox-protocol-handler/
 */

const Cc = Components.classes;
const Ci = Components.interfaces;
const Cr = Components.results;

const nsIProtocolHandler = Ci.nsIProtocolHandler;

Components.utils.import("resource://gre/modules/XPCOMUtils.jsm");
Components.utils.import("resource://gre/modules/NetUtil.jsm");

function NiProtocol() {
}

NiProtocol.prototype = {
  scheme: "ni",
  protocolFlags: nsIProtocolHandler.URI_NORELATIVE |
                 nsIProtocolHandler.URI_NOAUTH |
                 nsIProtocolHandler.URI_LOADABLE_BY_ANYONE,

  newURI: function(aSpec, aOriginCharset, aBaseURI)
  {
    var uri = Cc["@mozilla.org/network/simple-uri;1"].createInstance(Ci.nsIURI);

    // We have to trim now because nsIURI converts spaces to %20 and we can't trim in newChannel.
    var uriParts = NiProtocol.splitUri(aSpec);
    var niParts = NiProtocol.splitHierPart(uriParts.name);
    if (niParts == null)
      // This doesn't seem to be a well-formed ni URI.
      return null;
    else {
      var authority = niParts.authority;
      if (authority == "" && aBaseURI != null) {
        // Try to get the authority from the base URI.
        if (aBaseURI.scheme == "ni") {
          var baseNiParts = NiProtocol.splitHierPart(NiProtocol.splitUri(aBaseURI.spec).name);
          if (baseNiParts != null)
            authority = baseNiParts.authority;
        }
        else if (aBaseURI.scheme == "http" || aBaseURI.scheme == "https")
          authority = aBaseURI.hostPort;
      }

      if (authority == "")
        // Returning null should prevent newChannel from being called.
        return null;

      // Reconstruct the trimmed URI.
      uri.spec = "ni://" + authority + '/' + niParts.algorithm + ';' + niParts.value + uriParts.search + uriParts.hash;
      return uri;
    }
  },

  newChannel: function(aURI)
  {
    try {   
      var uriParts = NiProtocol.splitUri(aURI.spec);
      var niParts = NiProtocol.splitHierPart(uriParts.name);
      if (niParts == null || niParts.authority == "")
        // We don't expect this to happen because newURI checked it.
        return null;

      var contentType = NiProtocol.getSearchValue(uriParts.search, "ct");
      var contentCharset = "ISO-8859-1";
      if (contentType != null && contentType.split('/')[0] == "text")
        contentCharset = "utf-8";

      var httpHandler = Cc["@mozilla.org/network/protocol;1?name=http"].getService(Ci.nsIHttpProtocolHandler);
      var wellKnownUri = NiProtocol.newWellKnownUri(niParts.authority, niParts.algorithm, niParts.value);
      if (contentType == null)
        // There is no content type, so just fetch HTTP directly.
        return httpHandler.newChannel(wellKnownUri);

      // Use a ContentChannel so that we can control the contentType and fetching.
      if (contentType == "application/camlistore") {
        var requestContent = function(contentListener) {
          NiProtocol.fetchJson(wellKnownUri, function(json) {
            if (json.camliType == "file") {
              var contentTypeEtc = MimeTypes.getContentTypeAndCharset(json.fileName);
              contentListener.onStart(contentTypeEtc.contentType, contentTypeEtc.charset, null);
              new CamlistorePartsReader(json.parts, contentListener, niParts.authority);
            }
            else {
              contentListener.onStart("text/plain", "utf-8", null);
              contentListener.onReceivedContent("Unknown camliType " + json.camliType);
              contentListener.onStop();
            }
          });
        };

        return new ContentChannel(aURI, requestContent);
      }
      else {
        var requestContent = function(contentListener) {
          var httpListener = {
            onStartRequest: function(aRequest, aContext) {
              contentListener.onStart(contentType, contentCharset, null);
            },
            onDataAvailable: function(aRequest, aContext, aInputStream, aOffset, aCount) {
              // TODO: Can pipe directly to the contentListener?
              var content = NetUtil.readInputStreamToString(aInputStream, aCount);
              contentListener.onReceivedContent(content);
            },
            onStopRequest: function(aRequest, aContext, aStatusCode) {
              contentListener.onStop();
            }
          };

          httpHandler.newChannel(wellKnownUri).asyncOpen(httpListener, null);
        };

        return new ContentChannel(aURI, requestContent);
      }
    } catch (ex) {
      dump("NiProtocol.newChannel exception: " + ex + "\n" + ex.stack);
    }
  },

  classDescription: "ni Protocol Handler",
  contractID: "@mozilla.org/network/protocol;1?name=" + "ni",
  classID: Components.ID('{0a6367e0-166d-11e3-8ffd-0800200c9a66}'),
  QueryInterface: XPCOMUtils.generateQI([Ci.nsIProtocolHandler])
};

if (XPCOMUtils.generateNSGetFactory)
  var NSGetFactory = XPCOMUtils.generateNSGetFactory([NiProtocol]);
else
  var NSGetModule = XPCOMUtils.generateNSGetModule([NiProtocol]);

var CamlistorePartsReader = function CamlistorePartsReader(parts, contentListener, authority)
{
  this.parts = parts;
  this.contentListener = contentListener;
  this.authority = authority;

  this.httpHandler = Cc["@mozilla.org/network/protocol;1?name=http"]
    .getService(Ci.nsIHttpProtocolHandler);
  this.nativeJSON = Components.classes["@mozilla.org/dom/json;1"]
    .createInstance(Components.interfaces.nsIJSON);

  this.iPart = 0;
  this.fetchPart();
};

CamlistorePartsReader.prototype.onStartRequest = function(aRequest, aContext) {};

CamlistorePartsReader.prototype.onDataAvailable = function
  (aRequest, aContext, aInputStream, aOffset, aCount)
{
  // TODO: Can pipe directly to the contentListener?
  var content = NetUtil.readInputStreamToString(aInputStream, aCount);
  this.contentListener.onReceivedContent(content);
};

CamlistorePartsReader.prototype.onStopRequest = function
  (aRequest, aContext, aStatusCode) 
{
  ++this.iPart;
  this.fetchPart();
};

CamlistorePartsReader.prototype.fetchPart = function()
{
  if (this.iPart >= this.parts.length) {
    // Finished.
    this.contentListener.onStop();
    return;
  }

  var blobRef = this.parts[this.iPart].blobRef;
  if (blobRef != undefined)
    this.httpHandler.newChannel
      (NiProtocol.newBlobRefWellKnownUri(blobRef, this.authority))
      .asyncOpen(this, null);
  else {
    var bytesRef = this.parts[this.iPart].bytesRef;
    if (bytesRef != undefined) {
      // Fetch the bytesRef JSON.
    }
  }
};

/**
 * Read the uri using HTTP, convert to a JSON object and call callback(json).
 *
 * @param {ndsURI} uri The URI to fetch.
 * @param {Function} callback This calls callback(json) where json is
 *   a JavaScript object.
 */
NiProtocol.fetchJson = function(uri, callback)
{
  var jsonPipe = Cc["@mozilla.org/pipe;1"].createInstance(Ci.nsIPipe);
  jsonPipe.init(true, true, 0, 0, null);
  var jsonLength = 0;

  var httpListener = {
    onStartRequest: function(aRequest, aContext) {},
    onDataAvailable: function(aRequest, aContext, aInputStream, aOffset, aCount) {
      // TODO: Can pipe directly to the jsonPipe?
      var content = NetUtil.readInputStreamToString(aInputStream, aCount);
      jsonPipe.outputStream.write(content, content.length);
      jsonLength += content.length;
    },
    onStopRequest: function(aRequest, aContext, aStatusCode) {
      // TODO: Can create nativeJSON once?
      var nativeJSON = Components.classes["@mozilla.org/dom/json;1"]
        .createInstance(Components.interfaces.nsIJSON);
      jsonPipe.outputStream.close();
      callback(nativeJSON.decodeFromStream(jsonPipe.inputStream, jsonLength));
    }
  };

  // TODO: Can create httpHandler once?
  var httpHandler = Cc["@mozilla.org/network/protocol;1?name=http"]
    .getService(Ci.nsIHttpProtocolHandler);
  httpHandler.newChannel(uri).asyncOpen(httpListener, null);
};

/**
 * Return a new .well-known URI constructed from the given parts.
 * @param {String} authority The authority, e.g. "example.com".
 * @param {String} algorithm The algorithm, e.g. "sha-256".
 * @param {String} value The value, e.g. "f4OxZX_x_FO5LcGBSKHWXfwtSx-j1ncoSt3SABJtkGk".
 * @returns {nsiURI} The URI object.
 */
NiProtocol.newWellKnownUri = function(authority, algorithm, value)
{
  return NetUtil.newURI("http://" + authority + "/.well-known/ni/" + algorithm + "/" + value);
};

/**
 * Split the URI spec and return an object with the URI fields.
 * All result strings are trimmed.  This does not unescape the name.
 * The name may include a host and port.
 * @param {String} spec
 * @returns An object with fields: protocol (including ':'), name, search (including '?') and hash (including '#').
 */
NiProtocol.splitUri = function(spec) 
{
  spec = spec.trim();
  var result = {};
  var preHash = spec.split('#', 1)[0];
  result.hash = spec.substr(preHash.length).trim();
  var preSearch = preHash.split('?', 1)[0];
  result.search = preHash.substr(preSearch.length).trim();

  preSearch = preSearch.trim();
  var colonIndex = preSearch.indexOf(':');
  if (colonIndex >= 0) {
    result.protocol = preSearch.substr(0, colonIndex + 1).trim();
    result.name = preSearch.substr(colonIndex + 1).trim();
  }
  else {
    result.protocol = "";
    result.name = preSearch;
  }

  return result;
};

/**
 * Parse the URI search string for the first "<key>=<value>" where 
 * <key> is the given key, and return the value.
 * @param {String} search The search string (including '?').
 * @param {String} key The key to search for.
 * @returns The value or null if not found.
 */
NiProtocol.getSearchValue = function(search, key)
{
  // Skip the leading '?'.
  var keyValuePairs = search.substr(1).split('&');
  for (var i = 0; i < keyValuePairs.length; ++i) {
    var keyValueSplit = keyValuePairs[i].split('=');
    if (keyValueSplit.length >= 2 && keyValueSplit[0].trim() == key)
      return keyValueSplit[1].trim();
  }

  return null;
};

/**
 * Split the hierPart as defined by RFC 6920.   All the result strings are trimmed.
 * @param {String} hierPart The "name" part of a URI, for example as returned by NiProtocol.splitUri.
 * For example, "//example.org/sha-256-32;f4OxZQ"
 * @returns An object with fields: authority (or "" if omitted), algorithm and value.  Return null if
 * required fields are missing.
 */
NiProtocol.splitHierPart = function(hierPart) 
{
  hierPart = hierPart.trim();
  
  var result = {};
  var algValue = null;
  if (hierPart.length >= 6 && hierPart.substring(0, 3) == "///") {
    result.authority = "";
    algValue = hierPart.substring(3).trim();
  }
  else if (hierPart.length >= 7 && hierPart.substring(0, 2) == "//") {
    var slashIndex = hierPart.indexOf('/', 2);
    if (slashIndex < 0)
      return null;
    
    result.authority = hierPart.substring(2, slashIndex).trim();
    algValue = hierPart.substring(slashIndex + 1).trim();
  }
  else
    return null;
  
  var splitAlgValue = algValue.split(';');
  if (splitAlgValue.length != 2)
    return null;
  
  result.algorithm = splitAlgValue[0].trim();
  result.value = splitAlgValue[1].trim();
  return result;
};


/**
 * Return a new .well-known URI constructed from the given Camlistore blobRef.
 * @param {String} blobRef The blobRef, e.g. "sha1-b03fe81437efcc263291c3505ed9239642263dac".
 * @param {String} authority The authority, e.g. "example.com".
 * @returns {nsiURI} The URI object or null if can't parse the blobRef.
 */
NiProtocol.newBlobRefWellKnownUri = function(blobRef, authority)
{
  var match = blobRef.match(/^(\w+)-(.+)$/);
  if (!match)
    return null;

  if (match[1] == "sha1") {
    var blobHex = match[2];
    var blobBase64 = rawStringtoBase64(hexToRawString(blobHex));
    // Remove '=' and convert '+' and '/'.
    blobBase64 = blobBase64.replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");

    return NiProtocol.newWellKnownUri(authority, "sha-1", blobBase64);
  }
  else
    return null;
};

var base64KeyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";

function rawStringtoBase64(input) 
{
  var output = "";
  var chr1, chr2, chr3 = "";
  var enc1, enc2, enc3, enc4 = "";
  var i = 0;

  do {
    chr1 = input.charCodeAt(i++);
    chr2 = input.charCodeAt(i++);
    chr3 = input.charCodeAt(i++);

    enc1 = chr1 >> 2;
    enc2 = ((chr1 & 3) << 4) | (chr2 >> 4);
    enc3 = ((chr2 & 15) << 2) | (chr3 >> 6);
    enc4 = chr3 & 63;

    if (isNaN(chr2))
      enc3 = enc4 = 64;
    else if (isNaN(chr3))
      enc4 = 64;

    output = output +
      base64KeyStr.charAt(enc1) +
      base64KeyStr.charAt(enc2) +
      base64KeyStr.charAt(enc3) +
      base64KeyStr.charAt(enc4);
    chr1 = chr2 = chr3 = "";
    enc1 = enc2 = enc3 = enc4 = "";
  } while (i < input.length);

  return output;
}

function hexToRawString(hex)
{
  var result = "";
  hex.replace(/(..)/g, function(hexByte) {
    result += String.fromCharCode(parseInt(hexByte, 16));
  });

  return result;
}

// TODO: Move this to another file.
/* Create an nsIChannel for returning content to the caller of asyncOpen.
 * For requestContent detail, see asyncOpen.
 */
function ContentChannel(uri, requestContent)
{
  this.requestContent = requestContent;

  this.done = false;

  this.name = uri.spec;
    // Bit 18 "LOAD_REPLACE" means the window.location should use the URI set by onStart.
    // loadFlags is updated by the caller of asyncOpen.
    this.loadFlags = (1<<18);
  this.loadGroup = null;
  this.status = 200;

  // We don't know these yet.
  this.contentLength = -1;
  this.contentType = null;
  this.contentCharset = null;
  this.URI = uri;
  this.originalURI = uri;
  this.owner = null;
  this.notificationCallback = null;
  this.securityInfo = null;

    // Save the mostRecentWindow from the moment of creating the channel.
    var wm = Cc["@mozilla.org/appshell/window-mediator;1"].getService(Ci.nsIWindowMediator);
    this.mostRecentWindow = wm.getMostRecentWindow("navigator:browser");
}

ContentChannel.prototype = {
  QueryInterface: function(aIID) {
    if (aIID.equals(Ci.nsISupports))
      return this;

    if (aIID.equals(Ci.nsIRequest))
      return this;

    if (aIID.equals(Ci.nsIChannel))
      return this;

    throw Cr.NS_ERROR_NO_INTERFACE;
  },

  isPending: function() {
    return !this.done;
  },

  cancel: function(aStatus){
    this.status = aStatus;
    this.done   = true;
  },

  suspend: function(aStatus){
    this.status = aStatus;
  },

  resume: function(aStatus){
    this.status = aStatus;
  },

  open: function() {
    throw Cr.NS_ERROR_NOT_IMPLEMENTED;
  }
};

/* Call requestContent(contentListener).  When the content is available, you should call
 *   contentListener funtions as follows:
 * onStart(contentType, contentCharset, uri)
 *   Set the contentType and contentCharset and call aListener.onStartRequest.  If uri
 *   is not null, update this.URI and if this.loadFlags LOAD_INITIAL_DOCUMENT_URI bit is set,
 *   then update the URL bar of the mostRecentWindow. (Note that the caller of asyncOpen
 *   sets this.loadFlags.)
 * onReceivedContent(content)
 *   Call aListener.onDataAvailable.
 * onStop()
 *   Call aListener.onStopRequest.
 */
ContentChannel.prototype.asyncOpen = function(aListener, aContext)
{
  try {
    var thisContentChannel = this;

    var threadManager = Cc["@mozilla.org/thread-manager;1"].getService(Ci.nsIThreadManager);
    var callingThread = threadManager.currentThread;

    var contentListener = {
      onStart: function(contentType, contentCharset, uri) {
        if (uri)
          thisContentChannel.URI = uri;
        thisContentChannel.contentType = contentType;
        thisContentChannel.contentCharset = contentCharset;

        // nsIChannel requires us to call aListener on its calling thread.
        callingThread.dispatch({
          run: function() {
            aListener.onStartRequest(thisContentChannel, aContext);
            // Load flags bit 19 "LOAD_INITIAL_DOCUMENT_URI" means this channel is
            //   for the main window with the URL bar.
            if (uri && thisContentChannel.loadFlags & (1<<19))
              // aListener.onStartRequest may set the URL bar but now we update it.
              thisContentChannel.mostRecentWindow.gURLBar.value = thisContentChannel.URI.spec;
          }
        }, 0);
      },

      onReceivedContent: function(content) {
        var pipe = Cc["@mozilla.org/pipe;1"].createInstance(Ci.nsIPipe);
        pipe.init(true, true, 0, 0, null);
        pipe.outputStream.write(content, content.length);
        pipe.outputStream.close();

        // nsIChannel requires us to call aListener on its calling thread.
        // Assume calls to dispatch are eventually executed in order.
        callingThread.dispatch({
          run: function() {
            aListener.onDataAvailable(thisContentChannel, aContext, pipe.inputStream, 0, content.length);
          }
        }, 0);
      },

      onStop: function() {
        thisContentChannel.done = true;

        // nsIChannel requires us to call aListener on its calling thread.
        callingThread.dispatch({
          run: function() {
            aListener.onStopRequest(thisContentChannel, aContext, thisContentChannel.status);
          }
        }, 0);
      },

      isDone: function() { return thisContentChannel.done; }
    };

    this.requestContent(contentListener);
  }
  catch (ex) {
    dump("ContentChannel.asyncOpen exception: " + ex + "\n" + ex.stack);
  }
};

// TODO: Move this to another file.
/**
 * MimeTypes contains a mapping of filename extension to MIME type, and a function getContentTypeAndCharset to select it.
 */
var MimeTypes = {
  /**
   * Based on filename, return an object with properties contentType and charset.
   */
  getContentTypeAndCharset: function(filename) {
    if (filename) {
      var iDot = filename.lastIndexOf('.');
      if (iDot >= 0) {
        var extension = filename.substr(iDot + 1, filename.length - iDot - 1);
        var contentType = MimeTypes[extension];
        if (contentType != null) {
          var charset = "ISO-8859-1";
          if (contentType.split('/')[0] == "text")
              charset = "utf-8";
          return { contentType: contentType, charset: charset };
        }
      }
    }

    // Use a default.
    return { contentType: "application/octet-stream", charset: "ISO-8859-1" };
  },

  /* For each file extension, define the MIME type.
   */
  "323": "text/h323",
  "%": "application/x-trash",
  "~": "application/x-trash",
  "3gp": "video/3gpp",
  "7z": "application/x-7z-compressed",
  "abw": "application/x-abiword",
  "ai": "application/postscript",
  "aif": "audio/x-aiff",
  "aifc": "audio/x-aiff",
  "aiff": "audio/x-aiff",
  "alc": "chemical/x-alchemy",
  "amr": "audio/amr",
  "anx": "application/annodex",
  "apk": "application/vnd.android.package-archive",
  "art": "image/x-jg",
  "asc": "text/plain",
  "asf": "video/x-ms-asf",
  "asx": "video/x-ms-asf",
  "asn": "chemical/x-ncbi-asn1",
  "atom": "application/atom+xml",
  "atomcat": "application/atomcat+xml",
  "atomsrv": "application/atomserv+xml",
  "au": "audio/basic",
  "snd": "audio/basic",
  "avi": "video/x-msvideo",
  "awb": "audio/amr-wb",
  "axa": "audio/annodex",
  "axv": "video/annodex",
  "b": "chemical/x-molconn-Z",
  "bak": "application/x-trash",
  "bat": "application/x-msdos-program",
  "bcpio": "application/x-bcpio",
  "bib": "text/x-bibtex",
  "bin": "application/octet-stream",
  "bmp": "image/x-ms-bmp",
  "boo": "text/x-boo",
  "book": "application/x-maker",
  "brf": "text/plain",
  "bsd": "chemical/x-crossfire",
  "c": "text/x-csrc",
  "c++": "text/x-c++src",
  "c3d": "chemical/x-chem3d",
  "cab": "application/x-cab",
  "cac": "chemical/x-cache",
  "cache": "chemical/x-cache",
  "cap": "application/cap",
  "cascii": "chemical/x-cactvs-binary",
  "cat": "application/vnd.ms-pki.seccat",
  "cbin": "chemical/x-cactvs-binary",
  "cbr": "application/x-cbr",
  "cbz": "application/x-cbz",
  "cc": "text/x-c++src",
  "cda": "application/x-cdf",
  "cdf": "application/x-cdf",
  "cdr": "image/x-coreldraw",
  "cdt": "image/x-coreldrawtemplate",
  "cdx": "chemical/x-cdx",
  "cdy": "application/vnd.cinderella",
  "cer": "chemical/x-cerius",
  "chm": "chemical/x-chemdraw",
  "chrt": "application/x-kchart",
  "cif": "chemical/x-cif",
  "class": "application/java-vm",
  "cls": "text/x-tex",
  "cmdf": "chemical/x-cmdf",
  "cml": "chemical/x-cml",
  "cod": "application/vnd.rim.cod",
  "com": "application/x-msdos-program",
  "cpa": "chemical/x-compass",
  "cpio": "application/x-cpio",
  "cpp": "text/x-c++src",
  "cpt": "image/x-corelphotopaint",
  "cr2": "image/x-canon-cr2",
  "crl": "application/x-pkcs7-crl",
  "crt": "application/x-x509-ca-cert",
  "crw": "image/x-canon-crw",
  "csd": "audio/csound",
  "csf": "chemical/x-cache-csf",
  "csh": "application/x-csh",
  "csml": "chemical/x-csml",
  "csm": "chemical/x-csml",
  "css": "text/css",
  "csv": "text/csv",
  "ctab": "chemical/x-cactvs-binary",
  "ctx": "chemical/x-ctx",
  "cu": "application/cu-seeme",
  "cub": "chemical/x-gaussian-cube",
  "cxf": "chemical/x-cxf",
  "cef": "chemical/x-cxf",
  "cxx": "text/x-c++src",
  "d": "text/x-dsrc",
  "dat": "application/x-ns-proxy-autoconfig",
  "davmount": "application/davmount+xml",
  "dcr": "application/x-director",
  "deb": "application/x-debian-package",
  "dif": "video/dv",
  "dv": "video/dv",
  "diff": "text/x-diff",
  "patch": "text/x-diff",
  "dir": "application/x-director",
  "djvu": "image/vnd.djvu",
  "djv": "image/vnd.djvu",
  "dl": "video/dl",
  "dll": "application/x-msdos-program",
  "dmg": "application/x-apple-diskimage",
  "dms": "application/x-dms",
  "doc": "application/msword",
  "docm": "application/vnd.ms-word.document.macroEnabled.12",
  "docx": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "dot": "application/msword",
  "dotm": "application/vnd.ms-word.template.macroEnabled.12",
  "dotx": "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
  "dvi": "application/x-dvi",
  "dxr": "application/x-director",
  "emb": "chemical/x-embl-dl-nucleotide",
  "embl": "chemical/x-embl-dl-nucleotide",
  "eml": "message/rfc822",
  "eps": "application/postscript",
  "eps2": "application/postscript",
  "eps3": "application/postscript",
  "epsf": "application/postscript",
  "epsi": "application/postscript",
  "erf": "image/x-epson-erf",
  "es": "application/ecmascript",
  "etx": "text/x-setext",
  "exe": "application/x-msdos-program",
  "ez": "application/andrew-inset",
  "fb": "application/x-maker",
  "fbdoc": "application/x-maker",
  "fch": "chemical/x-gaussian-checkpoint",
  "fchk": "chemical/x-gaussian-checkpoint",
  "fig": "application/x-xfig",
  "flac": "audio/flac",
  "fli": "video/fli",
  "flv": "video/x-flv",
  "fm": "application/x-maker",
  "frame": "application/x-maker",
  "frm": "application/x-maker",
  "gal": "chemical/x-gaussian-log",
  "gam": "chemical/x-gamess-input",
  "gamin": "chemical/x-gamess-input",
  "gan": "application/x-ganttproject",
  "gau": "chemical/x-gaussian-input",
  "gcd": "text/x-pcs-gcd",
  "gcf": "application/x-graphing-calculator",
  "gcg": "chemical/x-gcg8-sequence",
  "gen": "chemical/x-genbank",
  "gf": "application/x-tex-gf",
  "gif": "image/gif",
  "gjc": "chemical/x-gaussian-input",
  "gjf": "chemical/x-gaussian-input",
  "gl": "video/gl",
  "gnumeric": "application/x-gnumeric",
  "gpt": "chemical/x-mopac-graph",
  "gsf": "application/x-font",
  "gsm": "audio/x-gsm",
  "gtar": "application/x-gtar",
  "h": "text/x-chdr",
  "h++": "text/x-c++hdr",
  "hdf": "application/x-hdf",
  "hh": "text/x-c++hdr",
  "hin": "chemical/x-hin",
  "hpp": "text/x-c++hdr",
  "hqx": "application/mac-binhex40",
  "hs": "text/x-haskell",
  "hta": "application/hta",
  "htc": "text/x-component",
  "htm": "text/html",
  "html": "text/html",
  "hxx": "text/x-c++hdr",
  "ica": "application/x-ica",
  "ice": "x-conference/x-cooltalk",
  "ico": "image/x-icon",
  "ics": "text/calendar",
  "icz": "text/calendar",
  "ief": "image/ief",
  "igs": "model/iges",
  "iges": "model/iges",
  "iii": "application/x-iphone",
  "info": "application/x-info",
  "inp": "chemical/x-gamess-input",
  "ins": "application/x-internet-signup",
  "iso": "application/x-iso9660-image",
  "isp": "application/x-internet-signup",
  "istr": "chemical/x-isostar",
  "ist": "chemical/x-isostar",
  "jad": "text/vnd.sun.j2me.app-descriptor",
  "jam": "application/x-jam",
  "jar": "application/java-archive",
  "java": "text/x-java",
  "jdx": "chemical/x-jcamp-dx",
  "dx": "chemical/x-jcamp-dx",
  "jmz": "application/x-jmol",
  "jng": "image/x-jng",
  "jnlp": "application/x-java-jnlp-file",
  "jpe": "image/jpeg",
  "jpeg": "image/jpeg",
  "jpg": "image/jpeg",
  "js": "application/javascript",
  "json": "application/json",
  "kar": "audio/midi",
  "key": "application/pgp-keys",
  "kil": "application/x-killustrator",
  "kin": "chemical/x-kinemage",
  "kml": "application/vnd.google-earth.kml+xml",
  "kmz": "application/vnd.google-earth.kmz",
  "kpr": "application/x-kpresenter",
  "kpt": "application/x-kpresenter",
  "ksp": "application/x-kspread",
  "kwd": "application/x-kword",
  "kwt": "application/x-kword",
  "latex": "application/x-latex",
  "lha": "application/x-lha",
  "lhs": "text/x-literate-haskell",
  "lin": "application/bbolin",
  "lsf": "video/x-la-asf",
  "lsx": "video/x-la-asf",
  "ltx": "text/x-tex",
  "lyx": "application/x-lyx",
  "lzh": "application/x-lzh",
  "lzx": "application/x-lzx",
  "m3g": "application/m3g",
  "m3u": "audio/mpegurl",
  "m3u8": "application/x-mpegURL",
  "m4a": "audio/mpeg",
  "maker": "application/x-maker",
  "man": "application/x-troff-man",
  "manifest": "text/cache-manifest",
  "mcif": "chemical/x-mmcif",
  "mcm": "chemical/x-macmolecule",
  "mdb": "application/msaccess",
  "me": "application/x-troff-me",
  "mesh": "model/mesh",
  "mid": "audio/midi",
  "midi": "audio/midi",
  "mif": "application/x-mif",
  "mm": "application/x-freemind",
  "mmd": "chemical/x-macromodel-input",
  "mmod": "chemical/x-macromodel-input",
  "mmf": "application/vnd.smaf",
  "mml": "text/mathml",
  "mng": "video/x-mng",
  "moc": "text/x-moc",
  "mol": "chemical/x-mdl-molfile",
  "mol2": "chemical/x-mol2",
  "moo": "chemical/x-mopac-out",
  "mop": "chemical/x-mopac-input",
  "mopcrt": "chemical/x-mopac-input",
  "movie": "video/x-sgi-movie",
  "mp2": "audio/mpeg",
  "mp3": "audio/mpeg",
  "mp4": "video/mp4",
  "mpc": "chemical/x-mopac-input",
  "mpe": "video/mpeg",
  "mpeg": "video/mpeg",
  "mpega": "audio/mpeg",
  "mpg": "video/mpeg",
  "mpga": "audio/mpeg",
  "mph": "application/x-comsol",
  "mpv": "video/x-matroska",
  "mkv": "video/x-matroska",
  "ms": "application/x-troff-ms",
  "msh": "model/mesh",
  "msi": "application/x-msi",
  "mvb": "chemical/x-mopac-vib",
  "mxf": "application/mxf",
  "mxu": "video/vnd.mpegurl",
  "nb": "application/mathematica",
  "nbp": "application/mathematica",
  "nc": "application/x-netcdf",
  "nef": "image/x-nikon-nef",
  "nwc": "application/x-nwc",
  "o": "application/x-object",
  "oda": "application/oda",
  "odb": "application/vnd.oasis.opendocument.database",
  "odc": "application/vnd.oasis.opendocument.chart",
  "odf": "application/vnd.oasis.opendocument.formula",
  "odg": "application/vnd.oasis.opendocument.graphics",
  "odi": "application/vnd.oasis.opendocument.image",
  "odm": "application/vnd.oasis.opendocument.text-master",
  "odp": "application/vnd.oasis.opendocument.presentation",
  "ods": "application/vnd.oasis.opendocument.spreadsheet",
  "odt": "application/vnd.oasis.opendocument.text",
  "oga": "audio/ogg",
  "ogg": "audio/ogg",
  "ogv": "video/ogg",
  "ogx": "application/ogg",
  "old": "application/x-trash",
  "one": "application/onenote",
  "onepkg": "application/onenote",
  "onetmp": "application/onenote",
  "onetoc2": "application/onenote",
  "orc": "audio/csound",
  "orf": "image/x-olympus-orf",
  "otg": "application/vnd.oasis.opendocument.graphics-template",
  "oth": "application/vnd.oasis.opendocument.text-web",
  "otp": "application/vnd.oasis.opendocument.presentation-template",
  "ots": "application/vnd.oasis.opendocument.spreadsheet-template",
  "ott": "application/vnd.oasis.opendocument.text-template",
  "oza": "application/x-oz-application",
  "p": "text/x-pascal",
  "pas": "text/x-pascal",
  "p7r": "application/x-pkcs7-certreqresp",
  "pac": "application/x-ns-proxy-autoconfig",
  "pat": "image/x-coreldrawpattern",
  "pbm": "image/x-portable-bitmap",
  "pcap": "application/cap",
  "pcf": "application/x-font",
  "pcx": "image/pcx",
  "pdb": "chemical/x-pdb",
  "ent": "chemical/x-pdb",
  "pdf": "application/pdf",
  "pfa": "application/x-font",
  "pfb": "application/x-font",
  "pgm": "image/x-portable-graymap",
  "pgn": "application/x-chess-pgn",
  "pgp": "application/pgp-signature",
  "php": "application/x-httpd-php",
  "php3": "application/x-httpd-php3",
  "php3p": "application/x-httpd-php3-preprocessed",
  "php4": "application/x-httpd-php4",
  "php5": "application/x-httpd-php5",
  "phps": "application/x-httpd-php-source",
  "pht": "application/x-httpd-php",
  "phtml": "application/x-httpd-php",
  "pk": "application/x-tex-pk",
  "pl": "text/x-perl",
  "pm": "text/x-perl",
  "pls": "audio/x-scpls",
  "png": "image/png",
  "pnm": "image/x-portable-anymap",
  "pot": "text/plain",
  "potm": "application/vnd.ms-powerpoint.template.macroEnabled.12",
  "potx": "application/vnd.openxmlformats-officedocument.presentationml.template",
  "ppam": "application/vnd.ms-powerpoint.addin.macroEnabled.12",
  "ppm": "image/x-portable-pixmap",
  "pps": "application/vnd.ms-powerpoint",
  "ppsm": "application/vnd.ms-powerpoint.slideshow.macroEnabled.12",
  "ppsx": "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
  "ppt": "application/vnd.ms-powerpoint",
  "pptm": "application/vnd.ms-powerpoint.presentation.macroEnabled.12",
  "pptx": "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  "prf": "application/pics-rules",
  "prt": "chemical/x-ncbi-asn1-ascii",
  "ps": "application/postscript",
  "psd": "image/x-photoshop",
  "py": "text/x-python",
  "pyc": "application/x-python-code",
  "pyo": "application/x-python-code",
  "qgs": "application/x-qgis",
  "qt": "video/quicktime",
  "mov": "video/quicktime",
  "qtl": "application/x-quicktimeplayer",
  "ra": "audio/x-realaudio",
  "ram": "audio/x-pn-realaudio",
  "rar": "application/rar",
  "ras": "image/x-cmu-raster",
  "rb": "application/x-ruby",
  "rd": "chemical/x-mdl-rdfile",
  "rdf": "application/rdf+xml",
  "rdp": "application/x-rdp",
  "rgb": "image/x-rgb",
  "rhtml": "application/x-httpd-eruby",
  "rm": "audio/x-pn-realaudio",
  "roff": "application/x-troff",
  "ros": "chemical/x-rosdal",
  "rpm": "application/x-redhat-package-manager",
  "rss": "application/rss+xml",
  "rtf": "application/rtf",
  "rtx": "text/richtext",
  "rxn": "chemical/x-mdl-rxnfile",
  "scala": "text/x-scala",
  "sci": "application/x-scilab",
  "sce": "application/x-scilab",
  "sco": "audio/csound",
  "scr": "application/x-silverlight",
  "sct": "text/scriptlet",
  "wsc": "text/scriptlet",
  "sd": "chemical/x-mdl-sdfile",
  "sdf": "chemical/x-mdl-sdfile",
  "sd2": "audio/x-sd2",
  "sda": "application/vnd.stardivision.draw",
  "sdc": "application/vnd.stardivision.calc",
  "sdd": "application/vnd.stardivision.impress",
  "sds": "application/vnd.stardivision.chart",
  "sdw": "application/vnd.stardivision.writer",
  "ser": "application/java-serialized-object",
  "sfv": "text/x-sfv",
  "sgf": "application/x-go-sgf",
  "sgl": "application/vnd.stardivision.writer-global",
  "sh": "application/x-sh",
  "shar": "application/x-shar",
  "shp": "application/x-qgis",
  "shtml": "text/html",
  "shx": "application/x-qgis",
  "sid": "audio/prs.sid",
  "sik": "application/x-trash",
  "silo": "model/mesh",
  "sis": "application/vnd.symbian.install",
  "sisx": "x-epoc/x-sisx-app",
  "sit": "application/x-stuffit",
  "sitx": "application/x-stuffit",
  "skd": "application/x-koan",
  "skm": "application/x-koan",
  "skp": "application/x-koan",
  "skt": "application/x-koan",
  "sldm": "application/vnd.ms-powerpoint.slide.macroEnabled.12",
  "sldx": "application/vnd.openxmlformats-officedocument.presentationml.slide",
  "smi": "application/smil",
  "smil": "application/smil",
  "spc": "chemical/x-galactic-spc",
  "spl": "application/futuresplash",
  "spx": "audio/ogg",
  "sql": "application/x-sql",
  "src": "application/x-wais-source",
  "stc": "application/vnd.sun.xml.calc.template",
  "std": "application/vnd.sun.xml.draw.template",
  "sti": "application/vnd.sun.xml.impress.template",
  "stl": "application/sla",
  "stw": "application/vnd.sun.xml.writer.template",
  "sty": "text/x-tex",
  "sv4cpio": "application/x-sv4cpio",
  "sv4crc": "application/x-sv4crc",
  "svg": "image/svg+xml",
  "svgz": "image/svg+xml",
  "sw": "chemical/x-swissprot",
  "swf": "application/x-shockwave-flash",
  "swfl": "application/x-shockwave-flash",
  "sxc": "application/vnd.sun.xml.calc",
  "sxd": "application/vnd.sun.xml.draw",
  "sxg": "application/vnd.sun.xml.writer.global",
  "sxi": "application/vnd.sun.xml.impress",
  "sxm": "application/vnd.sun.xml.math",
  "sxw": "application/vnd.sun.xml.writer",
  "t": "application/x-troff",
  "tar": "application/x-tar",
  "taz": "application/x-gtar-compressed",
  "tcl": "application/x-tcl",
  "tk": "text/x-tcl",
  "tex": "text/x-tex",
  "texinfo": "application/x-texinfo",
  "texi": "application/x-texinfo",
  "text": "text/plain",
  "tgf": "chemical/x-mdl-tgf",
  "tgz": "application/x-gtar-compressed",
  "thmx": "application/vnd.ms-officetheme",
  "tiff": "image/tiff",
  "tif": "image/tiff",
  "tm": "text/texmacs",
  "torrent": "application/x-bittorrent",
  "tr": "application/x-troff",
  "ts": "video/MP2T",
  "tsp": "application/dsptype",
  "tsv": "text/tab-separated-values",
  "txt": "text/plain",
  "udeb": "application/x-debian-package",
  "uls": "text/iuls",
  "ustar": "application/x-ustar",
  "val": "chemical/x-ncbi-asn1-binary",
  "aso": "chemical/x-ncbi-asn1-binary",
  "vcd": "application/x-cdlink",
  "vcf": "text/x-vcard",
  "vcs": "text/x-vcalendar",
  "vmd": "chemical/x-vmd",
  "vms": "chemical/x-vamas-iso14976",
  "vrm": "x-world/x-vrml",
  "vsd": "application/vnd.visio",
  "wad": "application/x-doom",
  "wav": "audio/x-wav",
  "wax": "audio/x-ms-wax",
  "wbmp": "image/vnd.wap.wbmp",
  "wbxml": "application/vnd.wap.wbxml",
  "webm": "video/webm",
  "wk": "application/x-123",
  "wm": "video/x-ms-wm",
  "wma": "audio/x-ms-wma",
  "wmd": "application/x-ms-wmd",
  "wml": "text/vnd.wap.wml",
  "wmlc": "application/vnd.wap.wmlc",
  "wmls": "text/vnd.wap.wmlscript",
  "wmlsc": "application/vnd.wap.wmlscriptc",
  "wmv": "video/x-ms-wmv",
  "wmx": "video/x-ms-wmx",
  "wmz": "application/x-ms-wmz",
  "wp5": "application/vnd.wordperfect5.1",
  "wpd": "application/vnd.wordperfect",
  "wrl": "model/vrml",
  "vrml": "model/vrml",
  "wvx": "video/x-ms-wvx",
  "wz": "application/x-wingz",
  "x3d": "model/x3d+xml",
  "x3db": "model/x3d+binary",
  "x3dv": "model/x3d+vrml",
  "xbm": "image/x-xbitmap",
  "xcf": "application/x-xcf",
  "xht": "application/xhtml+xml",
  "xhtml": "application/xhtml+xml",
  "xlam": "application/vnd.ms-excel.addin.macroEnabled.12",
  "xlb": "application/vnd.ms-excel",
  "xls": "application/vnd.ms-excel",
  "xlsb": "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
  "xlsm": "application/vnd.ms-excel.sheet.macroEnabled.12",
  "xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  "xlt": "application/vnd.ms-excel",
  "xltm": "application/vnd.ms-excel.template.macroEnabled.12",
  "xltx": "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
  "xml": "application/xml",
  "xpi": "application/x-xpinstall",
  "xpm": "image/x-xpixmap",
  "xsd": "application/xml",
  "xsl": "application/xml",
  "xspf": "application/xspf+xml",
  "xtel": "chemical/x-xtel",
  "xul": "application/vnd.mozilla.xul+xml",
  "xwd": "image/x-xwindowdump",
  "xyz": "chemical/x-xyz",
  "zip": "application/zip",
  "zmt": "chemical/x-mopac-input"
};

