using System.Net;
using IntuneMonitor.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Tests;

public class GraphRetryHandlerTests
{
    private readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger("Test");

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates an HttpClient backed by a queue of canned responses.
    /// Responses are returned in order; if the queue is exhausted the last response repeats.
    /// </summary>
    private static HttpClient CreateClient(params HttpResponseMessage[] responses)
    {
        var handler = new QueueMessageHandler(responses);
        return new HttpClient(handler);
    }

    // -----------------------------------------------------------------------
    // SendWithRetryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendWithRetryAsync_SuccessOnFirstAttempt_ReturnsBody()
    {
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") });

        var result = await GraphRetryHandler.SendWithRetryAsync(client, "https://graph.test/v1", _logger, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("ok", result);
    }

    [Fact]
    public async Task SendWithRetryAsync_TransientThenSuccess_RetriesAndReturnsBody()
    {
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("error") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") });

        var result = await GraphRetryHandler.SendWithRetryAsync(client, "https://graph.test/v1", _logger, CancellationToken.None, maxAttempts: 3);

        Assert.NotNull(result);
        Assert.Contains("ok", result);
    }

    [Fact]
    public async Task SendWithRetryAsync_AllAttemptsExhausted_ReturnsNull()
    {
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("err") },
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("err") });

        var result = await GraphRetryHandler.SendWithRetryAsync(client, "https://graph.test/v1", _logger, CancellationToken.None, maxAttempts: 2);

        Assert.Null(result);
    }

    [Fact]
    public async Task SendWithRetryAsync_429OnLastAttempt_DoesNotDelayAndReturnsNull()
    {
        // When 429 occurs on the final attempt, it should not sleep — just fail.
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("throttled") });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await GraphRetryHandler.SendWithRetryAsync(client, "https://graph.test/v1", _logger, CancellationToken.None, maxAttempts: 1);
        sw.Stop();

        Assert.Null(result);
        // Should complete quickly since there's no retry delay on the last attempt
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Expected fast failure but took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task SendWithRetryAsync_NonRetryableError_ReturnsNull()
    {
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("forbidden") });

        var result = await GraphRetryHandler.SendWithRetryAsync(client, "https://graph.test/v1", _logger, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SendWithRetryAsync_HttpRequestExceptionThenSuccess_Retries()
    {
        var handler = new ExceptionThenSuccessHandler(
            throwCount: 1,
            successResponse: new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") });
        using var client = new HttpClient(handler);

        var result = await GraphRetryHandler.SendWithRetryAsync(client, "https://graph.test/v1", _logger, CancellationToken.None, maxAttempts: 3);

        Assert.NotNull(result);
        Assert.Contains("ok", result);
    }

    // -----------------------------------------------------------------------
    // PostWithRetryAsync / SendRequestWithRetryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PostWithRetryAsync_SuccessOnFirstAttempt_ReturnsResponse()
    {
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"123\"}") });

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await GraphRetryHandler.PostWithRetryAsync(client, "https://graph.test/v1", content, _logger, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostWithRetryAsync_ServerErrorThenSuccess_Retries()
    {
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("down") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") });

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await GraphRetryHandler.PostWithRetryAsync(client, "https://graph.test/v1", content, _logger, CancellationToken.None, maxAttempts: 3);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task PostWithRetryAsync_NonRetryableError_ReturnsResponse()
    {
        using var client = CreateClient(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad") });

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await GraphRetryHandler.PostWithRetryAsync(client, "https://graph.test/v1", content, _logger, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendWithRetryAsync_429WithRetryAfterHeader_RespectsHeader()
    {
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("throttled") };
        throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));

        using var client = CreateClient(
            throttled,
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") });

        var result = await GraphRetryHandler.SendWithRetryAsync(client, "https://graph.test/v1", _logger, CancellationToken.None, maxAttempts: 3);

        Assert.NotNull(result);
        Assert.Contains("ok", result);
    }

    // -----------------------------------------------------------------------
    // Test message handlers
    // -----------------------------------------------------------------------

    /// <summary>Returns queued responses in order; repeats the last one if exhausted.</summary>
    private sealed class QueueMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage[] _responses;
        private int _index;

        public QueueMessageHandler(HttpResponseMessage[] responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var idx = Math.Min(_index, _responses.Length - 1);
            _index++;
            return Task.FromResult(_responses[idx]);
        }
    }

    /// <summary>Throws HttpRequestException for the first N calls, then returns a canned response.</summary>
    private sealed class ExceptionThenSuccessHandler : HttpMessageHandler
    {
        private int _throwCount;
        private readonly HttpResponseMessage _successResponse;

        public ExceptionThenSuccessHandler(int throwCount, HttpResponseMessage successResponse)
        {
            _throwCount = throwCount;
            _successResponse = successResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_throwCount > 0)
            {
                _throwCount--;
                throw new HttpRequestException("Transient network error");
            }
            return Task.FromResult(_successResponse);
        }
    }
}
