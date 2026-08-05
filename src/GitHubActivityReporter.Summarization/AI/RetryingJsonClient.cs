using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GitHubActivityReporter.Summarization.AI;

public abstract class RetryingJsonClient
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetries;
    private static readonly Random _jitter = Random.Shared;

    protected RetryingJsonClient(HttpClient? httpClient, int maxRetries)
    {
        _httpClient = httpClient ?? new HttpClient();
        _maxRetries = Math.Clamp(maxRetries, 0, 5);
    }

    protected async Task<JsonDocument> PostAsync(
        Uri endpoint,
        string bearerToken,
        object payload,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            if (headers is not null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (attempt >= _maxRetries || !IsTransient(response.StatusCode))
            {
                // Read a short truncated body for diagnostics — no secrets expected in error bodies,
                // but we cap at 512 chars to avoid bloated logs.
                var bodySnippet = await ReadBodySnippetAsync(response.Content, cancellationToken).ConfigureAwait(false);
                var detail = string.IsNullOrWhiteSpace(bodySnippet)
                    ? string.Empty
                    : $" Response: {bodySnippet}";

                throw new HttpRequestException(
                    $"AI provider {endpoint.Host}{endpoint.AbsolutePath} returned HTTP {(int)response.StatusCode}.{detail}",
                    inner: null,
                    response.StatusCode);
            }

            // Exponential backoff with full jitter, capped at 30 s, respecting Retry-After when present.
            var retryAfter = response.Headers.RetryAfter?.Delta;
            var backoffBase = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
            var cap = TimeSpan.FromSeconds(30);
            var jitterMs = _jitter.NextDouble() * Math.Min(backoffBase.TotalMilliseconds, cap.TotalMilliseconds);
            var delay = retryAfter.HasValue
                ? (retryAfter.Value > cap ? cap : retryAfter.Value)
                : TimeSpan.FromMilliseconds(jitterMs);

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadBodySnippetAsync(HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            var body = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            const int maxLength = 512;
            return body.Length <= maxLength ? body : body[..maxLength] + "…";
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}
