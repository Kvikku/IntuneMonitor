using System.Net;
using System.Text;
using System.Text.Json;

namespace IntuneMonitor.Tests;

/// <summary>
/// A test helper that intercepts HTTP requests and returns pre-configured responses
/// in the order they were enqueued. Tracks all requests for assertion.
/// </summary>
internal sealed class MockHttpHandler : HttpMessageHandler
{
    private int _requestIndex;
    private readonly List<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    /// <summary>All HTTP requests sent through this handler, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = new();

    /// <summary>
    /// Buffered request bodies, in order. Use this to read request content after the call
    /// completes (since the underlying <see cref="HttpRequestMessage"/> may be disposed).
    /// </summary>
    public List<string?> RequestBodies { get; } = new();

    /// <summary>Enqueue a JSON response with the given status code and body object.</summary>
    public void Enqueue(HttpStatusCode statusCode, object body)
    {
        var json = JsonSerializer.Serialize(body);
        _responses.Add(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    /// <summary>Enqueue a response using a factory that receives the request.</summary>
    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        _responses.Add(factory);
    }

    /// <summary>Enqueue a 429 response with a Retry-After header.</summary>
    public void Enqueue429(int retryAfterSeconds)
    {
        _responses.Add(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    """{"error":{"code":"TooManyRequests","message":"Throttled"}}""",
                    Encoding.UTF8, "application/json")
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(retryAfterSeconds));
            return response;
        });
    }

    /// <summary>Enqueue a simple error response.</summary>
    public void EnqueueError(HttpStatusCode statusCode, string errorMessage = "Error")
    {
        var json = JsonSerializer.Serialize(new { error = new { code = statusCode.ToString(), message = errorMessage } });
        _responses.Add(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer the body now — the HttpRequestMessage (and its ByteArrayContent) may be
        // disposed by the caller before the test reads it back via Requests[i].Content.
        string? body = request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : null;

        Requests.Add(request);
        RequestBodies.Add(body);

        if (_requestIndex < _responses.Count)
        {
            var factory = _responses[_requestIndex++];
            return factory(request);
        }

        throw new InvalidOperationException(
            "MockHttpHandler has no more queued responses. Ensure the test enqueues enough responses for all expected requests.");
    }
}
