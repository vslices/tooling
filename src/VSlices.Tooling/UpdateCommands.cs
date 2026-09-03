namespace VSlices.Tooling;

internal static class UpdateCommands
{
    /// <summary>Updates VSlices tooling components.</summary>
    /// <param name="self">Update the standalone VSlices CLI executable.</param>
    /// <param name="channel">Override the configured update channel for this invocation.</param>
    /// <param name="source">Override the configured CLI update source for this invocation.</param>
    /// <param name="check">Check whether an update is available without replacing the executable.</param>
    public static Task<int> Update(
        bool self = false,
        string? channel = null,
        string? source = null,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        if (!self)
        {
            Console.Error.WriteLine(
                "UPD000: Specify what to update. Current supported surface: 'vslices update --self'.");
            return Task.FromResult(2);
        }

        var configuration = ProjectConfiguration.LoadNearest(Environment.CurrentDirectory);
        var resolvedSource = source
            ?? configuration?.UpdateSource
            ?? ProjectConfiguration.OfficialToolingSource;
        var resolvedChannel = channel
            ?? configuration?.UpdateChannel
            ?? ProjectConfiguration.DefaultUpdateChannel;

        return SelfUpdater.Update(
            resolvedSource,
            resolvedChannel,
            check,
            cancellationToken);
    }
}
