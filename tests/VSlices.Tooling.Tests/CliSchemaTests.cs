using System.Text.Json;

namespace VSlices.Tooling.Tests;

public sealed class CliSchemaTests
{
    [Fact]
    public async Task Schema_exports_clean_json_and_is_available_without_a_vslices_project()
    {
        using var environment = new ToolingTestProject();
        var result = await environment.Run(environment.Root, "--cli-schema");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.True(document.RootElement.GetArrayLength() >= 25,
            "The schema should cover the actual registered public CLI, not a handwritten subset.");
        Assert.StartsWith("[", result.StandardOutput.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("VSlices Tooling", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Schema_contains_commands_and_options_derived_from_public_cli()
    {
        using var environment = new ToolingTestProject();
        var result = await environment.Run(environment.Root, "--cli-schema");
        Assert.Equal(0, result.ExitCode);

        using var document = JsonDocument.Parse(result.StandardOutput);
        var commands = document.RootElement.EnumerateArray().ToArray();
        var names = commands.Select(item => item.GetProperty("CommandName").GetString()).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var required in new[]
        {
            "init",
            "new vsir", "new document", "discovery document", "update document",
            "read document", "read nexus", "read continuity-path", "read support-note",
            "new docs-standard", "update docs-standard", "list docs-standards",
            "new ruleset-standard", "update ruleset-standard", "list ruleset-standards",
            "lower", "transpile", "rebase", "search"
        })
            Assert.Contains(required, names);

        var read = commands.Single(item =>
            item.GetProperty("CommandName").GetString() == "read document");
        var options = read.GetProperty("Options").EnumerateArray().ToArray();
        Assert.Contains(options, option => HasOption(option, "--question-id"));
        var deep = Assert.Single(options.Where(option => HasOption(option, "--deep")));
        Assert.True(deep.GetProperty("IsFlag").GetBoolean());

        var update = commands.Single(item =>
            item.GetProperty("CommandName").GetString() == "update docs-standard");
        Assert.Contains(update.GetProperty("Options").EnumerateArray(),
            option => HasOption(option, "--skip-confirm"));

        Assert.True(
            read.GetProperty("Description").GetString()?.Contains("question", StringComparison.OrdinalIgnoreCase)
            == true,
            "Descriptions should come from command XML documentation.");
    }

    [Fact]
    public async Task Relational_discovery_and_update_commands_have_useful_descriptions()
    {
        using var environment = new ToolingTestProject();
        var schema = await environment.Run(environment.Root, "--cli-schema");
        Assert.Equal(0, schema.ExitCode);

        using var data = JsonDocument.Parse(schema.StandardOutput);
        foreach (var commandName in new[]
        {
            "discovery continuity-path", "discovery nexus", "discovery support-note",
            "update continuity-path", "update nexus", "update support-note"
        })
        {
            var command = data.RootElement.EnumerateArray().Single(
                item => item.GetProperty("CommandName").GetString() == commandName);
            var description = command.GetProperty("Description").GetString();
            Assert.False(string.IsNullOrWhiteSpace(description),
                $"Missing CLI schema description for '{commandName}'.");
            Assert.True(description!.Length > 20,
                $"Description for '{commandName}' is not sufficiently useful.");

            var help = await environment.Run(environment.Root,
                commandName.Split(' ').Concat(["--help"]).ToArray());
            Assert.Equal(0, help.ExitCode);
            Assert.Contains(description, help.StandardOutput, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Documentary_discovery_and_update_commands_have_descriptions_in_schema()
    {
        using var environment = new ToolingTestProject();
        var result = await environment.Run(environment.Root, "--cli-schema");
        Assert.Equal(0, result.ExitCode);

        using var json = JsonDocument.Parse(result.StandardOutput);
        var commands = json.RootElement.EnumerateArray().ToArray();
        foreach (var family in new[] { "continuity-path", "nexus", "support-note" })
        {
            foreach (var verb in new[] { "discovery", "update" })
            {
                var name = $"{verb} {family}";
                var command = Assert.Single(commands.Where(item =>
                    item.GetProperty("CommandName").GetString() == name));
                var description = command.GetProperty("Description").GetString();
                Assert.False(string.IsNullOrWhiteSpace(description),
                    $"{name} must describe its capability in --help and --cli-schema.");
                var help = await environment.Run(environment.Root, verb, family, "--help");
                Assert.Equal(0, help.ExitCode);
                Assert.Contains(description!, help.StandardOutput, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task Json_schema_flags_match_executable_help_surface()
    {
        using var environment = new ToolingTestProject();
        var schema = await environment.Run(environment.Root, "--cli-schema");
        var help = await environment.Run(environment.Root, "read", "document", "--help");
        Assert.Equal(0, schema.ExitCode);
        Assert.Equal(0, help.ExitCode);
        using var data = JsonDocument.Parse(schema.StandardOutput);
        var command = data.RootElement.EnumerateArray().Single(
            item => item.GetProperty("CommandName").GetString() == "read document");

        foreach (var option in command.GetProperty("Options").EnumerateArray())
        {
            if (option.GetProperty("IsHidden").GetBoolean()) continue;
            var flags = option.GetProperty("Options").EnumerateArray()
                .Select(value => value.GetString()).Where(value => value is not null);
            foreach (var flag in flags)
                Assert.Contains(flag!, help.StandardOutput, StringComparison.Ordinal);
        }
    }

    private static bool HasOption(JsonElement option, string name) =>
        option.GetProperty("Options").EnumerateArray()
            .Any(value => value.GetString() == name);
}
