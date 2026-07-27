using System.Net.Http.Headers;
using System.Text;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Publishing.Slack;

public interface ISlackWebhookClient
{
    Task PostAsync(Uri webhook, string json, CancellationToken cancellationToken);
}

public sealed class SlackWebhookClient : ISlackWebhookClient
{
    private readonly HttpClient _httpClient;

    public SlackWebhookClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task PostAsync(Uri webhook, string json, CancellationToken cancellationToken)
    {
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await _httpClient.PostAsync(webhook, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class SlackReportPublisher : IReportPublisher
{
    private readonly ISlackWebhookClient _client;
    private readonly Func<string, string?> _getSecret;

    public SlackReportPublisher(
        ISlackWebhookClient? client = null,
        Func<string, string?>? getSecret = null)
    {
        _client = client ?? new SlackWebhookClient();
        _getSecret = getSecret ?? Environment.GetEnvironmentVariable;
    }

    public string PublisherId => KnownPublishers.Slack;

    public async Task<PublishResult> PublishAsync(
        RenderedReport report,
        PublisherContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Configuration.Publishers.Slack.Enabled)
        {
            return PublishResult.Skipped(PublisherId, "Slack publisher is disabled.");
        }

        if (!string.Equals(report.RendererId, KnownRenderers.SlackBlocks, StringComparison.OrdinalIgnoreCase))
        {
            return PublishResult.Skipped(PublisherId, "Only the Slack Block Kit report is sent by the Slack publisher.");
        }

        if (context.DryRun)
        {
            return PublishResult.Skipped(PublisherId, "Dry run: no Slack message was sent.");
        }

        var secretName = context.Configuration.Publishers.Slack.SecretName;
        var secret = _getSecret(secretName);
        if (!Uri.TryCreate(secret, UriKind.Absolute, out var webhook)
            || webhook.Scheme != Uri.UriSchemeHttps)
        {
            return PublishResult.Failed(PublisherId, $"Slack webhook secret '{secretName}' must contain a valid HTTPS URL.");
        }

        var json = report.PrimaryArtifact?.Content;
        if (string.IsNullOrWhiteSpace(json))
        {
            return PublishResult.Failed(PublisherId, "Slack report contains no payload.");
        }

        try
        {
            await _client.PostAsync(webhook, json, cancellationToken).ConfigureAwait(false);
            return PublishResult.Published(PublisherId, Array.Empty<string>(), "Slack message sent.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PublishResult.Failed(PublisherId, $"Slack delivery failed ({exception.GetType().Name}).");
        }
    }
}
