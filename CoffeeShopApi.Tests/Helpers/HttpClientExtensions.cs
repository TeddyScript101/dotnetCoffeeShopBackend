using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoffeeShopApi.Tests.Helpers;

public static class HttpClientExtensions
{
    /// <summary>
    /// Matches the JSON options configured on the API (camelCase + string enums).
    /// Use with ReadFromJsonAsync when the response contains enum fields.
    /// </summary>
    public static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Sets the Authorization: Bearer header on the client.</summary>
    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Clears any Authorization header previously set on the client.</summary>
    public static void ClearBearerToken(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>Serialises the object to JSON and returns it as HttpContent.</summary>
    public static HttpContent ToJsonContent<T>(this T obj) =>
        JsonContent.Create(obj);
}
