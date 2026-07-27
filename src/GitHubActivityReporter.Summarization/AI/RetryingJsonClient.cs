using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GitHubActivityReporter.Summarization.AI;

public abstract class RetryingJsonClient
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetries;

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
                throw new HttpRequestException(
                    $"AI provider returned HTTP {(int)response.StatusCode}.",
                    inner: null,
                    response.StatusCode);
            }

            var delay = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
            await Task.Delay(delay > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : delay, cancellationToken)
                .ConfigureAwait(false);
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
