namespace VSlices.Tooling;

internal static class UpdateCommands
{
    /// <summary>Updates VSlices tooling components.</summary>
    /// <param name="self">Update the standalone VSlices CLI executable.</param>
    /// <param name="ruleset">Update the project-local ruleset snapshot from configured ruleset provenance.</param>
    /// <param name="channel">Override the configured CLI update channel for this invocation.</param>
    /// <param name="source">Override the configured CLI update source for this invocation.</param>
    /// <param name="pullRequest">Override the configured pull request used by the build update channel.</param>
    /// <param name="check">Check whether a CLI update is available without replacing the executable.</param>
    public static Task<int> Update(
        bool self = false,
        bool ruleset = false,
        string? channel = null,
        string? source = null,
        int? pullRequest = null,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        if (!self && !ruleset)
        {
            TerminalOutput.Error(
                "UPD000: Specify what to update. Supported surfaces: 'vslices update --self' and 'vslices update --ruleset'. Plain 'vslices update' is not defined yet.");
            return Task.FromResult(2);
        }

        if (self && ruleset)
        {
            TerminalOutput.Error(
                "UPD001: Updating self and ruleset together is not defined yet. Run the explicit update surfaces separately while aggregate update semantics are being established.");
            return Task.FromResult(2);
        }

        var configuration = ProjectConfiguration.LoadNearest(Environment.CurrentDirectory);

        if (ruleset)
        {
            if (check)
            {
                TerminalOutput.Error(
                    "UPD002: --check currently applies only to 'vslices update --self'.");
                return Task.FromResult(2);
            }

            if (configuration is null)
            {
                TerminalOutput.Error(
                    "UPD010: Could not locate .vslices/config.yaml. Run 'vslices init' before updating the project ruleset.");
                return Task.FromResult(1);
            }

            return RulesetUpdater.Update(configuration, cancellationToken);
        }

        var resolvedSource = source
            ?? configuration?.UpdateSource
            ?? ProjectConfiguration.OfficialToolingSource;
        var resolvedChannel = channel
            ?? configuration?.UpdateChannel
            ?? ProjectConfiguration.DefaultUpdateChannel;
        var resolvedPullRequest = pullRequest
            ?? configuration?.UpdatePullRequest;

        TerminalOutput.Detail("Channel", resolvedChannel);
        if (resolvedChannel.Equals("build", StringComparison.OrdinalIgnoreCase) && resolvedPullRequest is not null)
            TerminalOutput.Detail("Pull request", $"#{resolvedPullRequest}");
        TerminalOutput.Detail("Mode", check ? "check only" : "install");
        TerminalOutput.BlankLine();

        return SelfUpdater.Update(
            resolvedSource,
            resolvedChannel,
            resolvedPullRequest,
            check,
            cancellationToken);
    }
}
