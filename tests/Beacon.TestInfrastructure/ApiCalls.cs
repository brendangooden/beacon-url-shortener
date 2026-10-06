using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Beacon.Api.Domain;
using Beacon.Api.Features.Folders;
using Beacon.Api.Features.Links;
using Beacon.Api.Features.Sharing;
using Beacon.Api.Features.Workspaces;

namespace Beacon.TestInfrastructure;

/// <summary>
/// Thin typed wrappers over the real HTTP routes, for the Arrange step. They fail fast with the
/// response body when a setup call does not succeed, so a broken precondition never hides as a
/// confusing assertion failure later in the test.
/// </summary>
public static class ApiCalls
{
    public const string Api = "/api/v1";

    /// <summary>The API's wire format: web defaults + enums as strings.</summary>
    public static readonly JsonSerializerOptions Json = CreateJsonOptions();

    public static string LinksPath(Guid workspaceId) => $"{Api}/workspaces/{workspaceId}/links";

    public static string LinkPath(Guid workspaceId, Guid linkId) => $"{LinksPath(workspaceId)}/{linkId}";

    public static string FoldersPath(Guid workspaceId) => $"{Api}/workspaces/{workspaceId}/folders";

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        return value ?? throw new InvalidOperationException($"Empty {typeof(T).Name} body.");
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PostAsJsonAsync(path, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PutAsJsonAsync(path, body, Json);

    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PatchAsJsonAsync(path, body, Json);

    public static async Task<WorkspaceSummary> CreateWorkspaceAsync(this HttpClient client, CreateWorkspaceRequest request) =>
        await (await client.PostJsonAsync($"{Api}/workspaces", request)).EnsureOkAsync<WorkspaceSummary>();

    public static async Task<FolderDto> CreateFolderAsync(this HttpClient client, Guid workspaceId, CreateFolderRequest request) =>
        await (await client.PostJsonAsync(FoldersPath(workspaceId), request)).EnsureOkAsync<FolderDto>();

    public static async Task<LinkDto> CreateLinkAsync(this HttpClient client, Guid workspaceId, CreateLinkRequest request) =>
        await (await client.PostJsonAsync(LinksPath(workspaceId), request)).EnsureOkAsync<LinkDto>();

    public static async Task<LinkDto> RenameLinkAsync(this HttpClient client, Guid workspaceId, Guid linkId, string newCode) =>
        await (await client.PostJsonAsync($"{LinkPath(workspaceId, linkId)}/rename", new RenameLinkRequest(newCode))).EnsureOkAsync<LinkDto>();

    public static async Task SetActiveAsync(this HttpClient client, Guid workspaceId, Guid linkId, bool isActive) =>
        await (await client.PatchJsonAsync($"{LinkPath(workspaceId, linkId)}/active", new SetActiveRequest(isActive))).EnsureNoContentAsync();

    public static async Task SoftDeleteLinkAsync(this HttpClient client, Guid workspaceId, Guid linkId) =>
        await (await client.DeleteAsync(LinkPath(workspaceId, linkId))).EnsureNoContentAsync();

    public static async Task<ShareDto> ShareAsync(this HttpClient client, ResourceType type, Guid resourceId, TestPersona grantee, AccessLevel level) =>
        await (await client.PostJsonAsync($"{Api}/shares", new CreateShareRequest(type, resourceId, grantee.Email, grantee.Oid, grantee.Name, level)))
            .EnsureOkAsync<ShareDto>();

    /// <summary>GET a public path (a ShortCode). Always GET: the redirect route is GET-only.</summary>
    public static Task<HttpResponseMessage> ResolveAsync(this HttpClient client, string code) =>
        client.GetAsync($"/{code}");

    private static async Task<T> EnsureOkAsync<T>(this HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Setup call {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return await response.ReadAsync<T>();
    }

    private static async Task EnsureNoContentAsync(this HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException(
                $"Setup call {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
