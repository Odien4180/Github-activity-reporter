using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Validation;

public interface IOutputValidator
{
    ValidationResult Validate(
        RenderedReport report,
        ValidationContext context);
}
