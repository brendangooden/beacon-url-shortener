using System.Net;
using Shouldly;
using Beacon.TestInfrastructure;

namespace Beacon.Api.IntegrationTests;

/// <summary>Assertions for the public <c>GET /{code}</c> surface.</summary>
public static class RedirectAssertions
{
    /// <summary>The code resolves in one hop: a 302 straight to <paramref name="destination"/>.</summary>
    public static async Task ShouldRedirectToAsync(this HttpClient client, string code, string destination)
    {
        using var response = await client.ResolveAsync(code);

        response.StatusCode.ShouldBe(HttpStatusCode.Found, $"GET /{code}");
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(destination, $"GET /{code}");
    }

    /// <summary>The code does not resolve: the branded HTML Landing page with a 404.</summary>
    public static async Task ShouldShowLandingPageAsync(this HttpClient client, string code)
    {
        using var response = await client.ResolveAsync(code);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"GET /{code}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync()).ShouldContain("Link not found");
    }
}
