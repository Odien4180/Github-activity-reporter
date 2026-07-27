using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Publishing.Email;
using GitHubActivityReporter.Publishing.Slack;
using NSubstitute;

namespace GitHubActivityReporter.Publishing.Tests;

public sealed class ChannelPublisherTests
{
    [Fact]
    public async Task Email_publisher_sends_both_alternatives_from_json_secret()
    {
        var client = Substitute.For<IEmailDeliveryClient>();
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Outputs.Email.Enabled = true;
        config.Publishers.Email.Enabled = true;
        var secret = """{"host":"smtp.example.com","port":587,"username":"user","password":"secret","from":"from@example.com","to":"to@example.com","useSsl":true}""";
        var publisher = new EmailReportPublisher(client, _ => secret);

        var result = await publisher.PublishAsync(
            EmailReport(), PublisherTestData.Context(Path.GetTempPath(), config), CancellationToken.None);

        Assert.Equal(PublishOutcome.Published, result.Outcome);
        await client.Received(1).SendAsync(
            Arg.Is<EmailDelivery>(delivery =>
                delivery != null
                && delivery.Credentials.Password == "secret"
                && delivery.HtmlBody == "<p>html</p>"
                && delivery.TextBody == "text"),
            Arg.Any<CancellationToken>());
        Assert.DoesNotContain("secret", result.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slack_publisher_posts_payload_to_https_webhook()
    {
        var client = Substitute.For<ISlackWebhookClient>();
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Outputs.Slack.Enabled = true;
        config.Publishers.Slack.Enabled = true;
        var publisher = new SlackReportPublisher(client, _ => "https://hooks.slack.test/services/secret");

        var result = await publisher.PublishAsync(
            SlackReport(), PublisherTestData.Context(Path.GetTempPath(), config), CancellationToken.None);

        Assert.Equal(PublishOutcome.Published, result.Outcome);
        await client.Received(1).PostAsync(
            Arg.Is<Uri>(uri => uri != null && uri.Host == "hooks.slack.test"),
            "{\"blocks\":[]}",
            Arg.Any<CancellationToken>());
        Assert.DoesNotContain("hooks.slack.test", result.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Channel_publishers_do_not_read_secrets_during_dry_run()
    {
        string? ThrowIfRead(string _) => throw new InvalidOperationException("secret was read");
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Outputs.Email.Enabled = true;
        config.Outputs.Slack.Enabled = true;
        config.Publishers.Email.Enabled = true;
        config.Publishers.Slack.Enabled = true;
        var context = PublisherTestData.Context(Path.GetTempPath(), config, dryRun: true);

        var email = await new EmailReportPublisher(getSecret: ThrowIfRead).PublishAsync(
            EmailReport(), context, CancellationToken.None);
        var slack = await new SlackReportPublisher(getSecret: ThrowIfRead).PublishAsync(
            SlackReport(), context, CancellationToken.None);

        Assert.Equal(PublishOutcome.Skipped, email.Outcome);
        Assert.Equal(PublishOutcome.Skipped, slack.Outcome);
    }

    private static RenderedReport EmailReport() => new()
    {
        RendererId = KnownRenderers.EmailHtml,
        Artifacts =
        [
            new RenderedArtifact { Name = "html", RelativePath = "email.html", Content = "<p>html</p>", Kind = RenderedArtifactKind.Html },
            new RenderedArtifact { Name = "text", RelativePath = "email.txt", Content = "text", Kind = RenderedArtifactKind.PlainText }
        ]
    };

    private static RenderedReport SlackReport() => new()
    {
        RendererId = KnownRenderers.SlackBlocks,
        Artifacts =
        [
            new RenderedArtifact { Name = "slack", RelativePath = "slack.json", Content = "{\"blocks\":[]}", Kind = RenderedArtifactKind.Json }
        ]
    };
}
