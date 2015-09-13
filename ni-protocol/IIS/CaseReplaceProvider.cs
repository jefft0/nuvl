using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Web.Iis.Rewrite;

/**
 * See http://www.iis.net/learn/extensions/url-rewrite-module/developing-a-custom-rewrite-provider-for-url-rewrite-module .
 * Run Visual Studio as administrator so you can run the post-build event command line.
 */
public class ReplaceProvider : IRewriteProvider
{
  public void Initialize(IDictionary<string, string> settings, IRewriteContext rewriteContext)
  {
  }

  /**
   * Replaces all upper-case characters C with C!.
   */
  public string Rewrite(string value)
  {
    var result = new StringBuilder();
    foreach (var c in value) {
      result.Append(c);
      if (c >= 'A' && c <= 'Z')
        result.Append('!');
    }

    return result.ToString();
  }
}
