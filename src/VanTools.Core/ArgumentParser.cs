using System.Text;

namespace VanTools.Core;

// Configures and parses the options of a single command.
public sealed class ArgumentParser {
    private readonly List<OptionDefinition> options = [];

    // The configured options in the order they were declared.
    public IReadOnlyList<OptionDefinition> Options => options;

    // The option summary used in usage lines, for example "--s <value> --t <value>".
    public string UsageLine => string.Join(" ", options.Select(option => $"--{option.Name} <value>"));

    // Declares a required value option.
    // name: the option name without leading dashes.
    // description: the text shown for the option in help output.
    // Returns: this parser, so declarations can be chained.
    // Throws ArgumentException: when name or description is null, empty, or whitespace.
    // Throws InvalidOperationException: when the option is already configured.
    public ArgumentParser Required(string name, string description) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (options.Any(option => option.Name == name)) {
            throw new InvalidOperationException($"Option '--{name}' is already configured.");
        }

        options.Add(new OptionDefinition(name, description, IsRequired: true));

        return this;
    }

    // Parses the arguments that follow a subcommand name.
    // args: the arguments to parse; options may appear in any order, and values may be given as "--name value" or "--name=value".
    // Returns: the parsed option values.
    // Throws HelpRequestedException: when "--help" or "-h" appears anywhere in args.
    // Throws UsageException: when an option is unknown, repeated, missing a value, or required and absent, or when an argument does not start with "--".
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

    // Renders the help text for a command.
    // commandName: the name shown in the usage line.
    // description: the one-line command description shown below the usage line.
    // Returns: the help text listing the usage line, the description, every configured option, and "--help".
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

// One configured option: its name, the description shown in help output, and whether it must be supplied.
public sealed record OptionDefinition(string Name, string Description, bool IsRequired);
