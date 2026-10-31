using System.Text;

namespace VanTools.Core;

public sealed class ArgumentParser {
    private readonly List<OptionDefinition> options = [];

    public IReadOnlyList<OptionDefinition> Options => options;

    public string UsageLine => string.Join(" ", options.Select(option => $"--{option.Name} <value>"));

    public ArgumentParser Required(string name, string description) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (options.Any(option => option.Name == name)) {
            throw new InvalidOperationException($"Option '--{name}' is already configured.");
        }

        options.Add(new OptionDefinition(name, description, IsRequired: true));

        return this;
    }

    public ParsedArguments Parse(string[] args) {
        ArgumentNullException.ThrowIfNull(args);

        Dictionary<string, string> values = new(StringComparer.Ordinal);

        for (int index = 0; index < args.Length; index++) {
            string argument = args[index];

            if (argument is "--help" or "-h") {
                throw new HelpRequestedException();
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal)) {
                throw new UsageException($"Unexpected argument '{argument}'.");
            }

            string name;
            string? value;
            int separator = argument.IndexOf('=', StringComparison.Ordinal);

            if (separator >= 0) {
                name = argument[2..separator];
                value = argument[(separator + 1)..];
            } else {
                name = argument[2..];
                value = null;
            }

            if (options.Find(option => option.Name == name) is null) {
                throw new UsageException($"Unknown option '--{name}'.");
            }

            if (values.ContainsKey(name)) {
                throw new UsageException($"Option '--{name}' was specified more than once.");
            }

            if (value is null) {
                if (index + 1 >= args.Length) {
                    throw new UsageException($"Option '--{name}' requires a value.");
                }

                value = args[++index];
            }

            values[name] = value;
        }

        string[] missing =
        [
            .. options
                .Where(option => option.IsRequired && !values.ContainsKey(option.Name))
                .Select(option => $"--{option.Name}")
        ];

        if (missing.Length > 0) {
            throw new UsageException($"Missing required option(s): {string.Join(", ", missing)}.");
        }

        return new ParsedArguments(values);
    }

    public string RenderHelp(string commandName, string description) {
        StringBuilder builder = new();

        builder.AppendLine($"Usage: {commandName} {UsageLine}");
        builder.AppendLine();
        builder.AppendLine(description);
        builder.AppendLine();
        builder.AppendLine("Options:");

        foreach (OptionDefinition option in options) {
            builder.AppendLine($"  --{option.Name} <value>   {option.Description}");
        }

        builder.AppendLine("  --help              show this help");

        return builder.ToString();
    }
}

public sealed record OptionDefinition(string Name, string Description, bool IsRequired);
