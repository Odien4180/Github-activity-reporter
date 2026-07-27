using GitHubActivityReporter.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("github-activity-reporter");
    config.UseStrictParsing();
    config.PropagateExceptions();

    config.AddCommand<InitCommand>("init")
        .WithDescription("Interactive first time setup: configuration, workflow and next steps.");

    config.AddCommand<ConfigureCommand>("configure")
        .WithDescription("Update an existing activity-reporter.yml interactively.");

    config.AddCommand<PreviewCommand>("preview")
        .WithDescription("Generate every output locally without publishing anything.");

    config.AddCommand<RunCommand>("run")
        .WithDescription("Run the full pipeline: collect, render, validate and publish.");

    config.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Diagnose the local setup, credentials and generated output.");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Scan generated files for private identifiers and secrets.");

    config.AddCommand<InstallWorkflowCommand>("install-workflow")
        .WithDescription("Generate .github/workflows/update-activity-report.yml.");
});

try
{
    return await app.RunAsync(args);
}
catch (Exception exception)
{
    Spectre.Console.AnsiConsole.MarkupLineInterpolated($"[red]✗[/] {exception.Message}");
    return 1;
}
