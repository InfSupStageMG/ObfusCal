using System.Net;
using System.Text;

namespace ObfusCal.Tests.Helpers;

/// <summary>
/// Creates disposable HTTP test responses while making ownership transfer explicit at the call site.
/// </summary>
internal static class TestHttpResponses
{
    internal static HttpResponseMessage Create(HttpStatusCode statusCode)
        => new(statusCode);

    internal static HttpResponseMessage Text(HttpStatusCode statusCode, string content)
        => new(statusCode)
        {
            Content = new StringContent(content)
        };

    internal static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    internal static HttpResponseMessage Xml(HttpStatusCode statusCode, string xml)
        => new(statusCode)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        };
}

