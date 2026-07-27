using System.Net;
using System.Net.Mail;
using System.Text.Json;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Publishing.Email;

public sealed record EmailCredentials
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;
    public string Subject { get; init; } = "GitHub activity report";
}

public sealed record EmailDelivery
{
    public required EmailCredentials Credentials { get; init; }
    public required string HtmlBody { get; init; }
    public required string TextBody { get; init; }
}

public interface IEmailDeliveryClient
{
    Task SendAsync(EmailDelivery delivery, CancellationToken cancellationToken);
}

public sealed class SmtpEmailDeliveryClient : IEmailDeliveryClient
{
    public async Task SendAsync(EmailDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var credentials = delivery.Credentials;

        using var message = new MailMessage
        {
            From = new MailAddress(credentials.From),
            Subject = credentials.Subject,
            Body = delivery.TextBody,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(credentials.To));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(delivery.TextBody, null, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(delivery.HtmlBody, null, "text/html"));

        using var smtp = new SmtpClient(credentials.Host, credentials.Port)
        {
            EnableSsl = credentials.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(credentials.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(credentials.Username, credentials.Password)
        };

        await smtp.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class EmailReportPublisher : IReportPublisher
{
    private static readonly JsonSerializerOptions CredentialJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IEmailDeliveryClient _client;
    private readonly Func<string, string?> _getSecret;

    public EmailReportPublisher(
        IEmailDeliveryClient? client = null,
        Func<string, string?>? getSecret = null)
    {
        _client = client ?? new SmtpEmailDeliveryClient();
        _getSecret = getSecret ?? Environment.GetEnvironmentVariable;
    }

    public string PublisherId => KnownPublishers.Email;

    public async Task<PublishResult> PublishAsync(
        RenderedReport report,
        PublisherContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Configuration.Publishers.Email.Enabled)
        {
            return PublishResult.Skipped(PublisherId, "Email publisher is disabled.");
        }

        if (!string.Equals(report.RendererId, KnownRenderers.EmailHtml, StringComparison.OrdinalIgnoreCase))
        {
            return PublishResult.Skipped(PublisherId, "Only the email report is sent by the email publisher.");
        }

        if (context.DryRun)
        {
            return PublishResult.Skipped(PublisherId, "Dry run: no email was sent.");
        }

        var secretName = context.Configuration.Publishers.Email.SecretName;
        var secret = _getSecret(secretName);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return PublishResult.Failed(PublisherId, $"Email credential secret '{secretName}' is not set.");
        }

        EmailCredentials? credentials;
        try
        {
            credentials = JsonSerializer.Deserialize<EmailCredentials>(secret, CredentialJsonOptions);
        }
        catch (JsonException)
        {
            return PublishResult.Failed(PublisherId, $"Email credential secret '{secretName}' is not valid JSON.");
        }

        if (!AreValid(credentials))
        {
            return PublishResult.Failed(PublisherId, "Email credentials require host, port, from and to values.");
        }

        var html = report.Artifacts.FirstOrDefault(a => a.Kind == RenderedArtifactKind.Html)?.Content;
        var text = report.Artifacts.FirstOrDefault(a => a.Kind == RenderedArtifactKind.PlainText)?.Content;
        if (html is null || text is null)
        {
            return PublishResult.Failed(PublisherId, "Email report must contain both HTML and plain-text artifacts.");
        }

        try
        {
            await _client.SendAsync(
                new EmailDelivery { Credentials = credentials!, HtmlBody = html, TextBody = text },
                cancellationToken).ConfigureAwait(false);
            return PublishResult.Published(PublisherId, Array.Empty<string>(), "Email sent.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PublishResult.Failed(PublisherId, $"Email delivery failed ({exception.GetType().Name}).");
        }
    }

    private static bool AreValid(EmailCredentials? credentials)
        => credentials is not null
           && !string.IsNullOrWhiteSpace(credentials.Host)
           && credentials.Port is > 0 and <= 65535
           && !string.IsNullOrWhiteSpace(credentials.From)
           && !string.IsNullOrWhiteSpace(credentials.To);
}
