using GitHubActivityReporter.GitHub.Mapping;

namespace GitHubActivityReporter.GitHub.Tests;

public sealed class OctokitActivityMapperTests
{
    [Theory]
    [InlineData(0, 0, null, 1)]
    [InlineData(0, 3, null, 3)]
    [InlineData(2, 0, null, 2)]
    [InlineData(2, 3, null, 2)]
    [InlineData(2, 3, 10, 10)]
    [InlineData(0, 0, 0, 1)]
    public void ResolvePushEventCount_PreservesPushActivity(
        int includedCommitCount,
        int reportedSize,
        int? comparedCommitCount,
        int expected)
    {
        var result = OctokitActivityMapper.ResolvePushEventCount(
            includedCommitCount,
            reportedSize,
            comparedCommitCount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveCommitSubject_uses_compare_details_when_event_payload_omits_them()
    {
        var subject = OctokitActivityMapper.ResolveCommitSubject(
            Array.Empty<string?>(),
            ["Describe the completed work"],
            0);

        Assert.Equal("Describe the completed work", subject);
    }
}
