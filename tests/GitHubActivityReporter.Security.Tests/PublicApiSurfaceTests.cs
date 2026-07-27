using System.Reflection;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Summarization.RuleBased;

namespace GitHubActivityReporter.Security.Tests;

/// <summary>
/// Type level guarantees: the private raw event model must not be reachable
/// through any public API, and no summarizer can ever accept it.
/// </summary>
public sealed class PublicApiSurfaceTests
{
    private static readonly Assembly[] ProductAssemblies =
    [
        typeof(ActivityReport).Assembly,
        typeof(RuleBasedPublicActivitySummarizer).Assembly,
        typeof(Rendering.Markdown.MarkdownReportRenderer).Assembly,
        typeof(Publishing.FileSystem.LocalFileReportPublisher).Assembly,
        typeof(GitHub.Collectors.GitHubActivityCollector).Assembly,
        typeof(Bootstrap.GitHubActions.WorkflowGenerator).Assembly
    ];

    private static readonly Type PrivateEventType =
        typeof(ActivityReport).Assembly.GetType("GitHubActivityReporter.Core.Models.PrivateActivityEvent")!;

    [Fact]
    public void PrivateActivityEvent_exists_but_is_not_public()
    {
        Assert.NotNull(PrivateEventType);
        Assert.False(PrivateEventType.IsPublic);
        Assert.False(PrivateEventType.IsVisible);
    }

    [Fact]
    public void PrivateActivityAggregator_is_internal()
    {
        var aggregatorInterface = typeof(ActivityReport).Assembly
            .GetType("GitHubActivityReporter.Core.Abstractions.IPrivateActivityAggregator");

        Assert.NotNull(aggregatorInterface);
        Assert.False(aggregatorInterface!.IsVisible);
    }

    [Fact]
    public void No_public_api_exposes_the_private_event_model()
    {
        var offenders = new List<string>();

        foreach (var assembly in ProductAssemblies)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (Mentions(method.ReturnType) || method.GetParameters().Any(p => Mentions(p.ParameterType)))
                    {
                        offenders.Add($"{type.FullName}.{method.Name}");
                    }
                }

                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (Mentions(property.PropertyType))
                    {
                        offenders.Add($"{type.FullName}.{property.Name}");
                    }
                }

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (Mentions(field.FieldType))
                    {
                        offenders.Add($"{type.FullName}.{field.Name}");
                    }
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Public_summarizer_contract_only_accepts_public_events()
    {
        var method = typeof(IPublicActivitySummarizer).GetMethod(nameof(IPublicActivitySummarizer.SummarizeAsync))!;
        var parameter = method.GetParameters()[0];

        Assert.Equal(typeof(IReadOnlyList<PublicActivityEvent>), parameter.ParameterType);

        var summarizerImplementations = ProductAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IPublicActivitySummarizer).IsAssignableFrom(t) && !t.IsInterface)
            .ToArray();

        Assert.NotEmpty(summarizerImplementations);

        foreach (var implementation in summarizerImplementations)
        {
            foreach (var candidate in implementation.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Assert.DoesNotContain(candidate.GetParameters(), p => Mentions(p.ParameterType));
            }
        }
    }

    [Fact]
    public void Collected_activity_only_exposes_a_private_counter()
    {
        var properties = typeof(CollectedActivity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.Name)
            .ToArray();

        Assert.Contains(nameof(CollectedActivity.PublicEvents), properties);
        Assert.Contains(nameof(CollectedActivity.PrivateEventCount), properties);
        Assert.DoesNotContain("PrivateEvents", properties);
    }

    private static bool Mentions(Type type)
    {
        if (type == PrivateEventType)
        {
            return true;
        }

        if (type.IsArray)
        {
            return Mentions(type.GetElementType()!);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(Mentions);
    }
}
