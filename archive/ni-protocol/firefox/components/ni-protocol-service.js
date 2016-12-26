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
      if (contentType == "video/mp4")
        // Even though the web server sends "application/octet-stream", Firefox
        //   seems to show it correctly as mp4. At the moment, ContentChannel doesn't
        //   handle the HTTP streaming protocol, so we can use an httpHandler directly.
        return httpHandler.newChannel(wellKnownUri);

      // Use a ContentChannel so that we can control the contentType.
      var requestContent = function(contentListener) {
        var httpListener = {
          onStartRequest: function(aRequest, aContext) {
            contentListener.onStart(contentType, contentCharset);
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

// TODO: Move this to another file.
var PR_UINT32_MAX = 0xffffffff;

/* Create an nsIChannel for returning content to the caller of asyncOpen.
 * For requestContent detail, see asyncOpen.
 */
function ContentChannel(uri, requestContent)
{
  this.requestContent = requestContent;

  this.done = false;

  this.name = uri.spec;
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
 * onStart(contentType, contentCharset)
 *   Set the contentType and contentCharset and call aListener.onStartRequest.
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
      onStart: function(contentType, contentCharset) {
        thisContentChannel.contentType = contentType;
        thisContentChannel.contentCharset = contentCharset;

        // nsIChannel requires us to call aListener on its calling thread.
        callingThread.dispatch({
          run: function() {
            aListener.onStartRequest(thisContentChannel, aContext);
          }
        }, 0);
      },

      onReceivedContent: function(content) {
        var pipe = Cc["@mozilla.org/pipe;1"].createInstance(Ci.nsIPipe);
        // Use PR_UINT32_MAX because a content can be larger than 64K.
        pipe.init(true, true, 0, PR_UINT32_MAX, null);
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
        // nsIChannel requires us to call aListener on its calling thread.
        callingThread.dispatch({
          run: function() {
            aListener.onStopRequest(thisContentChannel, aContext, thisContentChannel.status);
            thisContentChannel.done = true;
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
