using System.Net.Http.Headers;
using Azure.Core;

namespace IntuneMonitor.Graph;

/// <summary>
/// Centralizes access-token acquisition and <see cref="HttpClient"/> creation for
/// Microsoft Graph API calls. Eliminates duplicated token/client code across Graph classes.
/// </summary>
internal static class GraphClientFactory
{
    private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };

    /// <summary>
    /// Acquires a bearer token from the provided credential and returns
    /// a configured <see cref="HttpClient"/> ready for Graph API calls.
    /// The caller is responsible for disposing the returned client.
    /// </summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        TokenCredential credential,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(credential, cancellationToken);
        return CreateHttpClient(token);
    }

    /// <summary>
    /// Acquires a bearer token string from the provided credential.
    /// </summary>
    public static async Task<string> GetAccessTokenAsync(
        TokenCredential credential,
        CancellationToken cancellationToken = default)
    {
        var context = new TokenRequestContext(Scopes);
        var tokenResult = await credential.GetTokenAsync(context, cancellationToken);
        return tokenResult.Token;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-configured with the provided bearer token
    /// and JSON accept header.
    /// </summary>
    public static HttpClient CreateHttpClient(string bearerToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
