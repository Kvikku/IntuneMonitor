using System.Net.Http.Headers;
using Azure.Core;

namespace IntuneMonitor.Graph;

/// <summary>
/// Centralizes access-token acquisition and <see cref="HttpClient"/> creation for
/// Microsoft Graph API calls. Uses <see cref="IHttpClientFactory"/> for proper
/// connection pooling and lifecycle management.
/// </summary>
public class GraphClientFactory
{
    /// <summary>Named client identifier used with <see cref="IHttpClientFactory"/>.</summary>
    public const string HttpClientName = "Graph";

    private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };
    private readonly IHttpClientFactory _httpClientFactory;

    public GraphClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    /// Acquires a bearer token from the provided credential and returns
    /// a configured <see cref="HttpClient"/> ready for Graph API calls.
    /// The caller is responsible for disposing the returned client.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
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
    /// Creates an <see cref="HttpClient"/> from the factory, pre-configured with
    /// the provided bearer token. The Accept header is set during client registration.
    /// </summary>
    public HttpClient CreateHttpClient(string bearerToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }
}
