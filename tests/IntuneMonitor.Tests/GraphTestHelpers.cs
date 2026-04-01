using Azure.Core;

namespace IntuneMonitor.Tests;

/// <summary>
/// Shared helpers for Graph API integration tests.
/// </summary>
internal static class GraphTestHelpers
{
    /// <summary>
    /// Creates an HttpClient backed by the given mock handler.
    /// The handler is NOT disposed when the client is disposed, allowing reuse.
    /// </summary>
    public static HttpClient CreateMockClient(MockHttpHandler handler) =>
        new(handler, disposeHandler: false);

    /// <summary>
    /// Returns a factory function suitable for the internal HttpClientFactory hooks
    /// on Graph classes. Each invocation returns a new HttpClient sharing the same handler.
    /// </summary>
    public static Func<CancellationToken, Task<HttpClient>> CreateClientFactory(MockHttpHandler handler) =>
        _ => Task.FromResult(CreateMockClient(handler));

    /// <summary>A fake TokenCredential that never contacts Azure AD.</summary>
    public static TokenCredential FakeCredential { get; } = new FakeTokenCredential();

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(new AccessToken("fake-token", DateTimeOffset.MaxValue));

        public override AccessToken GetToken(
            TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("fake-token", DateTimeOffset.MaxValue);
    }
}
