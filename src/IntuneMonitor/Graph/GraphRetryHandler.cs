using System.Net;
using Microsoft.Extensions.Logging;

namespace IntuneMonitor.Graph;

/// <summary>
/// Provides centralized retry logic for Microsoft Graph HTTP calls.
/// Handles HTTP 429 (throttling) with Retry-After, transient 5xx server errors
/// with exponential backoff, and <see cref="HttpRequestException"/> transient failures.
/// </summary>
internal static class GraphRetryHandler
{
    /// <summary>Default delay in seconds when no Retry-After header is present on a 429 response.</summary>
    private const int DefaultRetryDelaySeconds = 30;

    /// <summary>Maximum number of attempts for transient/throttle failures per request.</summary>
    private const int DefaultMaxAttempts = 5;

    /// <summary>Base delay in seconds for exponential backoff on server errors.</summary>
    private const int BaseBackoffSeconds = 5;

    /// <summary>
    /// Sends a GET request with retry logic for HTTP 429 and transient 5xx errors.
    /// Returns the response body as a string, or null if all retries are exhausted.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use.</param>
    /// <param name="url">The URL to GET.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="maxAttempts">Maximum number of attempts (default 5).</param>
    /// <returns>Response body string, or null on failure.</returns>
    public static async Task<string?> SendWithRetryAsync(
        HttpClient httpClient,
        string url,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = DefaultMaxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts - 1)
            {
                logger.LogWarning(ex, "HTTP request failed (attempt {Attempt}/{MaxAttempts}), retrying...",
                    attempt + 1, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(DefaultRetryDelaySeconds), cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    logger.LogWarning("Throttled (HTTP 429). Waiting {RetryAfterSeconds}s before retry (attempt {Attempt}/{MaxAttempts})",
                        retryAfter, attempt + 1, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(retryAfter), cancellationToken);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < maxAttempts - 1)
                {
                    var delay = (int)Math.Pow(2, attempt) * BaseBackoffSeconds;
                    logger.LogWarning("Server error (HTTP {StatusCode}). Retrying in {Delay}s (attempt {Attempt}/{MaxAttempts})",
                        (int)response.StatusCode, delay, attempt + 1, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                    continue;
                }

                // Non-retryable error
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Request failed with HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);
                return null;
            }
        }

        logger.LogError("Request failed after {MaxAttempts} attempts", maxAttempts);
        return null;
    }

    /// <summary>
    /// Sends a POST request with retry logic for HTTP 429 and transient 5xx errors.
    /// Returns the response, which the caller must dispose.
    /// Throws <see cref="HttpRequestException"/> if all retries are exhausted on transient errors.
    /// </summary>
    public static async Task<HttpResponseMessage> PostWithRetryAsync(
        HttpClient httpClient,
        string url,
        HttpContent content,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = DefaultMaxAttempts)
    {
        return await SendRequestWithRetryAsync(httpClient, HttpMethod.Post, url, content, logger, cancellationToken, maxAttempts);
    }

    /// <summary>
    /// Sends an HTTP request with a specified method and retry logic for transient errors.
    /// Returns the final <see cref="HttpResponseMessage"/> (success or non-retryable failure).
    /// </summary>
    public static async Task<HttpResponseMessage> SendRequestWithRetryAsync(
        HttpClient httpClient,
        HttpMethod method,
        string url,
        HttpContent? content,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = DefaultMaxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(method, url);
            if (content != null)
            {
                // Create a copy of content for retry since HttpContent can only be consumed once
                var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
                request.Content = new ByteArrayContent(bytes);
                if (content.Headers.ContentType != null)
                    request.Content.Headers.ContentType = content.Headers.ContentType;
            }

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts - 1)
            {
                logger.LogWarning(ex, "HTTP {Method} request failed (attempt {Attempt}/{MaxAttempts}), retrying...",
                    method, attempt + 1, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(DefaultRetryDelaySeconds), cancellationToken);
                continue;
            }

            if (response.IsSuccessStatusCode)
                return response;

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxAttempts - 1)
            {
                var retryAfter = GetRetryAfterSeconds(response);
                logger.LogWarning("Throttled (HTTP 429). Waiting {RetryAfterSeconds}s before retry (attempt {Attempt}/{MaxAttempts})",
                    retryAfter, attempt + 1, maxAttempts);
                response.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(retryAfter), cancellationToken);
                continue;
            }

            if ((int)response.StatusCode >= 500 && attempt < maxAttempts - 1)
            {
                var delay = (int)Math.Pow(2, attempt) * BaseBackoffSeconds;
                logger.LogWarning("Server error (HTTP {StatusCode}). Retrying in {Delay}s (attempt {Attempt}/{MaxAttempts})",
                    (int)response.StatusCode, delay, attempt + 1, maxAttempts);
                response.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                continue;
            }

            // Non-retryable error — return as-is for caller to handle
            return response;
        }

        // Should not reach here, but if it does return the last response or throw
        throw new HttpRequestException($"Request to {url} failed after {maxAttempts} attempts");
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return Math.Max(1, (int)delta.TotalSeconds);

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = (int)(date - DateTimeOffset.UtcNow).TotalSeconds;
            return Math.Max(1, wait);
        }

        return DefaultRetryDelaySeconds;
    }
}
