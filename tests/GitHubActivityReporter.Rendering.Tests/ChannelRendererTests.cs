using System.Text.Json;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Rendering.Email;
using GitHubActivityReporter.Rendering.Slack;

namespace GitHubActivityReporter.Rendering.Tests;

public sealed class ChannelRendererTests
{
    [Fact]
    public async Task Email_renderer_creates_html_and_plain_text_alternatives()
    {
        var context = Phase2RendererTests.CreateContext();
        context.Configuration.Outputs.Email.Enabled = true;

        var rendered = await new EmailReportRenderer().RenderAsync(
            Phase2RendererTests.CreateReport(), context, CancellationToken.None);

        Assert.Equal(KnownRenderers.EmailHtml, rendered.RendererId);
        Assert.Collection(
            rendered.Artifacts,
            html =>
            {
                Assert.Equal(RenderedArtifactKind.Html, html.Kind);
                Assert.Contains("GitHub activity for example-user", html.Content, StringComparison.Ordinal);
                Assert.Contains("Improved delivery reliability", html.Content, StringComparison.Ordinal);
            },
            text =>
            {
                Assert.Equal(RenderedArtifactKind.PlainText, text.Kind);
                Assert.Contains("Private activity (aggregate only)", text.Content, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task Slack_renderer_creates_valid_block_kit_json()
    {
        var context = Phase2RendererTests.CreateContext();
        context.Configuration.Outputs.Slack.Enabled = true;

        var rendered = await new SlackBlockKitRenderer().RenderAsync(
            Phase2RendererTests.CreateReport(), context, CancellationToken.None);

        using var json = JsonDocument.Parse(rendered.PrimaryArtifact!.Content);
        Assert.Equal("GitHub activity for example-user", json.RootElement.GetProperty("text").GetString());
        Assert.True(json.RootElement.GetProperty("blocks").GetArrayLength() >= 3);
        Assert.Contains("Improved delivery reliability", rendered.PrimaryArtifact.Content, StringComparison.Ordinal);
    }
}
