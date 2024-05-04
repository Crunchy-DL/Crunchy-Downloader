using System;

namespace CRD.Utils.Parser.Utils;

public class UrlUtils{
    public static string ResolveUrl(string baseUrl, string relativeUrl){
        // Return early if the relative URL is actually an absolute URL
        if (Uri.IsWellFormedUriString(relativeUrl, UriKind.Absolute))
            return relativeUrl;

        // Handle the case where baseUrl is not specified or invalid
        Uri baseUri;
        if (string.IsNullOrEmpty(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri)){
            // Assuming you want to use a default base if none is provided
            // For example, you could default to "http://example.com"
            // This part is up to how you want to handle such cases
            baseUri = new Uri("http://example.com");
        }

        Uri resolvedUri = new Uri(baseUri, relativeUrl);
        return resolvedUri.ToString();
    }
}